namespace Shared.Vocabulary;

public sealed record ParseError(string Message);

public union ParseResult<T>(T, ParseError) where T : notnull;