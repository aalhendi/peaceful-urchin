using System.Text.Json;
using Xunit;

namespace Shared.Vocabulary.Tests;

public sealed class VocabularyTests
{
    [Fact]
    public void CivilIdPreservesAWellFormedNumberWithoutClaimingIssuance()
    {
        var civilId = Assert.IsType<CivilId>(CivilId.Parse("180010100006").Value);

        Assert.Equal("180010100006", civilId.ExposeSecret());
        Assert.Equal(civilId, Assert.IsType<CivilId>(CivilId.Parse(civilId.ExposeSecret()).Value));
        Assert.IsType<CivilId>(CivilId.Parse("304022900002").Value);
    }

    [Fact]
    public void CivilIdRequiresExplicitExposure()
    {
        var civilId = Assert.IsType<CivilId>(CivilId.Parse("180010100006").Value);

        Assert.Equal("CivilId(**redacted**)", civilId.ToString());
        Assert.Throws<NotSupportedException>(() => JsonSerializer.Serialize(civilId));
        Assert.Throws<NotSupportedException>(() => JsonSerializer.Serialize<SensitiveString>(civilId));
        Assert.Throws<NotSupportedException>(() => JsonSerializer.Serialize(new { CivilId = civilId }));
        Assert.Equal("\"180010100006\"", JsonSerializer.Serialize(civilId.ExposeSecret()));
    }

    [Fact]
    public void CivilIdReportsParseErrorsWithoutEchoingTheInput()
    {
        Assert.Equal("Civil ID is required.", Assert.IsType<ParseError>(CivilId.Parse(null).Value).Message);
        Assert.Equal("Civil ID must have 12 digits.",
            Assert.IsType<ParseError>(CivilId.Parse("12345678901").Value).Message);
        Assert.IsType<ParseError>(CivilId.Parse("1234567890123").Value);
        Assert.Equal("Civil ID must contain only ASCII digits.",
            Assert.IsType<ParseError>(CivilId.Parse(" 12345678901").Value).Message);
        Assert.IsType<ParseError>(CivilId.Parse("١٢٣٤٥٦٧٨٩٠١٢").Value);
        Assert.Equal("Civil ID contains an invalid birth date.",
            Assert.IsType<ParseError>(CivilId.Parse("200023000018").Value).Message);
        Assert.IsType<ParseError>(CivilId.Parse("303022900008").Value);
        Assert.IsType<ParseError>(CivilId.Parse("400010100008").Value);
        Assert.Equal("Civil ID has an invalid checksum.",
            Assert.IsType<ParseError>(CivilId.Parse("180010100007").Value).Message);
    }

    [Fact]
    public void InstitutionIdentityIsAUuidButNeverTheEmptyUuid()
    {
        var institutionId = InstitutionId.New();

        Assert.NotEqual(Guid.Empty, institutionId.Value);
        Assert.Equal(institutionId, Assert.IsType<InstitutionId>(InstitutionId.Parse(institutionId.Value).Value));
        Assert.Equal("Institution ID cannot be empty.",
            Assert.IsType<ParseError>(InstitutionId.Parse(Guid.Empty).Value).Message);
    }

    [Fact]
    public void InstitutionCodesHaveOneCanonicalShape()
    {
        var code = Assert.IsType<InstitutionCode>(InstitutionCode.Parse("BANK_A").Value);

        Assert.Equal("BANK_A", code.Value);
        Assert.IsType<InstitutionCode>(InstitutionCode.Parse(new string('A', 32)).Value);
        Assert.IsType<ParseError>(InstitutionCode.Parse(new string('A', 33)).Value);
        Assert.IsType<ParseError>(InstitutionCode.Parse("").Value);
        Assert.IsType<ParseError>(InstitutionCode.Parse("bank_a").Value);
        Assert.IsType<ParseError>(InstitutionCode.Parse("BANK-A").Value);
        Assert.IsType<ParseError>(InstitutionCode.Parse("BANK_١").Value);
        Assert.IsType<ParseError>(InstitutionCode.Parse("ВANK_A").Value);
    }

    [Fact]
    public void KwdAmountKeepsExactFilsWithoutImposingALoanLimit()
    {
        var amount = Assert.IsType<KwdAmount>(KwdAmount.Parse(10.001m).Value);

        Assert.Equal(10.001m, amount.Dinars);
        Assert.IsType<KwdAmount>(KwdAmount.Parse(-0.001m).Value);
        Assert.IsType<KwdAmount>(KwdAmount.Parse(0m).Value);
        Assert.Equal("KWD amount cannot have more than three decimal places.",
            Assert.IsType<ParseError>(KwdAmount.Parse(0.0001m).Value).Message);
    }

    [Fact]
    public void FinancingRateParsesPercentPointsWithoutAProductCap()
    {
        var rate = Assert.IsType<FinancingRate>(FinancingRate.Parse(7.5m).Value);

        Assert.Equal(7.5m, rate.PercentPoints);
        Assert.Equal(0.075m, rate.AsFraction());
        Assert.IsType<FinancingRate>(FinancingRate.Parse(0m).Value);
        Assert.IsType<FinancingRate>(FinancingRate.Parse(125m).Value);
        Assert.Equal("Financing rate cannot be negative.",
            Assert.IsType<ParseError>(FinancingRate.Parse(-0.01m).Value).Message);
    }
}