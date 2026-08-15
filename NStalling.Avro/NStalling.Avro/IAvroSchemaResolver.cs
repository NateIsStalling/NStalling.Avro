using System.Diagnostics.CodeAnalysis;
using Avro;

namespace NStalling.Avro;

public interface IAvroSchemaResolver
{
    Schema Resolve(Type type);

    bool TryResolve(Type type, [NotNullWhen(true)] out Schema? schema);

    Schema? ResolveOrDefault(Type type);
}