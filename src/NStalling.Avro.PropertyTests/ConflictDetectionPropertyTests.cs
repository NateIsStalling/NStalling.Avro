using Avro;
using CsCheck;
using NStalling.Avro.PropertyTests.Fixtures;

namespace NStalling.Avro.PropertyTests
{
    // Generalizes the "same type at the same key is not a conflict, different types at the same key and
    // precedence always is" rule (ConfigurationConflictTests in NStalling.Avro.Tests) across the whole
    // type pool instead of the two hand-picked types those examples use.
    public class ConflictDetectionPropertyTests
    {
        private static readonly Type[] Types =
        {
            typeof(TypeA), typeof(TypeB), typeof(TypeC), typeof(TypeD), typeof(TypeE)
        };

        private static RecordSchema WidgetSchema { get; } = (RecordSchema)Schema.Parse(
            @"{""type"":""record"",""name"":""Widget"",""namespace"":""PropertyTests.Conflicts"",""fields"":[]}");

        [Fact]
        public void SameEffectiveKey_ConflictsIfAndOnlyIfRegisteredTypesDiffer()
        {
            Gen.OneOfConst(Types).Select(Gen.OneOfConst(Types))
                .Sample((first, second) =>
                {
                    var registry = new AvroTypeRegistry()
                        .Map(first, "Widget", "PropertyTests.Conflicts")
                        .Map(second, "Widget", "PropertyTests.Conflicts");

                    if (first == second)
                    {
                        var resolver = registry.BuildResolver();
                        Assert.Equal(first, resolver.Resolve(WidgetSchema));
                    }
                    else
                    {
                        Assert.Throws<AvroConfigurationException>(() => registry.BuildResolver());
                    }
                });
        }
    }
}
