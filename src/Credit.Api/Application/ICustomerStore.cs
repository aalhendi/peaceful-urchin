using Credit.Api.Domain;
using Shared.Vocabulary;

namespace Credit.Api.Application;

internal interface ICustomerStore
{
    Task<Customer?> FindAsync(CivilId customerId);
}