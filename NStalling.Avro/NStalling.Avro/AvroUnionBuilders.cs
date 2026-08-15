using System.Linq.Expressions;
using System.Reflection;

namespace NStalling.Avro;

public sealed class AvroUnionBranchBuilder<TBase>
{
    private readonly List<Type> _branches;

    internal AvroUnionBranchBuilder(List<Type> branches)
    {
        _branches = branches;
    }

    public AvroUnionBranchBuilder<TBase> Add<TBranch>()
    {
        _branches.Add(typeof(TBranch));
        return this;
    }
}

public sealed class AvroTypeUnionBuilder<T>
{
    private readonly AvroUnionConfigurationBuilder _owner;

    internal AvroTypeUnionBuilder(AvroUnionConfigurationBuilder owner)
    {
        _owner = owner;
    }

    public AvroMemberUnionBuilder<T, TMember> Member<TMember>(Expression<Func<T, TMember>> memberExpression)
    {
        var member = GetMember(memberExpression);
        return new AvroMemberUnionBuilder<T, TMember>(_owner, member.Name);
    }

    private static PropertyInfo GetMember<TMember>(Expression<Func<T, TMember>> expression)
    {
        if (expression.Body is MemberExpression member && member.Member is PropertyInfo propertyInfo)
            return propertyInfo;

        if (expression.Body is UnaryExpression { Operand: MemberExpression boxedMember }
            && boxedMember.Member is PropertyInfo boxedProperty)
            return boxedProperty;

        throw new ArgumentException("Member expression must reference a property.", nameof(expression));
    }
}

public sealed class AvroMemberUnionBuilder<TDeclaring, TMember>
{
    private readonly string _memberName;
    private readonly AvroUnionConfigurationBuilder _owner;

    internal AvroMemberUnionBuilder(AvroUnionConfigurationBuilder owner, string memberName)
    {
        _owner = owner;
        _memberName = memberName;
    }

    public AvroTypeUnionBuilder<TDeclaring> Union(Action<AvroUnionBranchBuilder<TMember>> configure)
    {
        if (configure is null) throw new ArgumentNullException(nameof(configure));

        var branches = new List<Type>();
        var builder = new AvroUnionBranchBuilder<TMember>(branches);
        configure(builder);

        _owner.SetMemberUnion<TDeclaring>(_memberName, branches);
        return new AvroTypeUnionBuilder<TDeclaring>(_owner);
    }
}