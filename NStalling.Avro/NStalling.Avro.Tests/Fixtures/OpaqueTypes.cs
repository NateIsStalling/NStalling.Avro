using System.Diagnostics.CodeAnalysis;
using System.Runtime.Serialization;
using Avro;
using NStalling.Avro;

namespace NStalling.Avro.Tests.Fixtures
{
    // Reader-side opaque envelope: the payload arrives as opaque bytes and is materialized in a second pass.
    [DataContract(Name = "OpaqueEnvelope", Namespace = "Acme.Events")]
    public sealed class OpaqueEnvelope
    {
        public string EventType { get; set; } = "";

        public string? SchemaVersion { get; set; }

        public object Payload { get; set; } = null!;
    }

    // Writer-side opaque envelope: the payload is a raw byte buffer written to the schema's `bytes` field.
    // Intentionally carries no [DataContract] so it does not collide with OpaqueEnvelope on assembly scans;
    // the write path binds it to the schema by concrete type, not by discovered Avro identity.
    public sealed class OpaqueEnvelopeWire
    {
        public string EventType { get; set; } = "";

        public string? SchemaVersion { get; set; }

        public byte[] Payload { get; set; } = System.Array.Empty<byte>();
    }

    /// <summary>Test payload schema source mapping a discriminator value to a fixed writer schema.</summary>
    internal sealed class MapPayloadSchemaSource : IAvroPayloadSchemaSource
    {
        private readonly System.Collections.Generic.Dictionary<string, Schema> _map;

        public MapPayloadSchemaSource(System.Collections.Generic.Dictionary<string, Schema> map) => _map = map;

        public bool TryGetWriterSchema(AvroPayloadSchemaContext context, [NotNullWhen(true)] out Schema? schema)
        {
            schema = null;
            return context.TypeDiscriminator is { } key && _map.TryGetValue(key, out schema);
        }
    }

    /// <summary>Test payload schema source that always throws (infrastructure failure).</summary>
    internal sealed class ThrowingPayloadSchemaSource : IAvroPayloadSchemaSource
    {
        public bool TryGetWriterSchema(AvroPayloadSchemaContext context, [NotNullWhen(true)] out Schema? schema)
            => throw new System.InvalidOperationException("registry unavailable");
    }
}
