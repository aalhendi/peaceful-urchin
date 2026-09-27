using System.Text.Json.Serialization;

namespace Shared.Vocabulary;

public abstract record SensitiveString : IJsonOnSerializing
{
    private readonly string _value;

    protected SensitiveString(string value) => _value = value ?? throw new ArgumentNullException(nameof(value));

    public string ExposeSecret() => _value;

    public sealed override string ToString() => $"{GetType().Name}(**redacted**)";

    void IJsonOnSerializing.OnSerializing() =>
        throw new NotSupportedException($"{GetType().Name} must be exposed explicitly before JSON serialization.");
}