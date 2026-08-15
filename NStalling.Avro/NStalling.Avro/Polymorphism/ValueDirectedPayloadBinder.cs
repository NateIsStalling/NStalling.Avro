using System;
using Avro;
using NStalling.Avro.Resolution;
using NStalling.Avro.Serialization;

namespace NStalling.Avro.Polymorphism
{
    /// <summary>
    /// Executes the value-directed second-pass state machine for a single polymorphic member against a
    /// fully decoded outer object. Discriminators are read from the completed outer datum, so behavior is
    /// independent of field order. The unrecognized-type-discriminator policy governs only failure to
    /// identify type/schema identity; payload-schema, CLR-resolution, and inner-decode failures are typed
    /// separately and are never diverted by that policy.
    /// </summary>
    internal sealed class ValueDirectedPayloadBinder
    {
        private readonly IAvroTypeResolver _resolver;
        private readonly AvroSerializer _serializer;

        public ValueDirectedPayloadBinder(IAvroTypeResolver resolver, AvroSerializer serializer)
        {
            _resolver = resolver;
            _serializer = serializer;
        }

        public void Bind(object outer, PolymorphicMemberBinding binding, string? rootVersion)
        {
            var path = outer.GetType().Name + "." + binding.Member.Name;
            var rawValue = binding.Member.GetValue(outer);

            // Null payload requires no type discriminator.
            if (rawValue is null)
            {
                return;
            }

            // Stage 1 — obtain polymorphism context from the completed outer object.
            string? typeDiscriminator = null;
            binding.TypeDiscriminator?.TryRead(outer, out typeDiscriminator);

            string? versionDiscriminator = null;
            var versionPresent = binding.VersionDiscriminator is not null
                                  && binding.VersionDiscriminator.TryRead(outer, out versionDiscriminator);
            var effectiveVersion = versionPresent
                ? versionDiscriminator
                : (binding.InheritVersion ? rootVersion : null);

            var payloadBytes = rawValue as byte[];

            // Stage 2 — acquire the inner payload writer schema.
            Schema? innerSchema = null;
            var identified = false;
            if (!string.IsNullOrEmpty(typeDiscriminator) && binding.PayloadSchemaSource is not null)
            {
                var context = new AvroPayloadSchemaContext(
                    typeDiscriminator,
                    versionDiscriminator,
                    outer.GetType(),
                    binding.Member.Name,
                    binding.DeclaredMemberType);

                try
                {
                    identified = binding.PayloadSchemaSource.TryGetWriterSchema(context, out innerSchema);
                }
                catch (Exception ex) when (ex is not AvroSerializationException and not OperationCanceledException)
                {
                    throw new AvroPayloadSchemaException(
                        $"The payload schema source failed while resolving '{path}'.", ex)
                    {
                        Path = path,
                        DiscriminatorPath = binding.TypeDiscriminator?.Path,
                        DiscriminatorValue = typeDiscriminator,
                        SchemaVersion = effectiveVersion
                    };
                }
            }

            if (!identified || innerSchema is null)
            {
                ApplyUnrecognizedTypeIdentity(binding, path, typeDiscriminator, effectiveVersion);
                return;
            }

            // Stage 3 — resolve the CLR type for the identified schema.
            var (targetType, isUnionRoot) = ResolveTargetType(binding, innerSchema, effectiveVersion, path, typeDiscriminator);

            // Stage 4 — decode the isolated payload buffer.
            if (payloadBytes is null)
            {
                throw new AvroPayloadDecodeException(
                    $"Member '{path}' is configured for value-directed decoding but its decoded value is not an opaque byte buffer.")
                {
                    Path = path,
                    SchemaVersion = effectiveVersion
                };
            }

            var materialized = _serializer.DecodeInnerPayload(payloadBytes, innerSchema, targetType, effectiveVersion, path);

            if (!binding.DeclaredMemberType.IsInstanceOfType(materialized))
            {
                throw new AvroTypeResolutionException(
                    $"Materialized payload of type '{materialized.GetType().FullName}' for '{path}' is not assignable to " +
                    $"the declared member type '{binding.DeclaredMemberType.FullName}'.")
                {
                    Path = path,
                    SchemaVersion = effectiveVersion
                };
            }

            binding.Member.SetValue(outer, materialized);
            _ = isUnionRoot;
        }

        private (Type Type, bool IsUnionRoot) ResolveTargetType(
            PolymorphicMemberBinding binding,
            Schema innerSchema,
            string? effectiveVersion,
            string path,
            string? typeDiscriminator)
        {
            switch (innerSchema)
            {
                case RecordSchema record:
                    if (_resolver.TryResolve(record, binding.DeclaredMemberType, effectiveVersion, out var resolved))
                    {
                        return (resolved, false);
                    }

                    if (binding.Handling == AvroUnrecognizedTypeDiscriminatorHandling.UseFallbackType
                        && binding.FallbackType is not null)
                    {
                        return (binding.FallbackType, false);
                    }

                    // Known schema, no CLR mapping: a resolution failure, never policy-diverted.
                    throw new AvroTypeResolutionException(
                        $"No CLR mapping for identified payload record '{record.Fullname}' at '{path}'.")
                    {
                        Path = path,
                        SchemaFullName = record.Fullname,
                        SchemaVersion = effectiveVersion,
                        DiscriminatorPath = binding.TypeDiscriminator?.Path,
                        DiscriminatorValue = typeDiscriminator
                    };

                case UnionSchema:
                    // Let Apache select the record branch during decode; resolve CLR per branch via the cache.
                    return (typeof(object), true);

                default:
                    throw new AvroTypeResolutionException(
                        $"Identified payload schema for '{path}' is not a record or union of records.")
                    {
                        Path = path,
                        SchemaVersion = effectiveVersion
                    };
            }
        }

        private static void ApplyUnrecognizedTypeIdentity(
            PolymorphicMemberBinding binding,
            string path,
            string? typeDiscriminator,
            string? effectiveVersion)
        {
            switch (binding.Handling)
            {
                case AvroUnrecognizedTypeDiscriminatorHandling.PreservePayload:
                    // The raw payload is already assigned to the member; retain it.
                    return;

                case AvroUnrecognizedTypeDiscriminatorHandling.UseFallbackType:
                    // No inner writer schema was obtainable; a fallback type cannot fabricate one.
                    throw new AvroTypeResolutionException(
                        $"Type identity for '{path}' could not be recognized and no payload writer schema was " +
                        "available, so the configured fallback type cannot be used.")
                    {
                        Path = path,
                        DiscriminatorPath = binding.TypeDiscriminator?.Path,
                        DiscriminatorValue = typeDiscriminator,
                        SchemaVersion = effectiveVersion
                    };

                default:
                    throw new AvroTypeResolutionException(
                        $"Could not recognize the payload type identity for '{path}'.")
                    {
                        Path = path,
                        DiscriminatorPath = binding.TypeDiscriminator?.Path,
                        DiscriminatorValue = typeDiscriminator,
                        SchemaVersion = effectiveVersion
                    };
            }
        }
    }
}
