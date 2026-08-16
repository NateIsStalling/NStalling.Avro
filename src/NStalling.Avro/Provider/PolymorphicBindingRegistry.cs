using System;
using System.Collections.Generic;

namespace NStalling.Avro.Provider
{
    /// <summary>Read-only lookup of compiled polymorphic member bindings by outer CLR type.</summary>
    internal interface IPolymorphicBindingProvider
    {
        bool TryGetBindings(Type type, out IReadOnlyList<PolymorphicMemberBinding> bindings);
    }

    internal sealed class PolymorphicBindingRegistry : IPolymorphicBindingProvider
    {
        private static readonly IReadOnlyList<PolymorphicMemberBinding> Empty = Array.Empty<PolymorphicMemberBinding>();
        private readonly IReadOnlyDictionary<Type, IReadOnlyList<PolymorphicMemberBinding>> _bindings;

        public PolymorphicBindingRegistry(IReadOnlyDictionary<Type, IReadOnlyList<PolymorphicMemberBinding>> bindings)
        {
            _bindings = bindings;
        }

        public bool TryGetBindings(Type type, out IReadOnlyList<PolymorphicMemberBinding> bindings)
        {
            if (_bindings.TryGetValue(type, out var found))
            {
                bindings = found;
                return found.Count > 0;
            }

            bindings = Empty;
            return false;
        }
    }
}
