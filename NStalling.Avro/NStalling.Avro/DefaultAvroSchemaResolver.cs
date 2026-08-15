using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text.Json;
using Avro;

namespace NStalling.Avro;

public sealed class DefaultAvroSchemaResolver : IAvroSchemaResolver
{
    private readonly ConcurrentDictionary<Type, Schema> _cache = new();
    private readonly AvroUnionConfiguration _unionConfiguration;

    public DefaultAvroSchemaResolver() : this(AvroUnionConfiguration.Empty)
    {
    }

    public DefaultAvroSchemaResolver(AvroUnionConfiguration unionConfiguration)
    {
        _unionConfiguration = unionConfiguration ?? throw new ArgumentNullException(nameof(unionConfiguration));
    }

    public Schema Resolve(Type type)
    {
        if (type is null) throw new ArgumentNullException(nameof(type));
        return _cache.GetOrAdd(type, t => GenerateSchema(t, _unionConfiguration));
    }

    public bool TryResolve(Type type, [NotNullWhen(true)] out Schema? schema)
    {
        try
        {
            schema = Resolve(type);
            return true;
        }
        catch
        {
            schema = null;
            return false;
        }
    }

    public Schema? ResolveOrDefault(Type type)
    {
        return TryResolve(type, out var schema) ? schema : null;
    }

    private static Schema GenerateSchema(Type type, AvroUnionConfiguration unionConfiguration)
    {
        var generator = new AvroSchemaJsonGenerator(unionConfiguration);
        var schemaModel = generator.BuildFor(type, null, true);
        var avsc = JsonSerializer.Serialize(schemaModel);
        return Schema.Parse(avsc);
    }

    private sealed class AvroSchemaJsonGenerator
    {
        private readonly HashSet<Type> _constructing = new();
        private readonly Dictionary<Type, AvroSchemaName> _namedSchemaNames = new();
        private readonly AvroUnionConfiguration _unionConfiguration;

        public AvroSchemaJsonGenerator(AvroUnionConfiguration unionConfiguration)
        {
            _unionConfiguration = unionConfiguration;
        }

        public object BuildFor(Type type, MemberInfo? memberInfo, bool isRoot)
        {
            var normalizedType = Nullable.GetUnderlyingType(type) ?? type;
            var allowNull = ShouldBeNullable(type, memberInfo);
            var baseSchema = BuildCore(normalizedType, isRoot);

            if (!allowNull) return baseSchema;

            return new List<object> { "null", baseSchema };
        }

        private object BuildCore(Type type, bool isRoot)
        {
            if (TryMapPrimitive(type, out var primitiveSchema)) return primitiveSchema;

            if (type.IsEnum) return BuildEnumSchema(type);

            if (TryMapDictionary(type, out var dictionaryValueType))
                return new Dictionary<string, object?>
                {
                    ["type"] = "map",
                    ["values"] = BuildFor(dictionaryValueType, null, false)
                };

            if (TryMapArray(type, out var itemType))
                return new Dictionary<string, object?>
                {
                    ["type"] = "array",
                    ["items"] = BuildFor(itemType, null, false)
                };

            return BuildRecordSchema(type, isRoot);
        }

        private object BuildRecordSchema(Type type, bool isRoot)
        {
            var schemaName = GetSchemaName(type);

            if (_namedSchemaNames.ContainsKey(type) && !isRoot) return schemaName.FullName;

            if (!_namedSchemaNames.ContainsKey(type)) _namedSchemaNames[type] = schemaName;

            if (_constructing.Contains(type)) return schemaName.FullName;

            _constructing.Add(type);

            var record = new Dictionary<string, object?>
            {
                ["type"] = "record",
                ["name"] = schemaName.Name
            };

            if (!string.IsNullOrWhiteSpace(schemaName.Namespace)) record["namespace"] = schemaName.Namespace;

            var fields = new List<object>();
            foreach (var property in GetSerializableProperties(type))
            {
                var memberName = property.GetCustomAttribute<DataMemberAttribute>()?.Name;
                var fieldName = string.IsNullOrWhiteSpace(memberName) ? property.Name : memberName;

                fields.Add(new Dictionary<string, object?>
                {
                    ["name"] = fieldName,
                    ["type"] = BuildForProperty(type, property)
                });
            }

