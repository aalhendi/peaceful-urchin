using Dapper;
using Lending.Api.Application;
using Lending.Api.Domain;
using Npgsql;
using Shared.Vocabulary;

namespace Lending.Api.Infrastructure;

internal sealed class LitigationStore(NpgsqlDataSource dataSource) : ILitigationStore
{
    public async Task<OpenLitigationOutcome> OpenAsync(LoanId loanId, CourtId courtId, StaffActorId actorId)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        var id = LitigationId.New();
        var inserted = await connection.QuerySingleOrDefaultAsync<Guid?>("""
                                                                         INSERT INTO loan_litigations
                                                                             (id, loan_id, court_id, status, opened_by)
                                                                         SELECT @Id, loans.id, @CourtId, 'Pending', @ActorId
                                                                         FROM loans WHERE loans.id = @LoanId
                                                                         ON CONFLICT (loan_id, court_id) DO NOTHING
                                                                         RETURNING id
                                                                         """, new
        {
            Id = id.Value, LoanId = loanId.Value, CourtId = courtId.Value, ActorId = actorId.Value
        });
        if (inserted is not null) return new LitigationOpened(id);

        var loanExists = await connection.ExecuteScalarAsync<bool>(
            "SELECT EXISTS (SELECT 1 FROM loans WHERE id = @LoanId)",
            new { LoanId = loanId.Value });
        return loanExists ? new LitigationConflict() : new LitigationNotFound();
    }

    public async Task<LoanLitigation?> ReadAsync(LoanId loanId, LendingActor actor)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        var loan = await connection.QuerySingleOrDefaultAsync<LoanRow>("""
                                                                       SELECT customer_civil_id AS "CustomerCivilId",
                                                                              institution_id AS "InstitutionId"
                                                                       FROM loans
                                                                       WHERE id = @LoanId
                                                                         AND (@IsCinet OR institution_id = @InstitutionId)
                                                                       """, new
        {
            LoanId = loanId.Value,
            IsCinet = actor.InstitutionKind == InstitutionKind.Cinet,
            InstitutionId = actor.InstitutionId.Value
        });
        if (loan is null) return null;

        var rows = await connection.QueryAsync<LitigationRow>("""
                                                              SELECT id AS "Id", court_id AS "CourtId", status AS "Status",
                                                                     verdict_date AS "VerdictDate"
                                                              FROM loan_litigations
                                                              WHERE loan_id = @LoanId ORDER BY opened_at, id
                                                              """, new { LoanId = loanId.Value });
        var cases = rows.Select(ReadCase).ToArray();
        var customerId = CivilId.Parse(loan.CustomerCivilId).Value as CivilId
                         ?? throw new InvalidOperationException("Invalid customer Civil ID in the lending database.");
        var institutionId = InstitutionId.Parse(loan.InstitutionId).Value as InstitutionId
                            ?? throw new InvalidOperationException("Invalid institution ID in the lending database.");
        return new LoanLitigation(loanId, customerId, institutionId, cases);
    }

    public async Task<RecordVerdictOutcome> RecordVerdictAsync(LitigationId id, Verdict verdict, StaffActorId actorId)
    {
        var (status, verdictDate) = verdict switch
        {
            Guilty guilty => ("Guilty", guilty.VerdictDate),
            Innocent innocent => ("Innocent", innocent.VerdictDate)
        };
        await using var connection = await dataSource.OpenConnectionAsync();
        var updated = await connection.QuerySingleOrDefaultAsync<Guid?>("""
                                                                        UPDATE loan_litigations AS litigation
                                                                        SET status = @Status, verdict_date = @VerdictDate,
                                                                            verdict_by = @ActorId, verdict_recorded_at = now()
                                                                        FROM loans
                                                                        WHERE litigation.id = @Id
                                                                          AND loans.id = litigation.loan_id
                                                                          AND litigation.status = 'Pending'
                                                                          AND loans.start_date <= @VerdictDate
                                                                        RETURNING litigation.id
                                                                        """, new
        {
            Id = id.Value, Status = status, VerdictDate = verdictDate, ActorId = actorId.Value
        });
        if (updated is not null) return new VerdictRecorded();

        var existing = await connection.QuerySingleOrDefaultAsync<VerdictRow>("""
                                                                              SELECT litigation.status AS "Status",
                                                                                     litigation.verdict_date AS "VerdictDate",
                                                                                     loans.start_date AS "StartDate"
                                                                              FROM loan_litigations AS litigation
                                                                              JOIN loans ON loans.id = litigation.loan_id
                                                                              WHERE litigation.id = @Id
                                                                              """, new { Id = id.Value });
        if (existing is null) return new LitigationNotFound();
        if (existing.Status == "Pending" && verdictDate < existing.StartDate)
            return new VerdictBeforeLoanStart();
        return existing.Status == status && existing.VerdictDate == verdictDate
            ? new VerdictRecorded()
            : new LitigationConflict();
    }

    private static LitigationCase ReadCase(LitigationRow row)
    {
        var id = LitigationId.Parse(row.Id).Value as LitigationId
                 ?? throw new InvalidOperationException("Invalid litigation ID in the lending database.");
        var courtId = CourtId.Parse(row.CourtId).Value as CourtId
                      ?? throw new InvalidOperationException("Invalid court ID in the lending database.");
        if (LitigationStates.Parse(row.Status, row.VerdictDate).Value is not LitigationState state)
            throw new InvalidOperationException("Invalid litigation verdict in the lending database.");
        return new LitigationCase(id, courtId, state);
    }

    private sealed record LoanRow(string CustomerCivilId, Guid InstitutionId);

    private sealed record LitigationRow(Guid Id, string CourtId, string Status, DateOnly? VerdictDate);

    private sealed record VerdictRow(string Status, DateOnly? VerdictDate, DateOnly StartDate);
}
