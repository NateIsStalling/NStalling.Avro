using Avro;
using CsCheck;
using NStalling.Avro.PropertyTests.Fixtures;

namespace NStalling.Avro.PropertyTests
{
    // The bug that motivated this project: a hand-picked example test asserted a version resolved
    // correctly, but the assertion turned out to hold for a reason unrelated to the code path it meant
    // to pin (see README.md "Schema versions" / Documentation/RegisteringTypesSnippetTests.cs history).
    // This property generalizes the invariant that example was supposed to guard: once any
    // version-qualified mapping exists for an Avro full name, requesting a version outside that set must
    // fail outright -- it must never silently fall back to another version's mapping.
    public class VersionFallbackPropertyTests
    {
        private static readonly Type[] Types =
        {
            typeof(TypeA), typeof(TypeB), typeof(TypeC), typeof(TypeD), typeof(TypeE)
        };

        private static readonly string[] Versions = { "1", "2", "3", "4", "5" };

        private static RecordSchema Record(string fullName)
        {
            var idx = fullName.LastIndexOf('.');
            var name = fullName[(idx + 1)..];
            var ns = fullName[..idx];
            return (RecordSchema)Schema.Parse(
                $@"{{""type"":""record"",""name"":""{name}"",""namespace"":""{ns}"",""fields"":[]}}");
        }

        [Fact]
        public void UnmappedVersion_NeverFallsBackWhenAnyVersionedMappingExists()
        {
            // Shuffle the version pool, then register the first `registeredCount` of them (each to a
            // distinct type). The version at index `registeredCount` is guaranteed absent from the
            // registry -- that's the one we query.
            Gen.Shuffle(Versions).Select(Gen.Int[1, Versions.Length - 1])
                .Sample((shuffledVersions, registeredCount) =>
                {
                    var registry = new AvroTypeRegistry();
                    for (var i = 0; i < registeredCount; i++)
                    {
                        registry.Map(Types[i], "Widget", "PropertyTests.Versioning", shuffledVersions[i]);
                    }

                    var resolver = registry.BuildResolver();
                    var schema = Record("PropertyTests.Versioning.Widget");

                    for (var i = 0; i < registeredCount; i++)
                    {
                        Assert.Equal(Types[i], resolver.Resolve(schema, null, shuffledVersions[i]));
                    }

                    var missingVersion = shuffledVersions[registeredCount];

                    Assert.False(resolver.TryResolve(schema, null, missingVersion, out _));
                    Assert.Null(resolver.ResolveOrDefault(schema, null, missingVersion));
                    Assert.Throws<AvroTypeResolutionException>(() => resolver.Resolve(schema, null, missingVersion));
                });
        }
    }
}
