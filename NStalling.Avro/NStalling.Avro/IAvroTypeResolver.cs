using System;
using System.Diagnostics.CodeAnalysis;
using Avro;

namespace NStalling.Avro;

public interface IAvroTypeResolver
{
    Type Resolve(Schema schema);

    bool TryResolve(Schema schema, [NotNullWhen(true)] out Type? type);

    Type? ResolveOrDefault(Schema schema);

    Type Resolve(AvroSchemaName schemaName, string? schemaVersion = null);

    bool TryResolve(AvroSchemaName schemaName, string? schemaVersion, [NotNullWhen(true)] out Type? type);

    Type? ResolveOrDefault(AvroSchemaName schemaName, string? schemaVersion = null);
}

