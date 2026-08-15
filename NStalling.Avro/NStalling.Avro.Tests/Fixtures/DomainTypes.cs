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
}
