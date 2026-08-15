using System.Reflection;

namespace NStalling.Avro;

public sealed class AvroTypeRegistryBuilder
{
    private readonly HashSet<Assembly> _assemblies = new();
    private readonly List<SchemaMapping> _mappings = new();
    private readonly HashSet<Type> _types = new();

    public AvroTypeRegistryBuilder FromAssembly(Assembly assembly)
    {
        _assemblies.Add(assembly ?? throw new ArgumentNullException(nameof(assembly)));
        return this;
    }

    public AvroTypeRegistryBuilder FromAssemblyContaining<T>()
    {
        return FromAssembly(typeof(T).Assembly);
    }

    public AvroTypeRegistryBuilder Add<T>()
    {
        return Add(typeof(T));
    }

    public AvroTypeRegistryBuilder Add(Type type)
    {
        _types.Add(type ?? throw new ArgumentNullException(nameof(type)));
        return this;
    }

    public AvroTypeRegistryBuilder Map<T>(AvroSchemaName schemaName, string? schemaVersion = null)
    {
        return Map(typeof(T), schemaName, schemaVersion);
    }

    public AvroTypeRegistryBuilder Map(Type type, AvroSchemaName schemaName, string? schemaVersion = null)
    {
        if (type is null) throw new ArgumentNullException(nameof(type));
        _types.Add(type);
        _mappings.Add(new SchemaMapping(type, schemaName, schemaVersion));
        return this;
    }

    public AvroTypeRegistry Build()
    {
        return new AvroTypeRegistry(
            _assemblies.ToArray(),
            _types.ToArray(),
            _mappings.ToArray());
    }

    public static AvroTypeRegistryBuilder CreateDefault()
    {
        return new AvroTypeRegistryBuilder();
    }

    public sealed record SchemaMapping(Type Type, AvroSchemaName SchemaName, string? SchemaVersion);
}

public sealed class AvroTypeRegistry
{
    public AvroTypeRegistry(
        IReadOnlyCollection<Assembly> discoveryAssemblies,
        IReadOnlyCollection<Type> registeredTypes,
        IReadOnlyCollection<AvroTypeRegistryBuilder.SchemaMapping> mappings)
    {
        DiscoveryAssemblies = discoveryAssemblies;
        RegisteredTypes = registeredTypes;
        Mappings = mappings;
    }

    public IReadOnlyCollection<Assembly> DiscoveryAssemblies { get; }

    public IReadOnlyCollection<Type> RegisteredTypes { get; }

    public IReadOnlyCollection<AvroTypeRegistryBuilder.SchemaMapping> Mappings { get; }

    public static AvroTypeRegistry Empty { get; } = new(
        Array.Empty<Assembly>(),
        Array.Empty<Type>(),
        Array.Empty<AvroTypeRegistryBuilder.SchemaMapping>());
}