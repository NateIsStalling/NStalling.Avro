using Avro;
using NStalling.Avro.Tests.Models;
using NStalling.Avro.Tests.Util;

namespace NStalling.Avro.Tests;

public class AvroSerializerTests
{
    

    [Fact]
    public void SchemaAssistedDeserialize_RoundTripsPoco()
    {
        var avsc = TestUtil.ReadFixture("customer-v1.avsc");
        var schema = Schema.Parse(avsc);

        var original = new CustomerV1
        {
            Id = Guid.NewGuid().ToString(),
            Name = "Ada"
        };

        var bytes = AvroSerializer.Serialize(original, schema);
        var clone = AvroSerializer.Deserialize<CustomerV1>(bytes, schema, schema);

        Assert.Equal(original.Id, clone.Id);
        Assert.Equal(original.Name, clone.Name);
    }

    [Fact]
    public void DynamicDeserialize_UsesTypeResolver_FromWriterSchema()
    {
        var avsc = TestUtil.ReadFixture("customer-v1.avsc");
        var schema = Schema.Parse(avsc);

        var original = new CustomerV1
        {
            Id = "1",
            Name = "Grace"
        };

        var bytes = AvroSerializer.Serialize(original, schema);

        var typeResolver = new DefaultAvroTypeResolver(
            AvroTypeRegistryBuilder.CreateDefault()
                .Map<CustomerV1>(new AvroSchemaName("Customer", "Acme.Events"))
                .Build());

        var deserialized = AvroSerializer.Deserialize(bytes, schema, typeResolver, schema);

        var typed = Assert.IsType<CustomerV1>(deserialized);
        Assert.Equal("1", typed.Id);
        Assert.Equal("Grace", typed.Name);
    }
}