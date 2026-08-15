namespace NStalling.Avro.Tests.Models;

/// <summary>
/// Duplicate of the CustomerId class in the Messages namespace, used for testing ambiguous type resolution.
/// </summary>
public sealed class CustomerId
{
    public string Id { get; init; } = string.Empty;
}