using Avro;
using NStalling.Avro;
using NStalling.Avro.Tests.Fixtures;
using Xunit;

namespace NStalling.Avro.Tests.Documentation
{
    // Reproduces the README "Registering types" code: Add, Map, Map(schemaVersion), and
    // FromAssemblyContaining chained on one registry. Each source is unit-tested individually elsewhere
    // (DiscoveryTests, VersionResolutionTests); this guards that the combined chain still composes and
    // resolves as documented.
    public class RegisteringTypesSnippetTests
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
        public void ChainedExplicitDataContractAndAssemblyScanSources_AllResolve()
        {
            // Product's own attributes already cover versions "2" and "3" (see VersionResolutionTests), so
            // mapping it explicitly at one of those versions wouldn't actually depend on the Map call.
            // Version "4" is supplied only by the explicit Map below, so this assertion is load-bearing:
            // once any versioned mapping exists for a name, an unmapped version never falls back to the
            // unqualified bucket, so removing the Map call would make this throw rather than pass.
            var resolver = new AvroTypeRegistry()
                .Add<CustomerCreated>()
                .Map<OrderPlaced>("OrderPlaced", "Acme.Events")
                .Map<Product>("Product", "Acme.Events", schemaVersion: "4")
                .FromAssemblyContaining<CustomerCreated>()
                .BuildResolver();

            Assert.Equal(typeof(CustomerCreated), resolver.Resolve(Record("Acme.Events.CustomerCreated")));
            Assert.Equal(typeof(OrderPlaced), resolver.Resolve(Record("Acme.Events.OrderPlaced")));
            Assert.Equal(typeof(Product), resolver.Resolve(Record("Acme.Events.Product"), null, "4"));
        }
    }
}
