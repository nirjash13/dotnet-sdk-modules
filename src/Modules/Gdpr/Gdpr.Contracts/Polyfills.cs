#if NETSTANDARD2_0
// Records require System.Runtime.CompilerServices.IsExternalInit on netstandard2.0.
// This polyfill satisfies the compiler without adding a runtime dependency.
// Block-scoped namespace is intentional — file-scoped namespace cannot be used inside #if.
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit { }
}
#endif
