namespace NStalling.Avro.Tests.Messages;

/// <summary>
/// Duplicate of the CustomerId class in the Models namespace, used for testing ambiguous type resolution.
/// </summary>
public sealed class CustomerId
{
    public string Name { get; init; } = string.Empty;
}