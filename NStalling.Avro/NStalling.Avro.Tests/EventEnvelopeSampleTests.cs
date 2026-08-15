namespace NStalling.Avro.Tests;

public class EventEnvelopeSampleTests
{
    [Fact]
    public void EventEnvelope_Deserializes_WithPatternMatching()
    {
        var schema = EventEnvelopeSample.ParseUnionEnvelopeSchema();

        // Register the event types
        var typeResolver = new DefaultAvroTypeResolver(
            AvroTypeRegistryBuilder.CreateDefault()
                .Map<EventEnvelopeSample.CustomerCreated>(new AvroSchemaName("CustomerCreated", "Demo"))
                .Map<EventEnvelopeSample.OrderPlaced>(new AvroSchemaName("OrderPlaced", "Demo"))
                .Build());

        var envelope = new EventEnvelopeSample.EventEnvelope
        {
            EventId = "evt-001",
            EventTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            Payload = new EventEnvelopeSample.CustomerCreated
            {
                CustomerId = "cust-123",
                Name = "Alice"
            }
        };

        var bytes = AvroSerializer.Serialize(envelope, schema, typeResolver);

        var deserialized =
            AvroSerializer.Deserialize<EventEnvelopeSample.EventEnvelope>(bytes, schema, typeResolver, schema);
        var eventDescription = EventEnvelopeSample.DescribePayload(deserialized.Payload);

        Assert.Equal("Customer created: Alice", eventDescription);
        Assert.IsType<EventEnvelopeSample.CustomerCreated>(deserialized.Payload);
    }

    [Fact]
    public void EventEnvelope_DynamicDeserialization_WithTypeResolver()
    {
        var schema = EventEnvelopeSample.ParseOrderEnvelopeSchema();

        var typeResolver = new DefaultAvroTypeResolver(
            AvroTypeRegistryBuilder.CreateDefault()
                .Map<EventEnvelopeSample.OrderPlaced>(new AvroSchemaName("OrderPlaced", "Demo"))
                .Build());

        var envelope = new EventEnvelopeSample.EventEnvelope
        {
            EventId = "evt-002",
            EventTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            Payload = new EventEnvelopeSample.OrderPlaced
            {
                OrderId = "ord-456",
                CustomerId = "cust-123",
                Amount = 99.99
            }
        };

        var bytes = AvroSerializer.Serialize(envelope, schema, typeResolver);

        var deserialized =
            AvroSerializer.Deserialize<EventEnvelopeSample.EventEnvelope>(bytes, schema, typeResolver, schema);

        Assert.IsType<EventEnvelopeSample.OrderPlaced>(deserialized.Payload);
        var order = (EventEnvelopeSample.OrderPlaced)deserialized.Payload;
        Assert.Equal("ord-456", order.OrderId);
        Assert.Equal(99.99, order.Amount);
    }

    [Fact]
    public void EventEnvelopeSample_DescribePayload_FormatsOrderPlaced()
    {
        var message = EventEnvelopeSample.DescribePayload(new EventEnvelopeSample.OrderPlaced
        {
            OrderId = "ord-789",
            CustomerId = "cust-xyz",
            Amount = 12.5
        });

        Assert.Equal("Order placed: $12.5", message);
    }

    [Fact]
    public void EventEnvelopeSample_DescribePayload_UnknownPayload_ReturnsFallback()
    {
        var message = EventEnvelopeSample.DescribePayload(new object());

        Assert.Equal("Unknown event", message);
    }
}