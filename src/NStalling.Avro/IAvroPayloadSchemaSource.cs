using System.Diagnostics.CodeAnalysis;
using Avro;

namespace NStalling.Avro
{
    /// <summary>
    /// Caller-supplied source of the inner Avro writer schema for the value-directed path. It must
    /// distinguish an ordinary "not found / unrecognized discriminator" (return <see langword="false"/>)
    /// from an infrastructure/contract failure (throw, surfaced as
    /// <see cref="Serialization.AvroPayloadSchemaException"/>). It may return a record schema or a union
    /// whose record branch Apache will select; NStalling.Avro still resolves CLR types only for record schemas.
    /// </summary>
    public interface IAvroPayloadSchemaSource
    {
        /// <summary>
        /// Attempts to obtain the inner writer schema for the supplied context. Returns
        /// <see langword="false"/> for an ordinary not-found; throws for infrastructure failures.
        /// </summary>
        bool TryGetWriterSchema(AvroPayloadSchemaContext context, [NotNullWhen(true)] out Schema? schema);
    }
}
