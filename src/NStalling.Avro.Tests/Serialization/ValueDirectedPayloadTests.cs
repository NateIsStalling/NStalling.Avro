using System.Collections.Generic;
using Avro;
using NStalling.Avro;
using NStalling.Avro.Serialization;
using NStalling.Avro.Tests.Fixtures;
using Xunit;

namespace NStalling.Avro.Tests.Serialization
{
    public class ValueDirectedPayloadTests
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

        private static AvroConfiguration Configure(
            IAvroPayloadSchemaSource source,
            AvroUnrecognizedTypeDiscriminatorHandling? handling = null)
        {
            return new AvroOptions()
                .Types(t => t.Add<CustomerCreated>())
                .Polymorphic<OpaqueEnvelope>(p =>
                {
                    var member = p.Member(e => e.Payload)
                        .DiscriminateBy(e => e.EventType)
                        .VersionBy(e => e.SchemaVersion)
                        .PayloadSchema(source);
                    if (handling is { } h)
                    {
                        member.OnUnrecognizedTypeDiscriminator(h);
                    }
                })
                .Build();
        }

        [Fact]
        public void OpaqueBytes_MaterializeConcreteTypeFromDiscriminator()
        {
            var source = new MapPayloadSchemaSource(new Dictionary<string, Schema>
            {
                ["CustomerCreated"] = Schema.Parse(Schemas.CustomerCreatedRecord)
            });
            var config = Configure(source);
            var bytes = WriteEnvelope("CustomerCreated", null, InnerCustomerCreated("c1"));

            var result = config.Serializer.Deserialize<OpaqueEnvelope>(bytes, Schema.Parse(Schemas.OpaqueEnvelope));

            var customer = Assert.IsType<CustomerCreated>(result.Payload);
            Assert.Equal("c1", customer.CustomerId);
        }

        [Fact]
        public void PayloadSchemaSource_NotFound_HonorsUnrecognizedHandling()
        {
            var source = new MapPayloadSchemaSource(new Dictionary<string, Schema>());
            var config = Configure(source, AvroUnrecognizedTypeDiscriminatorHandling.PreservePayload);
            var payload = InnerCustomerCreated("c2");
            var bytes = WriteEnvelope("Unknown", null, payload);

            var result = config.Serializer.Deserialize<OpaqueEnvelope>(bytes, Schema.Parse(Schemas.OpaqueEnvelope));

            // The raw payload is preserved when the discriminator is unrecognized.
            Assert.Equal(payload, Assert.IsType<byte[]>(result.Payload));
        }

        [Fact]
        public void PayloadSchemaSource_NotFound_FailByDefault()
        {
            var source = new MapPayloadSchemaSource(new Dictionary<string, Schema>());
            var config = Configure(source);
            var bytes = WriteEnvelope("Unknown", null, InnerCustomerCreated("c3"));

            Assert.Throws<AvroTypeResolutionException>(() =>
                config.Serializer.Deserialize<OpaqueEnvelope>(bytes, Schema.Parse(Schemas.OpaqueEnvelope)));
        }

        [Fact]
        public void PayloadSchemaSource_Throws_SurfacesAsPayloadSchemaException()
        {
            var config = Configure(new ThrowingPayloadSchemaSource());
            var bytes = WriteEnvelope("CustomerCreated", null, InnerCustomerCreated("c4"));

            var ex = Assert.Throws<AvroPayloadSchemaException>(() =>
                config.Serializer.Deserialize<OpaqueEnvelope>(bytes, Schema.Parse(Schemas.OpaqueEnvelope)));
            Assert.IsType<System.InvalidOperationException>(ex.InnerException);
        }

        [Fact]
        public void CorruptInnerPayload_SurfacesAsPayloadDecodeException()
        {
            var source = new MapPayloadSchemaSource(new Dictionary<string, Schema>
            {
                ["CustomerCreated"] = Schema.Parse(Schemas.CustomerCreatedRecord)
            });
            var config = Configure(source);
            var bytes = WriteEnvelope("CustomerCreated", null, new byte[] { 0xFF });

            Assert.Throws<AvroPayloadDecodeException>(() =>
                config.Serializer.Deserialize<OpaqueEnvelope>(bytes, Schema.Parse(Schemas.OpaqueEnvelope)));
        }

