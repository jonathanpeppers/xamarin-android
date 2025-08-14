using System;
using System.Threading;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Android.Runtime;

internal static class RuntimeTypeHandleAccessor
{
    // https://github.com/dotnet/runtime/blob/a2ba99899d196987a819fa714a9cb9efc6c64990/src/mono/System.Private.CoreLib/src/System/RuntimeTypeHandle.cs#L349
    [UnsafeAccessor (UnsafeAccessorKind.StaticMethod, Name = "GetTypeByName")]
    [return: UnsafeAccessorType ("System.RuntimeType")]
    [return: DynamicallyAccessedMembers (DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.NonPublicMethods | DynamicallyAccessedMemberTypes.NonPublicNestedTypes)]
    static extern Type? GetTypeByName (
        Type declaringType,
        string typeName,
        bool throwOnError,
        bool ignoreCase,
        [UnsafeAccessorType ("System.Threading.StackCrawlMark")]
        ref int stackMark);

    [return: DynamicallyAccessedMembers (DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.NonPublicMethods | DynamicallyAccessedMemberTypes.NonPublicNestedTypes)]
    public static Type? GetType (string typeName, bool throwOnError = false, bool ignoreCase = false)
    {
        int stackMark = 1; // https://github.com/dotnet/runtime/blob/a2ba99899d196987a819fa714a9cb9efc6c64990/src/libraries/System.Private.CoreLib/src/System/Threading/StackCrawlMark.cs#L14
        return GetTypeByName (typeof (RuntimeTypeHandle), typeName, throwOnError, ignoreCase, ref stackMark);
    }
}
