using Avro;
using NStalling.Avro.Resolution;
using NStalling.Avro.Serialization;
using NStalling.Avro.Tests.Fixtures;
using Xunit;

namespace NStalling.Avro.Tests.Serialization
{
    public class ApacheIntegrationTests
    {
        [Fact]
        public void NullableSingleUnion_NonNull_Materializes()
        {
            var resolver = new AvroTypeRegistry().Add<CustomerCreated>().BuildResolver();
            var serializer = new AvroSerializer(resolver);
            var schema = Schema.Parse(Schemas.EnvelopeNullableSingle);
            var value = new EnvelopeObject { EventId = "e", Payload = new CustomerCreated { CustomerId = "c" } };

            var bytes = AvroWriteHelper.Serialize(value, schema, resolver);
            var result = serializer.Deserialize<EnvelopeObject>(bytes, schema);

            Assert.IsType<CustomerCreated>(result.Payload);
        }

        [Fact]
        public void NullableSingleUnion_Null_StaysNull()
        {
            var resolver = new AvroTypeRegistry().Add<CustomerCreated>().BuildResolver();
            var serializer = new AvroSerializer(resolver);
            var schema = Schema.Parse(Schemas.EnvelopeNullableSingle);
            var value = new EnvelopeObject { EventId = "e", Payload = null! };

            var bytes = AvroWriteHelper.Serialize(value, schema, resolver);
            var result = serializer.Deserialize<EnvelopeObject>(bytes, schema);

            Assert.Null(result.Payload);
        }

        [Fact]
        public void UnionBranchSelection_PicksCorrectRecordPerInstance()
        {
            var resolver = new AvroTypeRegistry().Add<CustomerCreated>().Add<OrderPlaced>().BuildResolver();
            var serializer = new AvroSerializer(resolver);
            var schema = Schema.Parse(Schemas.EnvelopeUnion);

            var a = new EnvelopeObject { EventId = "1", Payload = new CustomerCreated { CustomerId = "c" } };
            var b = new EnvelopeObject { EventId = "2", Payload = new OrderPlaced { OrderId = "o" } };

            var ra = serializer.Deserialize<EnvelopeObject>(AvroWriteHelper.Serialize(a, schema, resolver), schema);
            var rb = serializer.Deserialize<EnvelopeObject>(AvroWriteHelper.Serialize(b, schema, resolver), schema);

            Assert.IsType<CustomerCreated>(ra.Payload);
            Assert.IsType<OrderPlaced>(rb.Payload);
        }

        [Fact]
        public void VersionCacheIsolation_SameFullNameDifferentTypes()
        {
            var resolver = new AvroTypeRegistry().Add<LegacyCustomer>().Add<CurrentCustomer>().BuildResolver();
            var serializer = new AvroSerializer(resolver);
            var schema = (RecordSchema)Schema.Parse(Schemas.Customer);

            // The wire bytes are identical (same field layout); only the external version differs.
            var bytes = AvroWriteHelper.Serialize(new CurrentCustomer { Name = "n" }, schema, resolver, "2");

            var asV1 = serializer.Deserialize(bytes, schema, "1");
            var asV2 = serializer.Deserialize(bytes, schema, "2");

            Assert.IsType<LegacyCustomer>(asV1);
            Assert.IsType<CurrentCustomer>(asV2);
        }
    }
}