            record["fields"] = fields;

            _constructing.Remove(type);
            return record;
        }

        private object BuildForProperty(Type declaringType, PropertyInfo property)
        {
            if (_unionConfiguration.TryGetMemberUnion(declaringType, property.Name, out var memberUnion))
                return BuildConfiguredUnion(memberUnion, property);

            var normalizedType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
            if (_unionConfiguration.TryGetTypeUnion(normalizedType, out var typeUnion))
                return BuildConfiguredUnion(typeUnion, property);

            return BuildFor(property.PropertyType, property, false);
        }

        private object BuildConfiguredUnion(IReadOnlyList<Type> branches, PropertyInfo property)
        {
            if (branches.Count == 0)
                throw new AvroTypeResolutionException(
                    $"Configured union for '{property.DeclaringType?.FullName}.{property.Name}' has no branches.");

            var normalized = NormalizeConfiguredUnion(branches, property);
            if (normalized.Count == 1) return normalized[0];

            return normalized;
        }

        private List<object> NormalizeConfiguredUnion(IReadOnlyList<Type> branches, PropertyInfo property)
        {
            var output = new List<object>();

            foreach (var branchType in branches)
            {
                var branchSchema = BuildFor(branchType, null, false);
                FlattenUnion(branchSchema, output);
            }

            if (ShouldBeNullable(property.PropertyType, property)
                && !output.Any(IsNullBranch))
                output.Insert(0, "null");

            ValidateUnionBranches(output, property);
            return output;
        }

