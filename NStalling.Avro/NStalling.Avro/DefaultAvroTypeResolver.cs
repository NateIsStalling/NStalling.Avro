using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using Avro;

namespace NStalling.Avro;

public sealed class DefaultAvroTypeResolver : IAvroTypeResolver
{
    private readonly IReadOnlyDictionary<AvroSchemaName, IReadOnlyCollection<Type>> _explicitMappings;
    private readonly IReadOnlyDictionary<VersionedSchemaName, Type> _versionedMappings;
    private readonly IReadOnlyCollection<Type> _candidates;

    public DefaultAvroTypeResolver(AvroTypeRegistry? registry = null)
    {
        registry ??= AvroTypeRegistry.Empty;

        _explicitMappings = BuildExplicitMappings(registry.Mappings);
        _versionedMappings = BuildVersionedMappings(registry.Mappings);
        _candidates = BuildCandidates(registry);
    }

    public Type Resolve(
        NamedSchema schema,
        Type? declaredType = null,
        string? schemaVersion = null)
    {
        if (!TryResolve(schema, declaredType, schemaVersion, out var type))
        {
            throw new AvroTypeResolutionException(
                $"Could not resolve CLR type for schema '{schema.Fullname}' with declared type '{declaredType?.FullName ?? "<none>"}' (version: '{schemaVersion ?? "<none>"}').");
        }

        return type;
    }

    public bool TryResolve(
        NamedSchema schema,
        Type? declaredType,
        string? schemaVersion,
        [NotNullWhen(true)] out Type? type)
    {
        try
        {
            type = ResolveOrDefault(schema, declaredType, schemaVersion);
            return type is not null;
        }
        catch
        {
            type = null;
            return false;
        }
    }

    public Type? ResolveOrDefault(
        NamedSchema schema,
        Type? declaredType = null,
        string? schemaVersion = null)
    {
        if (schema is null) throw new ArgumentNullException(nameof(schema));

        var schemaName = new AvroSchemaName(schema.Name, schema.Namespace);
        var resolvedType = ResolveSchemaName(schemaName, schemaVersion);

        if (resolvedType is null)
        {
            return null;
        }

        // Validate against declared type if provided
        if (declaredType is not null && declaredType != typeof(object))
        {
            if (!declaredType.IsAssignableFrom(resolvedType))
            {
                throw new AvroTypeResolutionException(
                    $"Resolved type '{resolvedType.FullName}' is not assignable to declared type '{declaredType.FullName}' for schema '{schema.Fullname}'.");
            }
        }

        return resolvedType;
    }

    private Type? ResolveSchemaName(AvroSchemaName schemaName, string? schemaVersion = null)
    {
        if (string.IsNullOrWhiteSpace(schemaName.Name))
        {
            throw new AvroTypeResolutionException("Schema name cannot be null, empty, or whitespace.");
        }

        // 1. Explicit version-qualified mapping (highest precedence)
        if (!string.IsNullOrWhiteSpace(schemaVersion)
            && _versionedMappings.TryGetValue(new VersionedSchemaName(schemaName, schemaVersion), out var versionedType))
        {
            return versionedType;
        }

        // 2. Explicit unqualified mapping
        if (_explicitMappings.TryGetValue(schemaName, out var explicitCandidates))
        {
            return SingleOrThrow(schemaName, explicitCandidates, "explicit mapping");
        }

        // 3. DataContract-derived mapping
        var dataContractCandidates = _candidates
            .Where(t => MatchesDataContract(t, schemaName))
            .ToArray();

        if (dataContractCandidates.Length > 0)
        {
            return SingleOrThrow(schemaName, dataContractCandidates, "DataContract");
        }

        // 4. Exact CLR fullname convention
        var fullNameCandidates = _candidates
            .Where(t => string.Equals(t.Name, schemaName.Name, StringComparison.Ordinal)
                        && string.Equals(t.Namespace ?? string.Empty, schemaName.Namespace ?? string.Empty, StringComparison.Ordinal))
            .ToArray();

        if (fullNameCandidates.Length > 0)
        {
            return SingleOrThrow(schemaName, fullNameCandidates, "CLR full-name convention");
        }

        // 5. Simple-name convention (from explicitly registered assemblies)
        var simpleNameCandidates = _candidates
            .Where(t => string.Equals(GetSimpleTypeName(t), schemaName.Name, StringComparison.Ordinal))
            .ToArray();

        if (simpleNameCandidates.Length > 0)
        {
            return SingleOrThrow(schemaName, simpleNameCandidates, "simple-name convention");
        }

        // 6. Not found
        return null;
    }

