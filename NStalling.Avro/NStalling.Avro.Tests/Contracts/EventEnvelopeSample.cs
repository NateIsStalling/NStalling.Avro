using System.Runtime.Serialization;
using Avro;

namespace NStalling.Avro.Tests;

public static class EventEnvelopeSample
{
    public static readonly string UnionEnvelopeSchemaJson = """
                                                            {
                                                              "type": "record",
                                                              "name": "EventEnvelope",
                                                              "namespace": "Demo",
                                                              "fields": [
                                                                {"name": "EventId", "type": "string"},
                                                                {"name": "EventTimestamp", "type": "long"},
                                                                {
                                                                  "name": "Payload",
                                                                  "type": [
                                                                    {
                                                                      "type": "record",
                                                                      "name": "CustomerCreated",
                                                                      "fields": [
                                                                        {"name": "CustomerId", "type": "string"},
                                                                        {"name": "Name", "type": "string"}
                                                                      ]
                                                                    },
                                                                    {
                                                                      "type": "record",
                                                                      "name": "OrderPlaced",
                                                                      "fields": [
                                                                        {"name": "OrderId", "type": "string"},
                                                                        {"name": "CustomerId", "type": "string"},
                                                                        {"name": "Amount", "type": "double"}
                                                                      ]
                                                                    }
                                                                  ]
                                                                }
                                                              ]
                                                            }
                                                            """;

    public static readonly string OrderEnvelopeSchemaJson = """
                                                            {
                                                              "type": "record",
                                                              "name": "EventEnvelope",
                                                              "namespace": "Demo",
                                                              "fields": [
                                                                {"name": "EventId", "type": "string"},
                                                                {"name": "EventTimestamp", "type": "long"},
                                                                {
                                                                  "name": "Payload",
                                                                  "type": {
                                                                    "type": "record",
                                                                    "name": "OrderPlaced",
                                                                    "fields": [
                                                                      {"name": "OrderId", "type": "string"},
                                                                      {"name": "CustomerId", "type": "string"},
                                                                      {"name": "Amount", "type": "double"}
                                                                    ]
                                                                  }
                                                                }
                                                              ]
                                                            }
                                                            """;

    public static Schema ParseUnionEnvelopeSchema()
    {
        return Schema.Parse(UnionEnvelopeSchemaJson);
    }

    public static Schema ParseOrderEnvelopeSchema()
    {
        return Schema.Parse(OrderEnvelopeSchemaJson);
    }

    public static string DescribePayload(object payload)
    {
        return payload switch
        {
            CustomerCreated customer => $"Customer created: {customer.Name}",
            OrderPlaced order => $"Order placed: ${order.Amount}",
            _ => "Unknown event"
        };
    }

    [DataContract(Name = "EventEnvelope", Namespace = "Demo")]
    public sealed class EventEnvelope
    {
        public string EventId { get; init; } = "";

        public long EventTimestamp { get; init; }

        public object Payload { get; init; } = null!;
    }

    [DataContract(Name = "CustomerCreated", Namespace = "Demo")]
    public sealed class CustomerCreated
    {
        public string CustomerId { get; init; } = "";

        public string Name { get; init; } = "";
    }

    [DataContract(Name = "OrderPlaced", Namespace = "Demo")]
    public sealed class OrderPlaced
    {
        public string OrderId { get; init; } = "";

        public string CustomerId { get; init; } = "";

        public double Amount { get; init; }
    }
}