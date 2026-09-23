# 0001: Use .NET 11 RC1 for typed business outcomes

**Date:** 2026-09-23

**Status:** Accepted

## Context

We want Lending to return a closed set of business outcomes, as we might write in Rust:

```rust
enum CreateLoanOutcome {
    Created(LoanId),
    CustomerBlocked,
}
```

[C# 15 unions](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/union) bring a similar closed set to C# (illustrative types):

```csharp
public sealed record LoanCreated(LoanId Id);
public sealed record CustomerBlocked;
public union CreateLoanOutcome(LoanCreated, CustomerBlocked);
```

Rust rejects an incomplete `match`; C# warns on an incomplete `switch`, and we will treat warnings as errors. With .NET 10, we would model the cases manually or add a union library.

## Decision

Target `net11.0` with `LangVersion=preview`, using the [.NET 11 RC1 release](https://dotnet.microsoft.com/en-us/download/dotnet/11.0). Pin matching SDK and ASP.NET runtime container images.

## Consequences

We get compiler-checked outcomes without a union dependency. We accept pre-GA tooling risk for this demo.
