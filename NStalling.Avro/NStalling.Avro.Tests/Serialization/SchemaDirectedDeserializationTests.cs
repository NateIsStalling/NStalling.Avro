using Avro;
using NStalling.Avro;
using NStalling.Avro.Serialization;
using NStalling.Avro.Tests.Fixtures;
using Xunit;

namespace NStalling.Avro.Tests.Serialization
{
    public class SchemaDirectedDeserializationTests
    {
        private static (AvroSerializer serializer, IAvroTypeResolver resolver) Build()
        {
            var resolver = new AvroTypeRegistry()
                .Add<CustomerCreated>()
                .Add<OrderPlaced>()
                .BuildResolver();
            return (new AvroSerializer(resolver), resolver);
        }

        [Fact]
        public void ObjectMember_MaterializesConcreteRecord()
        {
            var (serializer, resolver) = Build();
            var schema = Schema.Parse(Schemas.EnvelopeUnion);
            var value = new EnvelopeObject { EventId = "e1", Payload = new CustomerCreated { CustomerId = "c42" } };

            var bytes = AvroWriteHelper.Serialize(value, schema, resolver);
            var result = serializer.Deserialize<EnvelopeObject>(bytes, schema);

            Assert.Equal("e1", result.EventId);
            var customer = Assert.IsType<CustomerCreated>(result.Payload);
            Assert.Equal("c42", customer.CustomerId);
        }

        [Fact]
        public void InterfaceMember_MaterializesImplementation()
        {
            var (serializer, resolver) = Build();
            var schema = Schema.Parse(Schemas.EnvelopeUnion);
            var value = new EnvelopeInterface { EventId = "e2", Payload = new OrderPlaced { OrderId = "o99" } };

            var bytes = AvroWriteHelper.Serialize(value, schema, resolver);
            var result = serializer.Deserialize<EnvelopeInterface>(bytes, schema);

            var order = Assert.IsType<OrderPlaced>(result.Payload);
            Assert.Equal("o99", order.OrderId);
        }

        [Fact]
        public void AbstractMember_MaterializesImplementation()
        {
            var (serializer, resolver) = Build();
            var schema = Schema.Parse(Schemas.EnvelopeUnion);
            var value = new EnvelopeAbstract { EventId = "e3", Payload = new CustomerCreated { CustomerId = "c7" } };

            var bytes = AvroWriteHelper.Serialize(value, schema, resolver);
            var result = serializer.Deserialize<EnvelopeAbstract>(bytes, schema);

            Assert.IsType<CustomerCreated>(result.Payload);
        }
    }
}
