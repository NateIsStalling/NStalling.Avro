using System.Diagnostics.CodeAnalysis;
using Avro;

namespace NStalling.Avro;

public static class AvroSchemaResolverExtensions
{
    public static Schema Resolve<T>(this IAvroSchemaResolver resolver)
    {
        if (resolver is null) throw new ArgumentNullException(nameof(resolver));
        return resolver.Resolve(typeof(T));
    }

    public static bool TryResolve<T>(this IAvroSchemaResolver resolver, [NotNullWhen(true)] out Schema? schema)
    {
        if (resolver is null) throw new ArgumentNullException(nameof(resolver));
        return resolver.TryResolve(typeof(T), out schema);
    }

    public static Schema? ResolveOrDefault<T>(this IAvroSchemaResolver resolver)
    {
        if (resolver is null) throw new ArgumentNullException(nameof(resolver));
        return resolver.ResolveOrDefault(typeof(T));
    }
}