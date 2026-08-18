using System.Runtime.Serialization;

namespace NStalling.Avro.PropertyTests.Fixtures
{
    // Plain, behavior-free CLR types used only as distinguishable tokens: properties in this project
    // care about *which* type a resolver picked, never what the type does.
    public sealed class TypeA
    {
    }

    public sealed class TypeB
    {
    }

    public sealed class TypeC
    {
    }

    public sealed class TypeD
    {
    }

    public sealed class TypeE
    {
    }

    // One DataContract-sourced candidate plus several Explicit-sourced candidates for the same Avro
    // name, used to fuzz source precedence.
    [DataContract(Name = "Widget", Namespace = "PropertyTests.Precedence")]
    public sealed class DataContractWidget
    {
    }

    public sealed class ExplicitWidgetA
    {
    }

    public sealed class ExplicitWidgetB
    {
    }

    public sealed class ExplicitWidgetC
    {
    }
}
