using Avro;

namespace NStalling.Avro;

/// <summary>
///     Extension methods for IAvroTypeResolver.
/// </summary>
public static class AvroTypeResolverExtensions
{
    /// <summary>
    ///     Resolves a named schema (as Schema) to a CLR type by extracting the NamedSchema.
    /// </summary>
    public static Type Resolve(this IAvroTypeResolver resolver, Schema schema, Type? declaredType = null,
        string? schemaVersion = null)
    {
        if (resolver is null) throw new ArgumentNullException(nameof(resolver));
        if (schema is not NamedSchema namedSchema)
            throw new ArgumentException($"Schema must be a NamedSchema. Got {schema?.Tag}", nameof(schema));
        return resolver.Resolve(namedSchema, declaredType, schemaVersion);
    }

    /// <summary>
    ///     Attempts to resolve a named schema (as Schema) to a CLR type.
    /// </summary>
    public static bool TryResolve(this IAvroTypeResolver resolver, Schema schema, Type? declaredType,
        string? schemaVersion, out Type? type)
    {
        if (resolver is null) throw new ArgumentNullException(nameof(resolver));
        if (schema is not NamedSchema namedSchema)
        {
            type = null;
            return false;
        }

        return resolver.TryResolve(namedSchema, declaredType, schemaVersion, out type);
    }

    /// <summary>
    ///     Resolves a named schema (as Schema) to a CLR type or null.
    /// </summary>
    public static Type? ResolveOrDefault(this IAvroTypeResolver resolver, Schema schema, Type? declaredType = null,
        string? schemaVersion = null)
    {
        if (resolver is null) throw new ArgumentNullException(nameof(resolver));
        if (schema is not NamedSchema namedSchema) return null;
        return resolver.ResolveOrDefault(namedSchema, declaredType, schemaVersion);
    }
}