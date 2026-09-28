using System.Collections.Immutable;
using Shared.Vocabulary;

namespace Lending.Api.Domain;

internal sealed record InstallmentDraft(DateOnly DueDate, decimal AmountKwd);

internal sealed record ScheduledInstallment(DateOnly DueDate, PositiveKwdAmount Amount);

internal sealed record RepaymentSchedule
{
    private RepaymentSchedule(ImmutableArray<ScheduledInstallment> installments) => Installments = installments;

    public ImmutableArray<ScheduledInstallment> Installments { get; }

    public static ParseResult<RepaymentSchedule> Parse(
        DateOnly startDate, LoanTenor tenor, IReadOnlyList<InstallmentDraft>? drafts)
    {
        if (drafts is null || drafts.Count != tenor.Months)
            return new ParseError("Provide one installment for each tenor month.");
        if (startDate > DateOnly.MaxValue.AddMonths(-tenor.Months))
            return new ParseError("Repayment schedule exceeds the supported date range.");

        var installments = ImmutableArray.CreateBuilder<ScheduledInstallment>(drafts.Count);
        for (var index = 0; index < drafts.Count; index++)
        {
            var expectedMonth = startDate.AddMonths(index + 1);
            var draft = drafts[index];
            if (draft.DueDate.Year != expectedMonth.Year || draft.DueDate.Month != expectedMonth.Month)
                return new ParseError("Installments must fall in consecutive months after the loan starts.");
            if (PositiveKwdAmount.Parse(draft.AmountKwd).Value is not PositiveKwdAmount amount)
                return new ParseError("Each installment must have a positive KWD amount.");
            installments.Add(new ScheduledInstallment(draft.DueDate, amount));
        }

        // NOTE(aalhendi): The institution supplies amounts. Lending does not infer its financing formula from the rate.
        // TODO(aalhendi): Who owns products? Banks? What about Financing Formulas? Do we even care? Or do simply care about payment schedule?
        //  Check this. Makes sense that CINET/credit beaureu doesn't care about "how" but cares about "how much + when" (the schedule)
        return new RepaymentSchedule(installments.MoveToImmutable());
    }
}