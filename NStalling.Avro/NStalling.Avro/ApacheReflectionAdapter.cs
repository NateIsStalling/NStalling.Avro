using System.Diagnostics;
using Avro;
using Avro.Reflect;

namespace NStalling.Avro;

/// <summary>
///     Integrates NStalling.Avro type resolution with Apache.Avro's reflection infrastructure.
///     Responsible for populating Apache's ClassCache and other reflection-related data structures
///     based on resolved NamedSchema → CLR Type mappings.
/// </summary>
public sealed class ApacheReflectionAdapter
{
    private readonly IAvroTypeResolver _typeResolver;

    public ApacheReflectionAdapter(IAvroTypeResolver typeResolver)
        : this(typeResolver, new ClassCache())
    {
    }

    public ApacheReflectionAdapter(IAvroTypeResolver typeResolver, ClassCache classCache)
    {
        _typeResolver = typeResolver ?? throw new ArgumentNullException(nameof(typeResolver));
        ClassCache = classCache ?? throw new ArgumentNullException(nameof(classCache));
    }

    /// <summary>
    ///     Gets the underlying ClassCache for use with Apache reflection readers/writers.
    /// </summary>
    public ClassCache ClassCache { get; }

    /// <summary>
    ///     Prepares Apache reflection infrastructure for the given schema by resolving
    ///     and registering all named schemas reachable from it.
    ///     This ensures that Apache's reflection machinery can find the appropriate CLR types
    ///     for all named schemas in the graph before deserialization begins.
    /// </summary>
    public void PrepareSchema(Schema schema, Type? declaredType = null, string? schemaVersion = null)
    {
        if (schema is null) throw new ArgumentNullException(nameof(schema));

        // For typed flows, declaredType is authoritative for the root. If not supplied,
        // try resolver-based root lookup.
        if (schema is NamedSchema rootNamedSchema)
        {
            var rootType = declaredType ?? _typeResolver.ResolveOrDefault(rootNamedSchema, null, schemaVersion);
            if (rootType is not null) RegisterWithClassCache(rootNamedSchema, rootType);
        }

        // Traverse and resolve all nested named schemas
        foreach (var namedSchema in SchemaGraphWalker.EnumerateNamedSchemas(schema))
        {
            // Skip the root if it was already processed
            if (schema is NamedSchema rootNamed && namedSchema.Fullname == rootNamed.Fullname)
                continue;

            var resolvedType = _typeResolver.ResolveOrDefault(namedSchema, null, schemaVersion);
            if (resolvedType is not null) RegisterWithClassCache(namedSchema, resolvedType);
        }
    }

    /// <summary>
    ///     Registers a resolved NamedSchema → CLR Type mapping with Apache's ClassCache.
    ///     This allows Apache's reflection reader/writer to materialize the concrete CLR type
    ///     when deserializing/serializing the named schema.
    /// </summary>
    private void RegisterWithClassCache(NamedSchema namedSchema, Type clrType)
    {
        try
        {
            ClassCache.LoadClassCache(clrType, namedSchema);
        }
        catch (Exception ex)
        {
            // ClassCache integration is best-effort; if it fails, continue
            // Apache will attempt to find the type through its normal mechanisms
            Debug.WriteLine($"ClassCache registration failed for {namedSchema.Fullname}: {ex.Message}");
        }
    }
}