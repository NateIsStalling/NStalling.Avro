using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Avro;
using Avro.Reflect;

namespace NStalling.Avro.Provider
{
    /// <summary>
    /// Owns the Apache-specific plumbing that translates NStalling.Avro type resolution into a populated
    /// <see cref="ClassCache"/> for Apache's reflection reader/writer. The produced cache is scoped to a
    /// single resolution context (writer schema + root type + version); because Apache keys its cache by
    /// record full name only, callers must not share a cache across contexts that map the same full name
    /// to different CLR types.
    /// </summary>
    internal sealed class ApacheReflectionAdapter
    {
        private static readonly MethodInfo AddClassNameMapItemMethod =
            typeof(ClassCache).GetMethod(
                "AddClassNameMapItem",
                BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null,
                types: new[] { typeof(RecordSchema), typeof(Type) },
                modifiers: null)
            ?? throw new InvalidOperationException(
                "Apache.Avro ClassCache.AddClassNameMapItem(RecordSchema, Type) was not found; " +
                "the referenced Apache.Avro version is incompatible with NStalling.Avro.");

        private readonly IAvroTypeResolver _resolver;

        static ApacheReflectionAdapter()
        {
            // Apache's reflect reader rejects an `object`-typed member over an Avro `bytes` field. Register
            // a process-wide passthrough converter so opaque payload members declared as `object` decode to
            // a raw byte[] in the first pass; the value-directed engine performs the second-pass decode.
            ClassCache.AddDefaultConverter<byte[], object>(
                (bytes, _) => bytes,
                (value, _) => (byte[])value);
        }

        public ApacheReflectionAdapter(IAvroTypeResolver resolver)
        {
            _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
        }

        /// <summary>
        /// Builds a class cache mapping every reachable record schema to a concrete CLR type, resolving
        /// members that Apache cannot infer (object/interface/abstract members and union record branches)
        /// through the configured resolver.
        /// </summary>
        public ClassCache BuildClassCache(Schema writerSchema, Type rootType, string? rootVersion)
        {
            if (writerSchema is null)
            {
                throw new ArgumentNullException(nameof(writerSchema));
            }

            if (rootType is null)
            {
                throw new ArgumentNullException(nameof(rootType));
            }

            var cache = new ClassCache();
            var collected = new Dictionary<string, (RecordSchema Schema, Type Type)>(StringComparer.Ordinal);

            Collect(writerSchema, rootType, rootVersion, isRoot: true, collected);

            // Pass 1: register every record -> CLR type mapping directly, independent of graph order.
            foreach (var entry in collected.Values)
            {
                RegisterRecord(cache, entry.Schema, entry.Type);
            }

            // Pass 2: let Apache populate its auxiliary caches (enums, arrays, maps, fixed) for each
            // concrete type. All record branches are already registered, so union validation passes.
            foreach (var entry in collected.Values)
            {
                try
                {
                    cache.LoadClassCache(entry.Type, entry.Schema);
                }
                catch (AvroException)
                {
                    // Best-effort auxiliary population; core record mappings are already established.
                }
            }

            return cache;
        }

        private void Collect(
            Schema schema,
            Type? declaredType,
            string? version,
            bool isRoot,
            Dictionary<string, (RecordSchema Schema, Type Type)> collected)
        {
            switch (schema)
            {
                case RecordSchema record:
                    if (collected.ContainsKey(record.Fullname))
                    {
                        return;
                    }

                    var clrType = DetermineRecordType(record, declaredType, version, isRoot);
                    collected[record.Fullname] = (record, clrType);

                    foreach (var field in record.Fields)
                    {
                        var propertyType = GetPropertyType(clrType, field.Name);
                        // Nested records are unqualified by default; the higher polymorphism layer may
                        // supply nested version context explicitly.
                        Collect(field.Schema, propertyType, null, isRoot: false, collected);
                    }

                    break;

                case UnionSchema union:
                    foreach (var branch in union.Schemas)
                    {
                        if (branch.Tag == Schema.Type.Null)
                        {
                            continue;
                        }

                        Collect(branch, declaredType, version, isRoot: false, collected);
                    }

                    break;

                case ArraySchema array:
                    Collect(array.ItemSchema, GetElementType(declaredType), null, isRoot: false, collected);
                    break;

                case MapSchema map:
                    Collect(map.ValueSchema, GetMapValueType(declaredType), null, isRoot: false, collected);
                    break;
            }
        }

        private Type DetermineRecordType(RecordSchema record, Type? declaredType, string? version, bool isRoot)
        {
            // A concrete, instantiable member type is what Apache would infer; honor it directly.
            if (declaredType is not null && IsConcreteRecordType(declaredType))
            {
                return declaredType;
            }

            // Otherwise NStalling.Avro must supply the concrete type (object/interface/abstract/union branch,
            // or a root whose declared type is not directly instantiable).
            return _resolver.Resolve(record, declaredType, version);
        }

        private static void RegisterRecord(ClassCache cache, RecordSchema schema, Type type)
        {
            AddClassNameMapItemMethod.Invoke(cache, new object[] { schema, type });
        }

        private static bool IsConcreteRecordType(Type type)
            => type.IsClass && !type.IsAbstract && type != typeof(object) && type != typeof(string)
               && !typeof(IEnumerable).IsAssignableFrom(type);

        private static Type? GetPropertyType(Type declaringType, string fieldName)
        {
            var property = declaringType.GetProperty(fieldName);
            return property?.PropertyType;
        }

        private static Type? GetElementType(Type? collectionType)
        {
            if (collectionType is null)
            {
                return null;
            }

            if (collectionType.IsArray)
            {
                return collectionType.GetElementType();
            }

            if (collectionType.IsGenericType && collectionType.GenericTypeArguments.Length == 1)
            {
                return collectionType.GenericTypeArguments[0];
            }

            return null;
        }

        private static Type? GetMapValueType(Type? mapType)
        {
            if (mapType is null || !mapType.IsGenericType || mapType.GenericTypeArguments.Length != 2)
            {
                return null;
            }

            return mapType.GenericTypeArguments[1];
        }
    }
}
