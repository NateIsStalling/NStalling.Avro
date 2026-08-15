
namespace NStalling.Avro
{
    /// <summary>Global resolution behavior that is independent of the type registry.</summary>
    public sealed class AvroResolutionOptions
    {
        /// <summary>
        /// When enabled, nested/member resolutions inherit the parent/root schema version unless a more
        /// specific member version source applies. Disabled by default: nested records are unqualified.
        /// </summary>
        public bool InheritSchemaVersionEnabled { get; private set; }

        /// <summary>Enables parent/root version inheritance for nested resolutions.</summary>
        public AvroResolutionOptions InheritSchemaVersion()
        {
            InheritSchemaVersionEnabled = true;
            return this;
        }
    }

    /// <summary>Global polymorphism defaults applied when neither fluent nor attribute config specifies one.</summary>
    public sealed class AvroPolymorphismDefaults
    {
        /// <summary>The global default unrecognized-type-discriminator handling. Library default is <c>Fail</c>.</summary>
        public AvroUnrecognizedTypeDiscriminatorHandling DefaultHandling { get; private set; }
            = AvroUnrecognizedTypeDiscriminatorHandling.Fail;

        /// <summary>Sets the global default unrecognized-type-discriminator handling.</summary>
        public AvroPolymorphismDefaults UnrecognizedTypeDiscriminatorHandling(
            AvroUnrecognizedTypeDiscriminatorHandling handling)
        {
            DefaultHandling = handling == AvroUnrecognizedTypeDiscriminatorHandling.Unspecified
                ? AvroUnrecognizedTypeDiscriminatorHandling.Fail
                : handling;
            return this;
        }
    }
}
