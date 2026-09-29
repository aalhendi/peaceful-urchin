using Lending.Api.Domain;

namespace Lending.Api.Application;

internal sealed record LitigationOpened(LitigationId Id);

internal sealed record LitigationNotFound;

internal sealed record LitigationConflict;

internal sealed record VerdictBeforeLoanStart;

internal sealed record LitigationForbidden;

internal sealed record LitigationRead(LoanLitigation Loan);

internal sealed record VerdictRecorded;

internal union OpenLitigationOutcome(LitigationOpened, LitigationNotFound, LitigationConflict, LitigationForbidden);

internal union ReadLitigationOutcome(LitigationRead, LitigationNotFound, LitigationForbidden);

internal union RecordVerdictOutcome(
    VerdictRecorded, LitigationNotFound, LitigationConflict, VerdictBeforeLoanStart, LitigationForbidden);

internal interface ILitigationStore
{
    Task<OpenLitigationOutcome> OpenAsync(LoanId loanId, CourtId courtId, StaffActorId actorId);
    Task<LoanLitigation?> ReadAsync(LoanId loanId, LendingActor actor);
    Task<RecordVerdictOutcome> RecordVerdictAsync(LitigationId id, Verdict verdict, StaffActorId actorId);
}

internal sealed class LitigationService(ILitigationStore store)
{
    public async Task<OpenLitigationOutcome> OpenAsync(LendingActor actor, LoanId loanId, CourtId courtId)
    {
        if (!actor.MayWriteLitigation) return new LitigationForbidden();
        return await store.OpenAsync(loanId, courtId, actor.Id);
    }

    public async Task<ReadLitigationOutcome> ReadAsync(LendingActor actor, LoanId loanId)
    {
        if (!actor.MayReadLitigation) return new LitigationForbidden();
        var loan = await store.ReadAsync(loanId, actor);
        return loan is null ? new LitigationNotFound() : new LitigationRead(loan);
    }

    public async Task<RecordVerdictOutcome> RecordVerdictAsync(LendingActor actor, LitigationId id, Verdict verdict)
    {
        if (!actor.MayWriteLitigation) return new LitigationForbidden();
        return await store.RecordVerdictAsync(id, verdict, actor.Id);
    }
}