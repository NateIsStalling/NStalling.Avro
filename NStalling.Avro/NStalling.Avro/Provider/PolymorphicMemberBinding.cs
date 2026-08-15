using System;
using System.Reflection;

namespace NStalling.Avro.Provider
{
    /// <summary>
    /// A fully resolved, validated per-member polymorphism binding used by the value-directed engine.
    /// Produced from attributes and/or fluent configuration during configuration building so that
    /// structurally invalid paths and invalid option combinations fail fast.
    /// </summary>
    internal sealed class PolymorphicMemberBinding
    {
        public PolymorphicMemberBinding(
            PropertyInfo member,
            DiscriminatorLocator? typeDiscriminator,
            DiscriminatorLocator? versionDiscriminator,
            IAvroPayloadSchemaSource? payloadSchemaSource,
            AvroUnrecognizedTypeDiscriminatorHandling handling,
            Type? fallbackType,
            bool inheritVersion)
        {
            Member = member;
            TypeDiscriminator = typeDiscriminator;
            VersionDiscriminator = versionDiscriminator;
            PayloadSchemaSource = payloadSchemaSource;
            Handling = handling;
            FallbackType = fallbackType;
            InheritVersion = inheritVersion;
        }

        public PropertyInfo Member { get; }

        public Type DeclaredMemberType => Member.PropertyType;

        public DiscriminatorLocator? TypeDiscriminator { get; }

        public DiscriminatorLocator? VersionDiscriminator { get; }

        public IAvroPayloadSchemaSource? PayloadSchemaSource { get; }

        public AvroUnrecognizedTypeDiscriminatorHandling Handling { get; }

        public Type? FallbackType { get; }

        public bool InheritVersion { get; }
    }
}
