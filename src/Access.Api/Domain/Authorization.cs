using System.Collections.Immutable;
using Shared.Vocabulary;

namespace Access.Api.Domain;

internal sealed record InstitutionKind
{
    public static readonly InstitutionKind Bank = new("Bank");
    public static readonly InstitutionKind Cinet = new("CINET");

    private InstitutionKind(string value) => Value = value;

    public string Value { get; }

    public static ParseResult<InstitutionKind> Parse(string? value) => value switch
    {
        "Bank" => Bank,
        "CINET" => Cinet,
        _ => new ParseError("Institution kind must be Bank or CINET.")
    };
}

internal sealed record StaffRole
{
    public static readonly StaffRole CustomerReader = new("CustomerReader", "Customer.Read");
    public static readonly StaffRole CustomerWriter = new("CustomerWriter", "Customer.Write");
    public static readonly StaffRole LitigationReader = new("LitigationReader", "Litigation.Read");
    public static readonly StaffRole LitigationWriter = new("LitigationWriter", "Litigation.Write");
    public static readonly StaffRole LoanCreator = new("LoanCreator", "Loan.Create");
    public static readonly StaffRole LoanBlocker = new("LoanBlocker", "Loan.Block");
    public static readonly StaffRole PaymentWriter = new("PaymentWriter", "Payment.Write");
    public static readonly StaffRole CreditAnalyst = new("CreditAnalyst", "Credit.Read");
    public static readonly StaffRole AccessAdmin = new("AccessAdmin", "StaffRoles.Change");

    private StaffRole(string code, string permission)
    {
        Code = code;
        Permission = permission;
    }

    public string Code { get; }
    public string Permission { get; }

    public bool IsAllowedFor(InstitutionKind kind)
    {
        if (this == LoanCreator || this == PaymentWriter) return kind == InstitutionKind.Bank;
        if (this == CreditAnalyst || this == AccessAdmin || this == LoanBlocker || this == LitigationWriter)
            return kind == InstitutionKind.Cinet;
        return true;
    }

    public static ParseResult<StaffRole> Parse(string? value) => value switch
    {
        "CustomerReader" => CustomerReader,
        "CustomerWriter" => CustomerWriter,
        "LitigationReader" => LitigationReader,
        "LitigationWriter" => LitigationWriter,
        "LoanCreator" => LoanCreator,
        "LoanBlocker" => LoanBlocker,
        "PaymentWriter" => PaymentWriter,
        "CreditAnalyst" => CreditAnalyst,
        "AccessAdmin" => AccessAdmin,
        _ => new ParseError("Unknown staff role.")
    };
}

internal sealed record StaffActor(
    StaffUserId Id,
    InstitutionId InstitutionId,
    InstitutionKind InstitutionKind,
    ImmutableArray<StaffRole> Roles)
{
    public bool HasRole(StaffRole role) => Roles.Contains(role);
}