        [Fact]
        public void UseFallbackType_WithoutAllowlistedFallback_FailsConfiguration()
        {
            var source = new MapPayloadSchemaSource(new Dictionary<string, Schema>());

            // OrderPlaced is not registered in the resolver allowlist, so UseFallbackType must fail fast.
            Assert.Throws<AvroConfigurationException>(() => new AvroOptions()
                .Types(t => t.Add<CustomerCreated>())
                .Polymorphic<OpaqueEnvelope>(p => p.Member(e => e.Payload)
                    .DiscriminateBy(e => e.EventType)
                    .PayloadSchema(source)
                    .FallbackTo<OrderPlaced>())
                .Build());
        }

        [Fact]
        public void UseFallbackType_OnClrMappingFailure_DecodesIntoFallback()
        {
            // The inner writer schema IS identified (CustomerCreated), but the resolver maps no CLR type to
            // that record (only FallbackEvent is registered). UseFallbackType therefore diverts this
            // CLR-mapping failure and decodes the payload into the allowlisted fallback type.
            var source = new MapPayloadSchemaSource(new Dictionary<string, Schema>
            {
                ["CustomerCreated"] = Schema.Parse(Schemas.CustomerCreatedRecord)
            });

            var config = new AvroOptions()
                .Types(t => t.Add<FallbackEvent>())
                .Polymorphic<OpaqueEnvelope>(p => p.Member(e => e.Payload)
                    .DiscriminateBy(e => e.EventType)
                    .VersionBy(e => e.SchemaVersion)
                    .PayloadSchema(source)
                    .FallbackTo<FallbackEvent>())
                .Build();

            var bytes = WriteEnvelope("CustomerCreated", null, InnerCustomerCreated("c9"));

            var result = config.Serializer.Deserialize<OpaqueEnvelope>(bytes, Schema.Parse(Schemas.OpaqueEnvelope));

            var fallback = Assert.IsType<FallbackEvent>(result.Payload);
            Assert.Equal("c9", fallback.CustomerId);
        }

        [Fact]
        public void UseFallbackType_OnUnrecognizedIdentity_Throws()
        {
            // No inner writer schema is obtainable (discriminator not found), so the bytes cannot be
            // decoded and the fallback CLR type is inapplicable: UseFallbackType fails like Fail here.
            var source = new MapPayloadSchemaSource(new Dictionary<string, Schema>());

            var config = new AvroOptions()
                .Types(t => t.Add<FallbackEvent>())
                .Polymorphic<OpaqueEnvelope>(p => p.Member(e => e.Payload)
                    .DiscriminateBy(e => e.EventType)
                    .PayloadSchema(source)
                    .FallbackTo<FallbackEvent>())
                .Build();

            var bytes = WriteEnvelope("Unknown", null, InnerCustomerCreated("c10"));

            var ex = Assert.Throws<AvroTypeResolutionException>(() =>
                config.Serializer.Deserialize<OpaqueEnvelope>(bytes, Schema.Parse(Schemas.OpaqueEnvelope)));
            Assert.Contains("fallback type cannot be used", ex.Message);
        }

        [Fact]
        public void InterfaceDeclaredOpaqueMember_FailsConfiguration()
        {
            var source = new MapPayloadSchemaSource(new Dictionary<string, Schema>());

            // A byte[] payload cannot be held by an interface-typed member during the first pass, so the
            // declaration must be rejected at build time rather than failing deep inside Apache.
            var ex = Assert.Throws<AvroConfigurationException>(() => new AvroOptions()
                .Types(t => t.Add<CustomerCreated>())
                .Polymorphic<EnvelopeInterface>(p => p.Member(e => e.Payload)
                    .DiscriminateBy(e => e.EventId)
                    .PayloadSchema(source))
                .Build());

            Assert.Contains("EnvelopeInterface.Payload", ex.Message);
        }

        [Fact]
        public void AbstractBaseDeclaredOpaqueMember_FailsConfiguration()
        {
            var source = new MapPayloadSchemaSource(new Dictionary<string, Schema>());

            var ex = Assert.Throws<AvroConfigurationException>(() => new AvroOptions()
                .Types(t => t.Add<CustomerCreated>())
                .Polymorphic<EnvelopeAbstract>(p => p.Member(e => e.Payload)
                    .DiscriminateBy(e => e.EventId)
                    .PayloadSchema(source))
                .Build());

            Assert.Contains("EnvelopeAbstract.Payload", ex.Message);
        }
    }
}
