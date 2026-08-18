using Xunit;

namespace NStalling.Avro.Tests.Samples
{
    // Guards the Annotations sample referenced from the README: if this starts failing, the sample
    // (and the README section describing it) needs to be updated to match.
    public class AnnotationsSampleTests
    {
        [Fact]
        public void Run_SelectsClrTypeByVersionDiscriminator()
        {
            var lines = Annotations.Program.Run();

            Assert.Equal(
                new[]
                {
                    "evt-1 version=1 -> LegacyProfile(Name=Ada)",
                    "evt-2 version=2 -> CurrentProfile(Name=Ada)"
                },
                lines);
        }
    }
}
