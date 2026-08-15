using System;
using System.Collections.Generic;
using System.Linq;
using NStalling.Avro.Configuration;

namespace NStalling.Avro.Resolution
{
    /// <summary>The category of a bucket-level resolution attempt, before declared-type validation.</summary>
    internal enum AvroResolutionOutcome
    {
        Found,
        NotFound,
        Ambiguous
    }

    internal readonly record struct AvroResolutionResult(AvroResolutionOutcome Outcome, Type? Type)
    {
        public static readonly AvroResolutionResult NotFound = new(AvroResolutionOutcome.NotFound, null);
        public static readonly AvroResolutionResult Ambiguous = new(AvroResolutionOutcome.Ambiguous, null);

        public static AvroResolutionResult Resolved(Type type) => new(AvroResolutionOutcome.Found, type);
    }

    /// <summary>
    /// Immutable, concurrency-safe index of Avro full name (+ optional version) to CLR type, honoring
    /// the two-stage version-bucket resolution algorithm and source precedence within a bucket.
    /// Built once from a set of <see cref="AvroTypeMapping"/> values; deterministic configuration
    /// defects (equal-precedence duplicates for the same effective key) fail fast during construction.
    /// </summary>
    internal sealed class AvroTypeIndex
    {
        // (fullname, version) -> (source -> single resolved type)
        private readonly Dictionary<AvroTypeResolutionKey, Dictionary<AvroMappingSource, Type>> _map;

        // full names that have at least one version-qualified (non-null version) mapping
        private readonly HashSet<string> _fullNamesWithVersionedMappings;

        // full name -> set of registered CLR types (closed allowlist for value-directed polymorphism)
        private readonly HashSet<Type> _allowlist;

        private AvroTypeIndex(
            Dictionary<AvroTypeResolutionKey, Dictionary<AvroMappingSource, Type>> map,
            HashSet<string> fullNamesWithVersionedMappings,
            HashSet<Type> allowlist)
        {
            _map = map;
            _fullNamesWithVersionedMappings = fullNamesWithVersionedMappings;
            _allowlist = allowlist;
        }

        /// <summary>All CLR types known to this index; the closed allowlist for value-directed selection.</summary>
        public IReadOnlyCollection<Type> Allowlist => _allowlist;

        public bool IsAllowed(Type type) => _allowlist.Contains(type);

        public static AvroTypeIndex Build(IEnumerable<AvroTypeMapping> mappings)
        {
            if (mappings is null)
            {
                throw new ArgumentNullException(nameof(mappings));
            }

            var map = new Dictionary<AvroTypeResolutionKey, Dictionary<AvroMappingSource, Type>>();
            var versioned = new HashSet<string>(StringComparer.Ordinal);
            var allowlist = new HashSet<Type>();

            foreach (var mapping in mappings)
            {
                if (mapping.FullName is null)
                {
                    throw new AvroConfigurationException("A mapping was produced with a null Avro full name.");
                }

                if (mapping.Type is null)
                {
                    throw new AvroConfigurationException(
                        $"A mapping for '{mapping.FullName}' was produced with a null CLR type.");
                }

                allowlist.Add(mapping.Type);

                var key = new AvroTypeResolutionKey(mapping.FullName, mapping.SchemaVersion);
                if (mapping.SchemaVersion is not null)
                {
                    versioned.Add(mapping.FullName);
                }

                if (!map.TryGetValue(key, out var bySource))
                {
                    bySource = new Dictionary<AvroMappingSource, Type>();
                    map[key] = bySource;
                }

                if (bySource.TryGetValue(mapping.Source, out var existing))
                {
                    if (existing != mapping.Type)
                    {
                        throw new AvroConfigurationException(
                            $"Conflicting {mapping.Source} mappings for Avro full name '{mapping.FullName}'" +
                            (mapping.SchemaVersion is null ? string.Empty : $" version '{mapping.SchemaVersion}'") +
                            $": '{existing.FullName}' and '{mapping.Type.FullName}' claim the same effective key.");
                    }
                }
                else
                {
                    bySource[mapping.Source] = mapping.Type;
                }
            }

            return new AvroTypeIndex(map, versioned, allowlist);
        }

        /// <summary>
        /// Resolves a CLR type for a full name and optional version context using the two-stage
        /// algorithm. Declared-type validation is applied by the caller.
        /// </summary>
        public AvroResolutionResult Resolve(string fullName, string? schemaVersion)
        {
            if (fullName is null)
            {
                throw new ArgumentNullException(nameof(fullName));
            }

            // Stage 1 — select the version bucket.
            AvroTypeResolutionKey key;
            if (schemaVersion is not null)
            {
                var exactKey = new AvroTypeResolutionKey(fullName, schemaVersion);
                if (_map.ContainsKey(exactKey))
                {
                    key = exactKey;
                }
                else if (_fullNamesWithVersionedMappings.Contains(fullName))
                {
                    // Version-qualified mappings exist for this full name but not this version: no guessing.
                    return AvroResolutionResult.NotFound;
                }
                else
                {
                    key = new AvroTypeResolutionKey(fullName, null);
                }
            }
            else
            {
                key = new AvroTypeResolutionKey(fullName, null);
            }

            if (!_map.TryGetValue(key, out var bySource))
            {
                return AvroResolutionResult.NotFound;
            }

            // Stage 2 — apply source precedence within the selected bucket.
            foreach (var source in new[] { AvroMappingSource.Explicit, AvroMappingSource.DataContract, AvroMappingSource.ClrConvention })
            {
                if (bySource.TryGetValue(source, out var type))
                {
                    return AvroResolutionResult.Resolved(type);
                }
            }

            return AvroResolutionResult.NotFound;
        }
    }
}
