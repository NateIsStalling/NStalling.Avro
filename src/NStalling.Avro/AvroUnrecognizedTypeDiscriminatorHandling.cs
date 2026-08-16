namespace NStalling.Avro
{
    /// <summary>
    /// Governs how the value-directed path recovers when it cannot materialize the opaque payload into a
    /// resolver-mapped CLR type. Two distinct shortfalls are covered, and which options are meaningful
    /// differs between them:
    /// <list type="bullet">
    /// <item><description>
    /// <b>Unrecognized type identity</b> — the type discriminator is missing, null, or reported as an
    /// ordinary not-found by the payload schema source, so no inner writer schema is available. The bytes
    /// cannot be decoded, so only <see cref="Fail"/> and <see cref="PreservePayload"/> apply here.
    /// </description></item>
    /// <item><description>
    /// <b>Identified schema without a CLR mapping</b> — an inner writer schema was resolved (so the bytes
    /// are decodable) but the resolver maps no CLR type to it. <see cref="UseFallbackType"/> applies here,
    /// decoding into the configured fallback type instead of failing.
    /// </description></item>
    /// </list>
    /// It does not govern missing version context, infrastructure failures, or inner-decode failures.
    /// </summary>
    public enum AvroUnrecognizedTypeDiscriminatorHandling
    {
        /// <summary>No explicit choice; resolved through configuration precedence to the library default.</summary>
        Unspecified = 0,

        /// <summary>Throw <see cref="AvroTypeResolutionException"/> (library default).</summary>
        Fail = 1,

        /// <summary>
        /// Retain the already-decoded raw payload representation when assignable to the target member.
        /// Applies to the unrecognized-type-identity path, where no inner writer schema is available.
        /// </summary>
        PreservePayload = 2,

        /// <summary>
        /// Decode the payload into an explicitly configured fallback CLR type from the closed allowlist
        /// when an inner writer schema was identified but the resolver maps no CLR type to it. Because it
        /// requires a decodable writer schema, it is inapplicable when type identity itself is
        /// unrecognized, where it behaves like <see cref="Fail"/>.
        /// </summary>
        UseFallbackType = 3
    }
}
