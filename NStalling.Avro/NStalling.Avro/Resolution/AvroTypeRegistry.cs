using System;
using System.Collections.Generic;
using System.Reflection;
using NStalling.Avro.Configuration;
using NStalling.Avro.Discovery;

namespace NStalling.Avro.Resolution
{
    /// <summary>
    /// Accumulates CLR-type mappings from explicit registration and controlled discovery, then builds
    /// an immutable <see cref="AvroTypeIndex"/>. Deterministic configuration defects are surfaced as
    /// <see cref="AvroConfigurationException"/> when the index is built.
    /// </summary>
    public sealed class AvroTypeRegistry
    {
        private readonly List<AvroTypeMapping> _mappings = new();

        /// <summary>Registers a single type, deriving its Avro identity from metadata and CLR full name.</summary>
        public AvroTypeRegistry Add<T>() => Add(typeof(T));

        /// <summary>Registers a single type, deriving its Avro identity from metadata and CLR full name.</summary>
        public AvroTypeRegistry Add(Type type)
        {
            if (type is null)
            {
                throw new ArgumentNullException(nameof(type));
            }

            foreach (var mapping in DataContractTypeProvider.GetMappings(type))
            {
                _mappings.Add(mapping);
            }

            foreach (var mapping in ClrConventionTypeProvider.GetMappings(type))
            {
                _mappings.Add(mapping);
            }

            return this;
        }

        /// <summary>Explicitly maps a CLR type to an Avro full name, optionally version-qualified.</summary>
        public AvroTypeRegistry Map<T>(string schemaName, string schemaNamespace, string? schemaVersion = null)
            => Map(typeof(T), schemaName, schemaNamespace, schemaVersion);

        /// <summary>Explicitly maps a CLR type to an Avro full name, optionally version-qualified.</summary>
        public AvroTypeRegistry Map(Type type, string schemaName, string schemaNamespace, string? schemaVersion = null)
        {
            if (type is null)
            {
                throw new ArgumentNullException(nameof(type));
            }

            if (string.IsNullOrEmpty(schemaName))
            {
                throw new ArgumentException("Schema name must be provided.", nameof(schemaName));
            }

            if (!TypeMappingBuilder.IsCandidateRecordType(type))
            {
                throw new AvroConfigurationException(
                    $"Type '{type.FullName}' cannot be mapped to an Avro record; it must be a concrete, " +
                    "non-collection class.");
            }

            var fullName = string.IsNullOrEmpty(schemaNamespace) ? schemaName : schemaNamespace + "." + schemaName;
            _mappings.Add(new AvroTypeMapping(fullName, schemaVersion, type, AvroMappingSource.Explicit));
            return this;
        }

        /// <summary>Adds every candidate type in the assembly containing <typeparamref name="T"/>.</summary>
        public AvroTypeRegistry FromAssemblyContaining<T>() => FromAssembly(typeof(T).Assembly);

        /// <summary>Adds every candidate type in the specified assembly.</summary>
        public AvroTypeRegistry FromAssembly(Assembly assembly)
        {
            if (assembly is null)
            {
                throw new ArgumentNullException(nameof(assembly));
            }

            foreach (var mapping in AssemblyTypeProvider.GetMappings(assembly))
            {
                _mappings.Add(mapping);
            }

            return this;
        }

        internal IReadOnlyList<AvroTypeMapping> Mappings => _mappings;

        /// <summary>Builds the immutable index, failing fast on deterministic configuration defects.</summary>
        internal AvroTypeIndex BuildIndex() => AvroTypeIndex.Build(_mappings);

        /// <summary>Builds an immutable resolver from the accumulated mappings.</summary>
        public AvroTypeResolver BuildResolver() => new AvroTypeResolver(BuildIndex());
    }
}