    private static bool MatchesDataContract(Type type, AvroSchemaName schemaName)
    {
        var contract = type.GetCustomAttribute<DataContractAttribute>();
        if (contract is null || string.IsNullOrWhiteSpace(contract.Name))
        {
            return false;
        }

        var contractName = new AvroSchemaName(contract.Name, contract.Namespace);
        return contractName == schemaName;
    }

    private static Type SingleOrThrow(AvroSchemaName schemaName, IEnumerable<Type> candidates, string source)
    {
        var matches = candidates.Distinct().ToArray();
        if (matches.Length == 1)
        {
            return matches[0];
        }

        var candidateList = string.Join(", ", matches.Select(t => t.FullName));
        throw new AvroTypeResolutionException(
            $"Ambiguous {source} resolution for schema '{schemaName.FullName}'. Candidates: {candidateList}");
    }

    private static IReadOnlyDictionary<AvroSchemaName, IReadOnlyCollection<Type>> BuildExplicitMappings(
        IReadOnlyCollection<AvroTypeRegistryBuilder.SchemaMapping> mappings)
    {
        var map = new Dictionary<AvroSchemaName, List<Type>>();

        foreach (var mapping in mappings.Where(x => string.IsNullOrWhiteSpace(x.SchemaVersion)))
        {
            if (!map.TryGetValue(mapping.SchemaName, out var list))
            {
                list = new List<Type>();
                map[mapping.SchemaName] = list;
            }

            list.Add(mapping.Type);
        }

        return new ReadOnlyDictionary<AvroSchemaName, IReadOnlyCollection<Type>>(
            map.ToDictionary(kvp => kvp.Key, kvp => (IReadOnlyCollection<Type>)kvp.Value.ToArray()));
    }

    private static IReadOnlyDictionary<VersionedSchemaName, Type> BuildVersionedMappings(
        IReadOnlyCollection<AvroTypeRegistryBuilder.SchemaMapping> mappings)
    {
        var map = new Dictionary<VersionedSchemaName, Type>();

        foreach (var mapping in mappings.Where(x => !string.IsNullOrWhiteSpace(x.SchemaVersion)))
        {
            var key = new VersionedSchemaName(mapping.SchemaName, mapping.SchemaVersion!);
            if (map.TryGetValue(key, out var existing) && existing != mapping.Type)
            {
                throw new AvroTypeResolutionException(
                    $"Invalid configuration: version-qualified schema '{mapping.SchemaName.FullName}' with version '{mapping.SchemaVersion}' maps to both '{existing.FullName}' and '{mapping.Type.FullName}'.");
            }

            map[key] = mapping.Type;
        }

        return new ReadOnlyDictionary<VersionedSchemaName, Type>(map);
    }

    private static IReadOnlyCollection<Type> BuildCandidates(AvroTypeRegistry registry)
    {
        var candidates = new HashSet<Type>(registry.RegisteredTypes);

        foreach (var assembly in registry.DiscoveryAssemblies)
        {
            foreach (var type in SafeGetTypes(assembly))
            {
                if (type is { IsAbstract: false, IsGenericTypeDefinition: false })
                {
                    candidates.Add(type);
                }
            }
        }

        return candidates.ToArray();
    }

    private static IEnumerable<Type> SafeGetTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(t => t is not null)!;
        }
    }

    private static string GetSimpleTypeName(Type type)
    {
        var typeName = type.Name;
        var genericTickIndex = typeName.IndexOf('`');
        return genericTickIndex >= 0 ? typeName[..genericTickIndex] : typeName;
    }

    private static string DescribeSchema(Schema schema)
    {
        return schema is NamedSchema named
            ? named.Fullname
            : schema.Tag.ToString();
    }

    private static bool TryUnwrapNullableUnion(UnionSchema unionSchema, out Schema nonNullBranch)
    {
        nonNullBranch = null!;
        var branches = unionSchema.Schemas;
        if (branches.Count != 2)
        {
            return false;
        }

        var first = branches[0];
        var second = branches[1];

        if (first.Tag == Schema.Type.Null)
        {
            nonNullBranch = second;
            return true;
        }

        if (second.Tag == Schema.Type.Null)
        {
            nonNullBranch = first;
            return true;
        }

        return false;
    }

    private readonly record struct VersionedSchemaName(AvroSchemaName Name, string Version);
}
