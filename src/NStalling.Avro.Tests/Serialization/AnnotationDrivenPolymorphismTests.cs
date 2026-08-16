using System.Collections.Generic;
using Avro;
using NStalling.Avro;
using NStalling.Avro.Tests.Fixtures;
using Xunit;

namespace NStalling.Avro.Tests.Serialization
{
    /// <summary>
    /// End-to-end coverage of the supported member surface: the polymorphism attributes are property-only,
    /// and attribute-driven discovery materializes the opaque payload from marker attributes placed on
    /// properties (no fluent discriminator configuration).
    /// </summary>
    public class AnnotationDrivenPolymorphismTests
    {
        private static byte[] InnerCustomerCreated(string id)
        {
            var innerSchema = (RecordSchema)Schema.Parse(Schemas.CustomerCreatedRecord);
            var resolver = new AvroTypeRegistry().Add<CustomerCreated>().BuildResolver();
            return AvroWriteHelper.Serialize(new CustomerCreated { CustomerId = id }, innerSchema, resolver);
        }

        private static byte[] WriteEnvelope(string eventType, string? version, byte[] payload)
        {
            var schema = Schema.Parse(Schemas.OpaqueEnvelope);
            var resolver = new AvroTypeRegistry().Add<CustomerCreated>().BuildResolver();
            var wire = new OpaqueEnvelopeWire { EventType = eventType, SchemaVersion = version, Payload = payload };
            return AvroWriteHelper.Serialize(wire, schema, resolver);
        }

        [Fact]
        public void MarkerAttributesOnProperties_DriveValueDirectedDecoding()
        {
            // No DiscriminateBy/VersionBy: the discriminators are discovered from the property-level
            // [AvroTypeDiscriminator]/[AvroVersionDiscriminator]/[AvroPolymorphic] attributes.
            var source = new MapPayloadSchemaSource(new Dictionary<string, Schema>
            {
                ["CustomerCreated"] = Schema.Parse(Schemas.CustomerCreatedRecord)
            });

            var config = new AvroOptions()
                .Types(t => t.Add<CustomerCreated>())
                .Polymorphic<AnnotatedOpaqueEnvelope>(p => p.Member(e => e.Payload).PayloadSchema(source))
                .Build();

            var bytes = WriteEnvelope("CustomerCreated", "1", InnerCustomerCreated("c1"));

            var result = config.Serializer.Deserialize<AnnotatedOpaqueEnvelope>(
                bytes, Schema.Parse(Schemas.OpaqueEnvelope));

            var customer = Assert.IsType<CustomerCreated>(result.Payload);
            Assert.Equal("c1", customer.CustomerId);
        }
    }
}
