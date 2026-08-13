using System;

namespace NStalling.Avro;

public readonly record struct AvroSchemaName(string Name, string? Namespace = null)
{
    public string FullName => string.IsNullOrWhiteSpace(Namespace) ? Name : $"{Namespace}.{Name}";
}

