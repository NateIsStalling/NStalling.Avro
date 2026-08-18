using Xunit;

namespace NStalling.Avro.Tests.Samples
{
    // Guards the DependencyInjection sample referenced from the README: if this starts failing, the
    // sample (and the README section describing it) needs to be updated to match.
    public class DependencyInjectionSampleTests
    {
        [Fact]
        public void Run_ResolvesEachUnionBranchViaDiResolvedSerializer()
        {
            var lines = global::DependencyInjection.Program.Run();

            Assert.Equal(
                new[]
                {
                    "EventId=evt-1 -> CustomerCreated(CustomerId=cust-42)",
                    "EventId=evt-2 -> OrderPlaced(OrderId=ord-99)"
                },
                lines);
        }
    }
}
