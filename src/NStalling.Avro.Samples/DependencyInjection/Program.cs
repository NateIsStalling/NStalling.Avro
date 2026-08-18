using System.Runtime.Serialization;
using Avro;
using Avro.IO;
using Avro.Reflect;
using Microsoft.Extensions.DependencyInjection;
using NStalling.Avro;
using NStalling.Avro.DependencyInjection;
using NStalling.Avro.Serialization;

namespace DependencyInjection;

// Same envelope/union scenario as the EventEnvelope sample, but configured through
// IServiceCollection.AddAvro instead of building an AvroConfiguration directly.
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
        foreach (var line in Run())
        {
            Console.WriteLine(line);
        }
    }

    // Extracted from Main so NStalling.Avro.Tests can assert on this sample's behavior without
    // capturing console output.
    internal static IReadOnlyList<string> Run()
    {
        var schema = (RecordSchema)Schema.Parse(EnvelopeSchemaJson);

        // AddAvro compiles the AvroOptions eagerly, so a bad mapping fails here at registration
        // rather than surfacing later on first read.
        var services = new ServiceCollection();
        services.AddAvro(t => t.Types(m => m.Add<CustomerCreated>().Add<OrderPlaced>()));

        using var provider = services.BuildServiceProvider();
        var serializer = provider.GetRequiredService<AvroSerializer>();

        // Produce wire bytes for two different payloads using Apache's reflect writer.
        var first = WriteEnvelope(schema,
            new Envelope { EventId = "evt-1", Payload = new CustomerCreated { CustomerId = "cust-42" } });
        var second = WriteEnvelope(schema,
            new Envelope { EventId = "evt-2", Payload = new OrderPlaced { OrderId = "ord-99" } });

        // Deserialize with the DI-resolved serializer; the object payload materializes as the concrete
        // record type.
        var lines = new List<string>();
        foreach (var bytes in new[] { first, second })
        {
            var envelope = serializer.Deserialize<Envelope>(bytes, schema);
            lines.Add($"EventId={envelope.EventId} -> {Describe(envelope.Payload)}");
        }

        return lines;
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
