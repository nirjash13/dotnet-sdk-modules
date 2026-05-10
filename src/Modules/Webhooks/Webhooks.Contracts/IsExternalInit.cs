// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// This polyfill enables C# record types and init-only properties on netstandard2.0 targets.
// The compiler synthesizes calls to this type for 'init' accessors; without it, netstandard2.0
// builds fail with CS0518. On net5.0+, the runtime provides this type natively.
#if !NET5_0_OR_GREATER
namespace System.Runtime.CompilerServices;

internal static class IsExternalInit
{
}
#endif
