using System.Runtime.Serialization;
using Avro;
using Avro.IO;
using Avro.Reflect;
using NStalling.Avro.Configuration;

namespace EventEnvelope;

// The application envelope. Payload is declared as `object`; NStalling.Avro materializes the concrete
// record type behind it during deserialization. No envelope-specific library API is involved.
public sealed class Envelope
{
    public string EventId { get; init; } = "";

    public object Payload { get; init; } = null!;
}

[DataContract(Name = "CustomerCreated", Namespace = "Acme.Events")]
public sealed class CustomerCreated
{
    public string CustomerId { get; init; } = "";
}

[DataContract(Name = "OrderPlaced", Namespace = "Acme.Events")]
public sealed class OrderPlaced
{
    public string OrderId { get; init; } = "";
}

internal static class Program
{
    // Envelope schema whose Payload is a union of two named payload records.
    private const string EnvelopeSchemaJson = @"{
      ""type"":""record"",""name"":""Envelope"",""namespace"":""Acme.Events"",
      ""fields"":[
        {""name"":""EventId"",""type"":""string""},
        {""name"":""Payload"",""type"":[
           {""type"":""record"",""name"":""CustomerCreated"",""namespace"":""Acme.Events"",""fields"":[{""name"":""CustomerId"",""type"":""string""}]},
           {""type"":""record"",""name"":""OrderPlaced"",""namespace"":""Acme.Events"",""fields"":[{""name"":""OrderId"",""type"":""string""}]}
        ]}
      ]}";

    private static void Main()
    {
        var schema = (RecordSchema)Schema.Parse(EnvelopeSchemaJson);

        // Configure NStalling.Avro: map the payload records so union branches resolve to concrete CLR types.
        var config = new AvroOptions()
            .Types(t => t.Add<CustomerCreated>().Add<OrderPlaced>())
            .Build();

        // Produce wire bytes for two different payloads using Apache's reflect writer.
        var first = WriteEnvelope(schema,
            new Envelope { EventId = "evt-1", Payload = new CustomerCreated { CustomerId = "cust-42" } });
        var second = WriteEnvelope(schema,
            new Envelope { EventId = "evt-2", Payload = new OrderPlaced { OrderId = "ord-99" } });

        // Deserialize with NStalling.Avro; the object payload materializes as the concrete record type.
        foreach (var bytes in new[] { first, second })
        {
            var envelope = config.Serializer.Deserialize<Envelope>(bytes, schema);
            Console.WriteLine($"EventId={envelope.EventId} -> {Describe(envelope.Payload)}");
        }
    }

    private static string Describe(object payload) => payload switch
    {
        CustomerCreated c => $"CustomerCreated(CustomerId={c.CustomerId})",
        OrderPlaced o => $"OrderPlaced(OrderId={o.OrderId})",
        _ => $"Unknown({payload})"
    };

    // Builds an Apache write cache using only public ClassCache API: registering each union branch
    // record against its concrete CLR type is enough for the reflect writer to select the right branch.
    private static byte[] WriteEnvelope(RecordSchema schema, Envelope value)
    {
        var cache = new ClassCache();
        var union = (UnionSchema)schema["Payload"].Schema;
        cache.LoadClassCache(typeof(CustomerCreated), BranchNamed(union, "Acme.Events.CustomerCreated"));
        cache.LoadClassCache(typeof(OrderPlaced), BranchNamed(union, "Acme.Events.OrderPlaced"));

        var writer = new ReflectWriter<Envelope>(schema, cache);
        using var stream = new MemoryStream();
        writer.Write(value, new BinaryEncoder(stream));
        return stream.ToArray();
    }

    private static RecordSchema BranchNamed(UnionSchema union, string fullName)
    {
        foreach (var branch in union.Schemas)
        {
            if (branch is RecordSchema record && record.Fullname == fullName)
            {
                return record;
            }
        }

        throw new InvalidOperationException($"Union branch '{fullName}' not found.");
    }
}
