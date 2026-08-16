using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;

namespace NStalling.Avro.Provider
{
    /// <summary>
    /// Enumerates the candidate types of an explicitly configured assembly scope exactly once and
    /// produces <see cref="DataContractAttribute"/>-derived and CLR-full-name mappings for each. The
    /// AppDomain is never scanned and subclasses are never auto-discovered across arbitrary assemblies.
    /// </summary>
    internal static class AssemblyTypeProvider
    {
        public static IEnumerable<AvroTypeMapping> GetMappings(Assembly assembly)
        {
            if (assembly is null)
            {
                throw new ArgumentNullException(nameof(assembly));
            }

            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                types = Array.FindAll(ex.Types, t => t is not null)!;
            }

            foreach (var type in types)
            {
                if (!TypeMappingBuilder.IsCandidateRecordType(type))
                {
                    continue;
                }

                foreach (var mapping in DataContractTypeProvider.GetMappings(type))
                {
                    yield return mapping;
                }

                foreach (var mapping in ClrConventionTypeProvider.GetMappings(type))
                {
                    yield return mapping;
                }
            }
        }
    }
}
