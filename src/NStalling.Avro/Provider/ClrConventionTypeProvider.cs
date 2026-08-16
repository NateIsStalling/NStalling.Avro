using System;
using System.Collections.Generic;

namespace NStalling.Avro.Provider
{
    /// <summary>
    /// Derives a candidate Avro full name from the exact CLR full name of a type. Simple-name matching
    /// is never used and schema versions are never inferred from CLR naming patterns such as
    /// <c>V1</c>/<c>V2</c> suffixes.
    /// </summary>
    internal static class ClrConventionTypeProvider
    {
        public static IEnumerable<AvroTypeMapping> GetMappings(Type type)
        {
            if (!TypeMappingBuilder.IsCandidateRecordType(type))
            {
                yield break;
            }

            var fullName = type.FullName;
            if (fullName is null || fullName.IndexOf('+') >= 0)
            {
                // Skip nested/constructed types whose CLR full name cannot be an Avro full name.
                yield break;
            }

            foreach (var mapping in TypeMappingBuilder.Emit(fullName, type, AvroMappingSource.ClrConvention))
            {
                yield return mapping;
            }
        }
    }
}
