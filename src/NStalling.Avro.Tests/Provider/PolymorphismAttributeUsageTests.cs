using System;
using System.Reflection;
using NStalling.Avro;
using Xunit;

namespace NStalling.Avro.Tests.Provider
{
    /// <summary>
    /// Binding discovery and the binding model are property-only (they scan <see cref="PropertyInfo"/> and
    /// store it). The public polymorphism attributes must therefore advertise <see cref="AttributeTargets.Property"/>
    /// exclusively; advertising <see cref="AttributeTargets.Field"/> would let field annotations compile only
    /// to be silently ignored. These tests fail fast if any attribute re-adds a field (or other) target.
    /// </summary>
    public class PolymorphismAttributeUsageTests
    {
        public static TheoryData<Type> PolymorphismAttributes => new()
        {
            typeof(AvroPolymorphicAttribute),
            typeof(AvroTypeDiscriminatorAttribute),
            typeof(AvroVersionDiscriminatorAttribute),
        };

        [Theory]
        [MemberData(nameof(PolymorphismAttributes))]
        public void Attribute_TargetsPropertiesOnly(Type attributeType)
        {
            var usage = attributeType.GetCustomAttribute<AttributeUsageAttribute>(inherit: true);

            Assert.NotNull(usage);
            Assert.Equal(AttributeTargets.Property, usage!.ValidOn);
        }

        [Theory]
        [MemberData(nameof(PolymorphismAttributes))]
        public void Attribute_DoesNotAdvertiseFields(Type attributeType)
        {
            var usage = attributeType.GetCustomAttribute<AttributeUsageAttribute>(inherit: true);

            Assert.NotNull(usage);
            Assert.False(
                usage!.ValidOn.HasFlag(AttributeTargets.Field),
                $"{attributeType.Name} advertises AttributeTargets.Field, but field annotations are not " +
                "supported by binding discovery and would be silently ignored.");
        }
    }
}
