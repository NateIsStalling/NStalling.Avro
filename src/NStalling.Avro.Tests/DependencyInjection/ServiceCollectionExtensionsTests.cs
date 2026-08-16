using Avro;
using Microsoft.Extensions.DependencyInjection;
using NStalling.Avro;
using NStalling.Avro.DependencyInjection;
using NStalling.Avro.Serialization;
using NStalling.Avro.Tests.Fixtures;
using Xunit;

namespace NStalling.Avro.Tests.DependencyInjection
{
    public class ServiceCollectionExtensionsTests
    {
        [Fact]
        public void AddAvro_RegistersResolverSerializerAndConfiguration()
        {
            var services = new ServiceCollection();
            services.AddAvro(o => o.Types(t => t.Add<CustomerCreated>().Add<OrderPlaced>()));

            using var provider = services.BuildServiceProvider();

            Assert.NotNull(provider.GetRequiredService<IAvroTypeResolver>());
            Assert.NotNull(provider.GetRequiredService<AvroConfiguration>());

            var serializer = provider.GetRequiredService<AvroSerializer>();
            var schema = Schema.Parse(Schemas.EnvelopeUnion);
            var resolver = provider.GetRequiredService<IAvroTypeResolver>();
            var value = new EnvelopeObject { EventId = "e", Payload = new CustomerCreated { CustomerId = "c" } };

            var result = serializer.Deserialize<EnvelopeObject>(
                AvroWriteHelper.Serialize(value, schema, resolver), schema);

            Assert.IsType<CustomerCreated>(result.Payload);
        }

        [Fact]
        public void AddAvro_CompilesEagerly_FailsFastOnConfigurationDefect()
        {
            var services = new ServiceCollection();

            Assert.Throws<AvroConfigurationException>(() =>
                services.AddAvro(o => o.Types(t => t
                    .Map<LegacyCustomer>("Customer", "Acme.Events", "2")
                    .Map<CurrentCustomer>("Customer", "Acme.Events", "2"))));
        }
    }
}
