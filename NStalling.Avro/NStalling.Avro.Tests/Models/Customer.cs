namespace NStalling.Avro.Tests.Models;

public sealed class CustomerV1
{
    public string Id { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;
}

public sealed class CustomerVersion1
{
    public string Id { get; init; } = string.Empty;
}

public sealed class CustomerVersion2
{
    public string Id { get; init; } = string.Empty;

    public string? Email { get; init; }
}