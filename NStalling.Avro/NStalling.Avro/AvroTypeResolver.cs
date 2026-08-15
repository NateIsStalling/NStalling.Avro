using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Avro;

namespace NStalling.Avro
{
    /// <summary>
    /// Default <see cref="IAvroTypeResolver"/> backed by an immutable <see cref="AvroTypeIndex"/>.
    /// Immutable after construction and safe for concurrent reads.
    /// </summary>
    public sealed class AvroTypeResolver : IAvroTypeResolver
    {
        private readonly AvroTypeIndex _index;

        internal AvroTypeResolver(AvroTypeIndex index)
        {
            _index = index ?? throw new ArgumentNullException(nameof(index));
        }

        internal AvroTypeIndex Index => _index;

        /// <summary>CLR types eligible for value-directed selection (the closed allowlist).</summary>
        public IReadOnlyCollection<Type> Allowlist => _index.Allowlist;

        /// <inheritdoc />
        public Type Resolve(RecordSchema schema, Type? declaredType = null, string? schemaVersion = null)
        {
            if (schema is null)
            {
                throw new ArgumentNullException(nameof(schema));
            }

            var result = _index.Resolve(schema.Fullname, schemaVersion);
            switch (result.Outcome)
            {
                case AvroResolutionOutcome.Found:
                    var type = result.Type!;
                    if (!IsCompatible(declaredType, type))
                    {
                        throw new AvroTypeResolutionException(
                            $"Resolved CLR type '{type.FullName}' for Avro record '{schema.Fullname}' is not " +
                            $"assignable to the declared member type '{declaredType!.FullName}'.")
                        {
                            SchemaFullName = schema.Fullname,
                            SchemaVersion = schemaVersion
                        };
                    }

                    return type;

                case AvroResolutionOutcome.Ambiguous:
                    throw new AvroTypeResolutionException(
                        $"Ambiguous CLR mapping for Avro record '{schema.Fullname}'.")
                    {
                        SchemaFullName = schema.Fullname,
                        SchemaVersion = schemaVersion
                    };

                default:
                    throw new AvroTypeResolutionException(
                        $"No CLR mapping for Avro record '{schema.Fullname}'" +
                        (schemaVersion is null ? "." : $" under schema version '{schemaVersion}'."))
                    {
                        SchemaFullName = schema.Fullname,
                        SchemaVersion = schemaVersion
                    };
            }
        }

        /// <inheritdoc />
        public bool TryResolve(
            RecordSchema schema,
            Type? declaredType,
            string? schemaVersion,
            [NotNullWhen(true)] out Type? type)
        {
            if (schema is null)
            {
                throw new ArgumentNullException(nameof(schema));
            }

            type = null;
            var result = _index.Resolve(schema.Fullname, schemaVersion);
            if (result.Outcome != AvroResolutionOutcome.Found)
            {
                return false;
            }

            if (!IsCompatible(declaredType, result.Type!))
            {
                return false;
            }

            type = result.Type!;
            return true;
        }

        /// <inheritdoc />
        public Type? ResolveOrDefault(RecordSchema schema, Type? declaredType = null, string? schemaVersion = null)
        {
            if (schema is null)
            {
                throw new ArgumentNullException(nameof(schema));
            }

            var result = _index.Resolve(schema.Fullname, schemaVersion);
            switch (result.Outcome)
            {
                case AvroResolutionOutcome.Found:
                    var type = result.Type!;
                    if (!IsCompatible(declaredType, type))
                    {
                        throw new AvroTypeResolutionException(
                            $"Resolved CLR type '{type.FullName}' for Avro record '{schema.Fullname}' is not " +
                            $"assignable to the declared member type '{declaredType!.FullName}'.")
                        {
                            SchemaFullName = schema.Fullname,
                            SchemaVersion = schemaVersion
                        };
                    }

                    return type;

                case AvroResolutionOutcome.Ambiguous:
                    throw new AvroTypeResolutionException(
                        $"Ambiguous CLR mapping for Avro record '{schema.Fullname}'.")
                    {
                        SchemaFullName = schema.Fullname,
                        SchemaVersion = schemaVersion
                    };

                default:
                    return null;
            }
        }

        private static bool IsCompatible(Type? declaredType, Type resolvedType)
        {
            if (declaredType is null || declaredType == typeof(object))
            {
                return true;
            }

            return declaredType.IsAssignableFrom(resolvedType);
        }
    }
}
