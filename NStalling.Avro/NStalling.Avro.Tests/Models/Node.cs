namespace NStalling.Avro.Tests.Models;

public sealed class Node
{
    public string Name { get; init; } = string.Empty;

    public Node? Parent { get; init; }
}