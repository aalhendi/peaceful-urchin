using Lending.Api.Domain;
using Shared.Vocabulary;

namespace Lending.Api.Application;

internal interface ILendingStore
{
    Task<bool> CreateLoanAsync(Loan loan);
    Task SetLoanBlockAsync(CivilId customerId, bool blocked, StaffActorId changedBy);
}