using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace QuickMarkup.Infra;

static class SingletonWeakTable<T,
#if NET10_0_OR_GREATER
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
#endif
T2> where T : class where T2 : class
{
    internal static readonly ConditionalWeakTable<T, T2> Table = new();
}
