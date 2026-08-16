using System.Runtime.Serialization;
using NStalling.Avro;

namespace NStalling.Avro.Tests.Fixtures
{
    // ---- Schema-directed union / object / interface fixtures ----

    public interface IEvent
    {
    }

    public abstract class EventBase : IEvent
    {
    }

    [DataContract(Name = "CustomerCreated", Namespace = "Acme.Events")]
    public sealed class CustomerCreated : EventBase
    {
        public string CustomerId { get; set; } = "";
    }

    [DataContract(Name = "OrderPlaced", Namespace = "Acme.Events")]
    public sealed class OrderPlaced : EventBase
    {
        public string OrderId { get; set; } = "";
    }

    // Allowlisted fallback event used to exercise UseFallbackType on a CLR-mapping failure: it carries a
    // CustomerId property so a CustomerCreated payload (writer schema known) can decode into it even though
    // the resolver maps no CLR type to that record.
    [DataContract(Name = "FallbackEvent", Namespace = "Acme.Events")]
    public sealed class FallbackEvent : EventBase
    {
        public string CustomerId { get; set; } = "";
    }

    public sealed class EnvelopeObject
    {
        public string EventId { get; set; } = "";
        public object Payload { get; set; } = null!;
    }

    public sealed class EnvelopeInterface
    {
        public string EventId { get; set; } = "";
        public IEvent Payload { get; set; } = null!;
    }

    public sealed class EnvelopeAbstract
    {
        public string EventId { get; set; } = "";
        public EventBase Payload { get; set; } = null!;
    }

    // ---- Version-qualified fixtures ----

    public interface ICustomer
    {
    }

    [DataContract(Name = "Customer", Namespace = "Acme.Events")]
    [AvroSchemaVersion("1")]
    public sealed class LegacyCustomer : ICustomer
    {
        public string Name { get; set; } = "";
    }

    [DataContract(Name = "Customer", Namespace = "Acme.Events")]
    [AvroSchemaVersion("2")]
    public sealed class CurrentCustomer : ICustomer
    {
        public string Name { get; set; } = "";
    }

    // A CLR type whose name implies a version but must carry no version semantics.
    [DataContract(Name = "Widget", Namespace = "Acme.Events")]
    public sealed class WidgetV2
    {
        public string Sku { get; set; } = "";
    }

    // Multiple versions on one CLR type.
    [DataContract(Name = "Product", Namespace = "Acme.Events")]
    [AvroSchemaVersion("2")]
    [AvroSchemaVersion("3")]
    public sealed class Product
    {
        public string Sku { get; set; } = "";
    }

    // ---- Nested schema-directed version-inheritance fixtures ----
    // Read-side envelopes whose nested Customer member(s) are version-qualified (Legacy=1, Current=2) and
    // therefore only resolvable when the root version is inherited into the nested resolution. Members are
    // typed as object because Apache's reflect reader decodes a bare (non-union) record/array/map only into
    // a class-typed member; the resolved concrete type is what actually lands in the object slot.

    public sealed class VersionedRecordEnvelope
    {
        public string EventId { get; set; } = "";

        public object Customer { get; set; } = null!;
    }

    public sealed class VersionedArrayEnvelope
    {
        public string EventId { get; set; } = "";

        public System.Collections.Generic.List<object> Customers { get; set; } = new();
    }

    public sealed class VersionedMapEnvelope
    {
        public string EventId { get; set; } = "";

        public System.Collections.Generic.Dictionary<string, object> Customers { get; set; } = new();
    }

    // Concrete write-side counterparts: the nested member is a concrete CLR type, so the writer binds it by
    // type without consulting the resolver (mirrors OpaqueEnvelopeWire).

    public sealed class VersionedRecordEnvelopeWire
    {
        public string EventId { get; set; } = "";

        public LegacyCustomer Customer { get; set; } = new();
    }

    public sealed class VersionedArrayEnvelopeWire
    {
        public string EventId { get; set; } = "";

        public System.Collections.Generic.List<LegacyCustomer> Customers { get; set; } = new();
    }

    public sealed class VersionedMapEnvelopeWire
    {
        public string EventId { get; set; } = "";

        public System.Collections.Generic.Dictionary<string, LegacyCustomer> Customers { get; set; } = new();
    }
}
