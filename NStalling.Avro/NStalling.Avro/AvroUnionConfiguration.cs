using System.Collections.ObjectModel;

namespace NStalling.Avro;

public sealed class AvroUnionConfiguration
{
    private readonly IReadOnlyDictionary<MemberKey, IReadOnlyList<Type>> _memberUnions;
    private readonly IReadOnlyDictionary<Type, IReadOnlyList<Type>> _typeUnions;

    internal AvroUnionConfiguration(
        IReadOnlyDictionary<Type, IReadOnlyList<Type>> typeUnions,
        IReadOnlyDictionary<MemberKey, IReadOnlyList<Type>> memberUnions)
    {
        _typeUnions = typeUnions;
        _memberUnions = memberUnions;
    }

    internal static AvroUnionConfiguration Empty { get; } = new(
        new ReadOnlyDictionary<Type, IReadOnlyList<Type>>(new Dictionary<Type, IReadOnlyList<Type>>()),
        new ReadOnlyDictionary<MemberKey, IReadOnlyList<Type>>(new Dictionary<MemberKey, IReadOnlyList<Type>>()));

    internal bool TryGetTypeUnion(Type type, out IReadOnlyList<Type> branches)
    {
        return _typeUnions.TryGetValue(type, out branches!);
    }

    internal bool TryGetMemberUnion(Type declaringType, string memberName, out IReadOnlyList<Type> branches)
    {
        return _memberUnions.TryGetValue(new MemberKey(declaringType, memberName), out branches!);
    }

    internal readonly record struct MemberKey(Type DeclaringType, string MemberName);
}

public sealed class AvroUnionConfigurationBuilder
{
    private readonly Dictionary<AvroUnionConfiguration.MemberKey, List<Type>> _memberUnions = new();
    private readonly Dictionary<Type, List<Type>> _typeUnions = new();

    public AvroUnionConfigurationBuilder Union<TBase>(Action<AvroUnionBranchBuilder<TBase>> configure)
    {
        if (configure is null) throw new ArgumentNullException(nameof(configure));

        var branches = new List<Type>();
        var builder = new AvroUnionBranchBuilder<TBase>(branches);
        configure(builder);

        _typeUnions[typeof(TBase)] = branches;
        return this;
    }

    public AvroUnionConfigurationBuilder For<T>(Action<AvroTypeUnionBuilder<T>> configure)
    {
        if (configure is null) throw new ArgumentNullException(nameof(configure));

        var builder = new AvroTypeUnionBuilder<T>(this);
        configure(builder);
        return this;
    }

    internal void SetMemberUnion<TDeclaring>(string memberName, IReadOnlyList<Type> branches)
    {
        _memberUnions[new AvroUnionConfiguration.MemberKey(typeof(TDeclaring), memberName)] = branches.ToList();
    }

    public AvroUnionConfiguration Build()
    {
        var typeMap = _typeUnions.ToDictionary(
            kvp => kvp.Key,
            kvp => (IReadOnlyList<Type>)kvp.Value.ToArray());

        var memberMap = _memberUnions.ToDictionary(
            kvp => kvp.Key,
            kvp => (IReadOnlyList<Type>)kvp.Value.ToArray());

        return new AvroUnionConfiguration(
            new ReadOnlyDictionary<Type, IReadOnlyList<Type>>(typeMap),
            new ReadOnlyDictionary<AvroUnionConfiguration.MemberKey, IReadOnlyList<Type>>(memberMap));
    }
}