using System.Runtime.Serialization;

namespace NStalling.Avro.Tests.Contracts;

[DataContract(Name = "CustomerCreated", Namespace = "Acme.Events")]
public sealed class CustomerCreated
{
    [DataMember(Name = "customer_id", Order = 1)]
    public Guid CustomerId { get; init; }
}