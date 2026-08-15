using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using NStalling.Avro.Resolution;

namespace NStalling.Avro.Discovery
{
    /// <summary>
    /// Derives a candidate Avro full name from <see cref="DataContractAttribute"/>
    /// (<c>Namespace + "." + Name</c>). Standard .NET metadata is used only as a source of Avro schema
    /// identity; no <c>DataContractSerializer</c> semantics are implied.
    /// </summary>
    internal static class DataContractTypeProvider
    {
        public static IEnumerable<AvroTypeMapping> GetMappings(Type type)
        {
            var dataContract = type.GetCustomAttribute<DataContractAttribute>(inherit: false);
            if (dataContract is null)
            {
                yield break;
            }

            if (!TypeMappingBuilder.IsCandidateRecordType(type))
            {
                yield break;
            }

            var name = string.IsNullOrEmpty(dataContract.Name) ? type.Name : dataContract.Name!;
            var ns = dataContract.Namespace;
            var fullName = string.IsNullOrEmpty(ns) ? name : ns + "." + name;

            foreach (var mapping in TypeMappingBuilder.Emit(fullName, type, AvroMappingSource.DataContract))
            {
                yield return mapping;
            }
        }
    }
}
