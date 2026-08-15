namespace NStalling.Avro;

public sealed class AvroOptionsBuilder
{
    private readonly AvroTypeRegistryBuilder _typeRegistryBuilder = AvroTypeRegistryBuilder.CreateDefault();
    private readonly AvroUnionConfigurationBuilder _unionBuilder = new();

    public AvroOptionsBuilder Types(Action<AvroTypeRegistryBuilder> configure)
    {
        if (configure is null) throw new ArgumentNullException(nameof(configure));
        configure(_typeRegistryBuilder);
        return this;
    }

    public AvroOptionsBuilder Union<TBase>(Action<AvroUnionBranchBuilder<TBase>> configure)
    {
        _unionBuilder.Union(configure);
        return this;
    }

    public AvroOptionsBuilder For<T>(Action<AvroTypeUnionBuilder<T>> configure)
    {
        _unionBuilder.For(configure);
        return this;
    }

    internal AvroTypeRegistry BuildRegistry()
    {
        return _typeRegistryBuilder.Build();
    }

    internal AvroUnionConfiguration BuildUnionConfiguration()
    {
        return _unionBuilder.Build();
    }
}