using Avro;
using NStalling.Avro;
using NStalling.Avro.Serialization;
using NStalling.Avro.Tests.Fixtures;
using Xunit;

namespace NStalling.Avro.Tests.Documentation
{
    // Reproduces the README "Quick start" code: a directly-constructed AvroTypeRegistry/AvroSerializer
    // (no AvroOptions/AddAvro, unlike every runnable sample) resolving both union branches for pattern
    // matching. Uses the shared Fixtures types rather than redeclaring [DataContract]-attributed
    // CustomerCreated/OrderPlaced types locally, which would collide with FromAssemblyContaining scans
    // elsewhere in this assembly (see RegisteringTypesSnippetTests, DiscoveryTests).
    public class QuickStartSnippetTests
    {
        [Fact]
        public void ResolverAndSerializer_MaterializeUnionBranchesForPatternMatching()
        {
            var resolver = new AvroTypeRegistry()
                .Add<CustomerCreated>()
                .Add<OrderPlaced>()
                .BuildResolver();

            var serializer = new AvroSerializer(resolver);
            var schema = (RecordSchema)Schema.Parse(Schemas.EnvelopeUnion);

            var customerBytes = AvroWriteHelper.Serialize(
                new EnvelopeObject { EventId = "e1", Payload = new CustomerCreated { CustomerId = "c1" } }, schema, resolver);
            var orderBytes = AvroWriteHelper.Serialize(
                new EnvelopeObject { EventId = "e2", Payload = new OrderPlaced { OrderId = "o1" } }, schema, resolver);

            var customerEnvelope = serializer.Deserialize<EnvelopeObject>(customerBytes, schema);
            var orderEnvelope = serializer.Deserialize<EnvelopeObject>(orderBytes, schema);

            Assert.Equal("customer:c1", Handle(customerEnvelope.Payload));
            Assert.Equal("order:o1", Handle(orderEnvelope.Payload));
        }

        private static string Handle(object payload) => payload switch
        {
            CustomerCreated c => $"customer:{c.CustomerId}",
            OrderPlaced o => $"order:{o.OrderId}",
            _ => "unhandled"
        };
    }
}
