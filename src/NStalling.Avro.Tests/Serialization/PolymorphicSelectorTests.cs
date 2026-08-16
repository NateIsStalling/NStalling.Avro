using System.Collections.Generic;
using Avro;
using NStalling.Avro;
using NStalling.Avro.Tests.Fixtures;
using Xunit;

namespace NStalling.Avro.Tests.Serialization
{
    public class PolymorphicSelectorTests
    {
        private static MapPayloadSchemaSource EmptySource()
            => new(new Dictionary<string, Schema>());

        [Fact]
        public void Member_NestedProperty_FailsConfiguration()
        {
            // e => e.Header.Payload resolves the nested Payload's PropertyInfo, which is not a member of
            // NestedEnvelope and would throw at runtime during binding. Reject it up front.
            var ex = Assert.Throws<AvroConfigurationException>(() => new AvroOptions()
                .Types(t => t.Add<CustomerCreated>())
                .Polymorphic<NestedEnvelope>(p => p.Member(e => e.Header.Payload)
                    .DiscriminateBy(e => e.Header.EventType)
                    .PayloadSchema(EmptySource()))
                .Build());

            Assert.Contains("direct property", ex.Message);
        }

        [Fact]
        public void DiscriminateBy_NestedProperty_FailsConfiguration()
        {
            // e => e.Header.EventType would be silently truncated to "EventType"; the expression overload
            // must reject nested paths and steer callers to the string path overload.
            var ex = Assert.Throws<AvroConfigurationException>(() => new AvroOptions()
                .Types(t => t.Add<CustomerCreated>())
                .Polymorphic<NestedEnvelope>(p => p.Member(e => e.Payload)
                    .DiscriminateBy(e => e.Header.EventType)
                    .PayloadSchema(EmptySource()))
                .Build());

            Assert.Contains("direct property", ex.Message);
        }

        [Fact]
        public void VersionBy_NestedProperty_FailsConfiguration()
        {
            var ex = Assert.Throws<AvroConfigurationException>(() => new AvroOptions()
                .Types(t => t.Add<CustomerCreated>())
                .Polymorphic<NestedEnvelope>(p => p.Member(e => e.Payload)
                    .DiscriminateBy(e => e.Payload) // direct, valid
                    .VersionBy(e => e.Header.EventType)
                    .PayloadSchema(EmptySource()))
                .Build());

            Assert.Contains("direct property", ex.Message);
        }

        [Fact]
        public void DiscriminateBy_DirectProperty_IsAccepted()
        {
            var config = new AvroOptions()
                .Types(t => t.Add<CustomerCreated>())
                .Polymorphic<OpaqueEnvelope>(p => p.Member(e => e.Payload)
                    .DiscriminateBy(e => e.EventType)
                    .VersionBy(e => e.SchemaVersion)
                    .PayloadSchema(EmptySource()))
                .Build();

            Assert.NotNull(config);
        }

        [Fact]
        public void DiscriminateBy_NestedStringPath_IsAccepted()
        {
            // The string overload explicitly supports nested discriminator paths.
            var config = new AvroOptions()
                .Types(t => t.Add<CustomerCreated>())
                .Polymorphic<NestedEnvelope>(p => p.Member(e => e.Payload)
                    .DiscriminateBy("Header.EventType")
                    .PayloadSchema(EmptySource()))
                .Build();

            Assert.NotNull(config);
        }
    }
}
