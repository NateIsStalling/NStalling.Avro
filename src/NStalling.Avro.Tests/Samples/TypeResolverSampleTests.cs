using Xunit;

namespace NStalling.Avro.Tests.Samples
{
    // Guards the TypeResolver sample referenced from the README: if this starts failing, the sample
    // (and the README section describing it) needs to be updated to match.
    public class TypeResolverSampleTests
    {
        [Fact]
        public void Run_ExercisesResolveTryResolveAndResolveOrDefault()
        {
            var lines = TypeResolver.Program.Run();

            Assert.Equal(
                new[]
                {
                    "Resolve(Profile, v1) -> ProfileV1",
                    "Resolve(Profile, v2) -> ProfileV2",
                    "Resolve(Address, declaredType=IProfile) threw: Resolved CLR type 'TypeResolver.Address' " +
                    "for Avro record 'Acme.Directory.Address' is not assignable to the declared member type " +
                    "'TypeResolver.IProfile'.",
                    "TryResolve(Profile, v3) -> False",
                    "TryResolve(Unknown) -> False, type=null",
                    "ResolveOrDefault(Unknown) -> null"
                },
                lines);
        }
    }
}
