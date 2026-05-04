// Polyfill so we can use `init` accessors / records on netstandard2.1.
// Removed automatically by net5+ runtimes which already define this type.
#if !NET5_0_OR_GREATER
namespace System.Runtime.CompilerServices
{
    using System.ComponentModel;

    [EditorBrowsable(EditorBrowsableState.Never)]
    internal static class IsExternalInit { }
}
#endif
