namespace NStalling.Avro.Tests.Models;

public interface IPayment
{
}

public sealed class Order
{
    public IPayment? Payment { get; init; }
}

public sealed class CardPayment : IPayment
{
    public string Last4 { get; init; } = string.Empty;
}

public sealed class WirePayment : IPayment
{
    public string Iban { get; init; } = string.Empty;
}