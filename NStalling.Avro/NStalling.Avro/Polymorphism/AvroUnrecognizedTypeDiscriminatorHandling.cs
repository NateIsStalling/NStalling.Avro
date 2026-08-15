namespace NStalling.Avro.Polymorphism
{
    /// <summary>
    /// Governs behavior when the value-directed path cannot identify an inner payload schema / type
    /// identity because the type discriminator is missing, null, or reported as an ordinary not-found
    /// by the payload schema source. It does not govern missing version context, infrastructure
    /// failures, CLR-mapping failures, or inner-decode failures.
    /// </summary>
    public enum AvroUnrecognizedTypeDiscriminatorHandling
    {
        /// <summary>No explicit choice; resolved through configuration precedence to the library default.</summary>
        Unspecified = 0,

        /// <summary>Throw <see cref="Resolution.AvroTypeResolutionException"/> (library default).</summary>
        Fail = 1,

        /// <summary>Retain the already-decoded raw payload representation when assignable to the target member.</summary>
        PreservePayload = 2,

        /// <summary>Use an explicitly configured fallback CLR type from the closed allowlist.</summary>
        UseFallbackType = 3
    }
}
