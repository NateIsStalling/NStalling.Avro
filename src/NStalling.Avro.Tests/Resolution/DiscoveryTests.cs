using System.Reflection;
using Avro;
using NStalling.Avro;
using NStalling.Avro.Tests.Fixtures;
using Xunit;

namespace NStalling.Avro.Tests.Resolution
{
    public class DiscoveryTests
    {
        private static RecordSchema Record(string fullName)
        {
            var idx = fullName.LastIndexOf('.');
            var name = fullName.Substring(idx + 1);
            var ns = fullName.Substring(0, idx);
            return (RecordSchema)Schema.Parse(
                $@"{{""type"":""record"",""name"":""{name}"",""namespace"":""{ns}"",""fields"":[]}}");
        }

        [Fact]
        public void DataContractName_And_ClrFullName_BothResolveSameType()
        {
            var resolver = new AvroTypeRegistry().Add<CustomerCreated>().BuildResolver();

            Assert.Equal(typeof(CustomerCreated), resolver.Resolve(Record("Acme.Events.CustomerCreated")));
            Assert.Equal(
                typeof(CustomerCreated),
                resolver.Resolve(Record("NStalling.Avro.Tests.Fixtures.CustomerCreated")));
        }

        [Fact]
        public void AssemblyScan_DiscoversDataContractTypes()
        {
            var resolver = new AvroTypeRegistry()
                .FromAssemblyContaining<CustomerCreated>()
                .BuildResolver();

            Assert.Equal(typeof(OrderPlaced), resolver.Resolve(Record("Acme.Events.OrderPlaced")));
        }

        [Fact]
        public void VersionAttribute_PlacesTypeOnlyInVersionedBucket()
        {
            var resolver = new AvroTypeRegistry().Add<LegacyCustomer>().BuildResolver();
            var customer = (RecordSchema)Schema.Parse(Schemas.Customer);

            Assert.Equal(typeof(LegacyCustomer), resolver.Resolve(customer, null, "1"));
            // No unqualified mapping is produced for a version-attributed type.
            Assert.Null(resolver.ResolveOrDefault(customer));
        }
    }
}
