using Avro;
using CsCheck;
using NStalling.Avro.PropertyTests.Fixtures;

namespace NStalling.Avro.PropertyTests
{
    // Generalizes the README's documented precedence rule (Explicit > DataContract > ClrConvention)
    // across random registration order and random choice of which Explicit-sourced type is registered,
    // rather than the single hand-picked ordering an example test would cover.
    public class PrecedencePropertyTests
    {
        private static readonly Type[] ExplicitTypes =
        {
            typeof(ExplicitWidgetA), typeof(ExplicitWidgetB), typeof(ExplicitWidgetC)
        };

        private static RecordSchema WidgetSchema { get; } = (RecordSchema)Schema.Parse(
            @"{""type"":""record"",""name"":""Widget"",""namespace"":""PropertyTests.Precedence"",""fields"":[]}");

        [Fact]
        public void ExplicitMapping_AlwaysBeatsDataContractMapping_RegardlessOfRegistrationOrder()
        {
            Gen.OneOfConst(ExplicitTypes).Select(Gen.Bool)
                .Sample((explicitType, explicitFirst) =>
                {
                    var registry = new AvroTypeRegistry();
                    if (explicitFirst)
                    {
                        registry.Map(explicitType, "Widget", "PropertyTests.Precedence");
                        registry.Add<DataContractWidget>();
                    }
                    else
                    {
                        registry.Add<DataContractWidget>();
                        registry.Map(explicitType, "Widget", "PropertyTests.Precedence");
                    }

                    var resolver = registry.BuildResolver();

                    Assert.Equal(explicitType, resolver.Resolve(WidgetSchema));
                });
        }
    }
}
