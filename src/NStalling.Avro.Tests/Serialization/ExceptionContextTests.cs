using Avro;
using NStalling.Avro;
using NStalling.Avro.Serialization;
using NStalling.Avro.Tests.Fixtures;
using Xunit;

namespace NStalling.Avro.Tests.Serialization
{
    public class ExceptionContextTests
    {
        [Fact]
        public void CorruptTopLevelPayload_WrapsInnerAndPopulatesContext()
        {
            var resolver = new AvroTypeRegistry().Add<CustomerCreated>().BuildResolver();
            var serializer = new AvroSerializer(resolver);
            var schema = Schema.Parse(Schemas.CustomerCreatedRecord);

            var ex = Assert.Throws<AvroSerializationException>(() =>
                serializer.Deserialize<CustomerCreated>(new byte[] { 0xFF, 0xFF }, schema));

            Assert.NotNull(ex.InnerException);
            Assert.Equal("Acme.Events.CustomerCreated", ex.SchemaFullName);
        }

        [Fact]
        public void ResolutionFailure_PropagatesUnwrapped()
        {
            var resolver = new AvroTypeRegistry().Add<CustomerCreated>().BuildResolver();
            var serializer = new AvroSerializer(resolver);
            var schema = Schema.Parse(Schemas.EnvelopeUnion);
            var value = new EnvelopeObject { EventId = "e", Payload = new OrderPlaced { OrderId = "o" } };

            // OrderPlaced is unmapped in the read resolver, so the union branch cannot be resolved.
            var writeResolver = new AvroTypeRegistry().Add<CustomerCreated>().Add<OrderPlaced>().BuildResolver();
            var payload = AvroWriteHelper.Serialize(value, schema, writeResolver);

            Assert.Throws<AvroTypeResolutionException>(() =>
                serializer.Deserialize<EnvelopeObject>(payload, schema));
        }
    }
}
