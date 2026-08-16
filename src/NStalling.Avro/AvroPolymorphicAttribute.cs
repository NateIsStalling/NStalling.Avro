using System;

namespace NStalling.Avro
{
    /// <summary>
    /// Model-local declarative configuration for a polymorphic member. Discriminator values may be
    /// simple member names or nested paths (e.g. <c>Metadata.EventType</c>). An explicit path here
    /// takes precedence over marker-attribute discovery.
    /// </summary>
    [AttributeUsage(
        AttributeTargets.Property,
        AllowMultiple = false,
        Inherited = true)]
    public sealed class AvroPolymorphicAttribute : Attribute
    {
        /// <summary>Path to the member that supplies type/schema identity for the value-directed path.</summary>
        public string? TypeDiscriminator { get; set; }

        /// <summary>Path to the member that supplies runtime schema-version context.</summary>
        public string? VersionDiscriminator { get; set; }

        /// <summary>Behavior when type identity cannot be recognized. Defaults to <see cref="AvroUnrecognizedTypeDiscriminatorHandling.Unspecified"/>.</summary>
        public AvroUnrecognizedTypeDiscriminatorHandling UnrecognizedTypeDiscriminatorHandling { get; set; }
            = AvroUnrecognizedTypeDiscriminatorHandling.Unspecified;

        /// <summary>Fallback CLR type used only with <see cref="AvroUnrecognizedTypeDiscriminatorHandling.UseFallbackType"/>.</summary>
        public Type? FallbackType { get; set; }
    }
}