        private static void ValidateUnionBranches(IReadOnlyList<object> branches, PropertyInfo property)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var branch in branches)
            {
                var key = GetBranchKey(branch);
                if (!seen.Add(key))
                    throw new AvroTypeResolutionException(
                        $"Invalid union for '{property.DeclaringType?.FullName}.{property.Name}': duplicate Avro branch '{key}'.");
            }
        }

        private static string GetBranchKey(object branch)
        {
            if (branch is string primitiveOrNamedReference) return primitiveOrNamedReference;

            if (branch is Dictionary<string, object?> branchObject)
            {
                if (!branchObject.TryGetValue("type", out var typeNode) || typeNode is not string typeName)
                    throw new AvroTypeResolutionException("Union branch object is missing required 'type'.");

                if (typeName is "array" or "map") return typeName;

                if (typeName is "record" or "enum" or "fixed") return $"named:{GetFullName(branchObject)}";

                return typeName;
            }

            throw new AvroTypeResolutionException(
                $"Unsupported union branch model type '{branch.GetType().FullName}'.");
        }

        private static string GetFullName(Dictionary<string, object?> branchObject)
        {
            if (!branchObject.TryGetValue("name", out var nameNode)
                || nameNode is not string name
                || string.IsNullOrWhiteSpace(name))
                throw new AvroTypeResolutionException("Named union branch is missing required 'name'.");

            if (!branchObject.TryGetValue("namespace", out var namespaceNode)
                || namespaceNode is not string schemaNamespace
                || string.IsNullOrWhiteSpace(schemaNamespace))
                return name;

            return $"{schemaNamespace}.{name}";
        }

        private static bool IsNullBranch(object branch)
        {
            return branch is string s && string.Equals(s, "null", StringComparison.Ordinal);
        }

        private static void FlattenUnion(object branch, ICollection<object> destination)
        {
            if (branch is List<object> nestedUnion)
            {
                foreach (var nestedBranch in nestedUnion) FlattenUnion(nestedBranch, destination);

                return;
            }

            destination.Add(branch);
        }

        private static IEnumerable<PropertyInfo> GetSerializableProperties(Type type)
        {
            return type
                .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(p => p.CanRead && p.GetMethod?.IsPublic == true && p.GetIndexParameters().Length == 0)
                .OrderBy(p => p.GetCustomAttribute<DataMemberAttribute>()?.Order ?? int.MaxValue)
                .ThenBy(p => p.Name, StringComparer.Ordinal);
        }

        private static AvroSchemaName GetSchemaName(Type type)
        {
            var dataContract = type.GetCustomAttribute<DataContractAttribute>();
            if (dataContract is not null && !string.IsNullOrWhiteSpace(dataContract.Name))
                return new AvroSchemaName(dataContract.Name!, dataContract.Namespace);

            var name = type.Name;
            var genericTickIndex = name.IndexOf('`');
            if (genericTickIndex >= 0) name = name[..genericTickIndex];

            return new AvroSchemaName(name, type.Namespace);
        }

        private static bool TryMapPrimitive(Type type, out object schema)
        {
            if (type == typeof(bool))
            {
                schema = "boolean";
                return true;
            }

            if (type == typeof(int))
            {
                schema = "int";
                return true;
            }

            if (type == typeof(long))
            {
                schema = "long";
                return true;
            }

            if (type == typeof(float))
            {
                schema = "float";
                return true;
            }

            if (type == typeof(double))
            {
                schema = "double";
                return true;
            }

            if (type == typeof(string))
            {
                schema = "string";
                return true;
            }

            if (type == typeof(byte[]))
            {
                schema = "bytes";
                return true;
            }

            if (type == typeof(Guid))
            {
                schema = new Dictionary<string, object?>
                {
                    ["type"] = "string",
                    ["logicalType"] = "uuid"
                };
                return true;
            }

            schema = null!;
            return false;
        }

        private static object BuildEnumSchema(Type type)
        {
            var schemaName = GetSchemaName(type);
            return new Dictionary<string, object?>
            {
                ["type"] = "enum",
                ["name"] = schemaName.Name,
                ["namespace"] = schemaName.Namespace,
                ["symbols"] = Enum.GetNames(type)
            };
        }

        private static bool TryMapArray(Type type, out Type itemType)
        {
            if (type.IsArray)
            {
                itemType = type.GetElementType()!;
                return true;
            }

            if (type == typeof(string) || type == typeof(byte[]))
            {
                itemType = null!;
                return false;
            }

            var enumerable = type
                .GetInterfaces()
                .Concat(new[] { type })
                .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));

            if (enumerable is null)
            {
                itemType = null!;
                return false;
            }

            itemType = enumerable.GetGenericArguments()[0];
            return true;
        }

        private static bool TryMapDictionary(Type type, out Type valueType)
        {
            var dictionary = type
                .GetInterfaces()
                .Concat(new[] { type })
                .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IDictionary<,>));

            if (dictionary is null)
            {
                valueType = null!;
                return false;
            }

            var args = dictionary.GetGenericArguments();
            if (args[0] != typeof(string))
                throw new AvroTypeResolutionException(
                    $"Avro map keys must be strings. Type '{type.FullName}' uses key type '{args[0].FullName}'.");

            valueType = args[1];
            return true;
        }

        private static bool ShouldBeNullable(Type type, MemberInfo? memberInfo)
        {
            if (Nullable.GetUnderlyingType(type) is not null) return true;

            if (type.IsValueType) return false;

            return memberInfo is not null && IsNullableReference(memberInfo);
        }

        private static bool IsNullableReference(MemberInfo memberInfo)
        {
            var memberNullable = memberInfo.CustomAttributes
                .FirstOrDefault(a => a.AttributeType.FullName == "System.Runtime.CompilerServices.NullableAttribute");

            if (memberNullable is not null
                && memberNullable.ConstructorArguments.Count > 0)
            {
                var firstArgument = memberNullable.ConstructorArguments[0];

                if (firstArgument.ArgumentType == typeof(byte)) return (byte)firstArgument.Value! == 2;

                if (firstArgument.Value is IReadOnlyCollection<CustomAttributeTypedArgument> args
                    && args.Count > 0)
                    return (byte)args.First().Value! == 2;
            }

            var nullableContext = memberInfo.DeclaringType?.CustomAttributes
                .FirstOrDefault(a =>
                    a.AttributeType.FullName == "System.Runtime.CompilerServices.NullableContextAttribute");

            if (nullableContext is null || nullableContext.ConstructorArguments.Count == 0) return false;

            return (byte)nullableContext.ConstructorArguments[0].Value! == 2;
        }
    }
}