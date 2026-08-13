using System;

namespace NStalling.Avro;

public static class AvroTypeResolverExtensions
{
    public static Type Resolve(this IAvroTypeResolver resolver, string schemaName, string? schemaNamespace = null, string? schemaVersion = null)
    {
        if (resolver is null) throw new ArgumentNullException(nameof(resolver));
        return resolver.Resolve(new AvroSchemaName(schemaName, schemaNamespace), schemaVersion);
    }

    public static bool TryResolve(this IAvroTypeResolver resolver, string schemaName, string? schemaNamespace, string? schemaVersion, out Type? type)
    {
        if (resolver is null) throw new ArgumentNullException(nameof(resolver));
        return resolver.TryResolve(new AvroSchemaName(schemaName, schemaNamespace), schemaVersion, out type);
    }

    public static Type? ResolveOrDefault(this IAvroTypeResolver resolver, string schemaName, string? schemaNamespace = null, string? schemaVersion = null)
    {
        if (resolver is null) throw new ArgumentNullException(nameof(resolver));
        return resolver.ResolveOrDefault(new AvroSchemaName(schemaName, schemaNamespace), schemaVersion);
    }
}

