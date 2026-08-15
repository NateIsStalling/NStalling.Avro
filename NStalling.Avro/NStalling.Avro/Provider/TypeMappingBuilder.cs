using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace NStalling.Avro.Provider
{
    /// <summary>
    /// Shared helpers for turning a CLR type into candidate <see cref="AvroTypeMapping"/> values,
    /// expanding <see cref="AvroSchemaVersionAttribute"/> qualifiers.
    /// </summary>
    internal static class TypeMappingBuilder
    {
        /// <summary>True when a type can act as an Avro record materialization target.</summary>
        public static bool IsCandidateRecordType(Type type)
            => type.IsClass && !type.IsAbstract && !typeof(System.Collections.IEnumerable).IsAssignableFrom(type)
               && type != typeof(string);

        public static IReadOnlyList<string> GetDeclaredVersions(Type type)
            => type.GetCustomAttributes<AvroSchemaVersionAttribute>(inherit: false)
                .Select(a => a.Version)
                .Distinct(StringComparer.Ordinal)
                .ToList();

        /// <summary>
        /// Emits one mapping per declared schema version, or a single unqualified mapping when the type
        /// declares no version. A type that declares versions is never placed in the unqualified bucket.
        /// </summary>
        public static IEnumerable<AvroTypeMapping> Emit(string fullName, Type type, AvroMappingSource source)
        {
            var versions = GetDeclaredVersions(type);
            if (versions.Count == 0)
            {
                yield return new AvroTypeMapping(fullName, null, type, source);
                yield break;
            }

            foreach (var version in versions)
            {
                yield return new AvroTypeMapping(fullName, version, type, source);
            }
        }
    }
}
