using Shared.Vocabulary;

namespace Lending.Api.Domain;

internal sealed record GradeA;

internal sealed record GradeB;

internal sealed record GradeC;

internal sealed record GradeF;

internal sealed record PendingReview;

internal union CreditGradeDecision(GradeA, GradeB, GradeC, GradeF, PendingReview);

internal sealed record GradeLoan(
    LoanId Id,
    DateOnly StartDate,
    LoanStatus Status,
    LoanPrincipal Principal,
    KwdAmount OverdueAmount,
    IReadOnlyList<LitigationState> Cases);

internal static class CreditGradePolicy
{
    public static CreditGradeDecision Evaluate(IReadOnlyList<GradeLoan> loans, DateOnly today)
    {
        var openLoans = 0;
        var delinquentLoans = 0;
        var pendingCase = false;
        var recentGuiltyVerdict = false;

        foreach (var loan in loans)
        {
            if (loan.StartDate > today) continue;

            if (loan.Status == LoanStatus.Open)
            {
                openLoans++;
                if (loan.OverdueAmount.Dinars > 0) delinquentLoans++;
            }

            foreach (var state in loan.Cases)
            {
                var (isPending, isRecentGuilty) = state switch
                {
                    Pending => (true, false),
                    Guilty guilty => (false, IsRecent(guilty, loan.Principal, today)),
                    Innocent => (false, false)
                };
                pendingCase |= isPending;
                recentGuiltyVerdict |= isRecentGuilty;
            }
        }

        if (delinquentLoans > 3 || recentGuiltyVerdict) return new GradeF();
        if (pendingCase) return new PendingReview();
        if (delinquentLoans > 0) return new GradeC();
        return openLoans > 0 ? new GradeA() : new GradeB();
    }

    private static bool IsRecent(Guilty guilty, LoanPrincipal principal, DateOnly today)
    {
        var years = principal.Amount.Dinars > 10_000m ? 3 : 1;
        return guilty.VerdictDate <= today && guilty.VerdictDate.AddYears(years) > today;
    }
}