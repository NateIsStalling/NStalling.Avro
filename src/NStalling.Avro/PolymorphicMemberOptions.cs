using System;
using System.Reflection;

namespace NStalling.Avro
{
    /// <summary>Mutable per-member polymorphism options collected from fluent configuration.</summary>
    internal sealed class PolymorphicMemberOptions
    {
        public PolymorphicMemberOptions(PropertyInfo member)
        {
            Member = member;
        }

        public PropertyInfo Member { get; }

        public string? TypeDiscriminatorPath { get; set; }

        public string? VersionDiscriminatorPath { get; set; }

        public IAvroPayloadSchemaSource? PayloadSchemaSource { get; set; }

        public AvroUnrecognizedTypeDiscriminatorHandling Handling { get; set; }
            = AvroUnrecognizedTypeDiscriminatorHandling.Unspecified;

        public Type? FallbackType { get; set; }

        public bool InheritVersion { get; set; }
    }
}
