using Xunit;

namespace NStalling.Avro.Tests.Samples
{
    // Guards the EventEnvelope sample referenced from the README: if this starts failing, the sample
    // (and the README section describing it) needs to be updated to match.
    public class EventEnvelopeSampleTests
    {
        [Fact]
        public void Run_ResolvesEachUnionBranchToItsConcreteType()
        {
            var lines = EventEnvelope.Program.Run();

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
