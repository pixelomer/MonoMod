using System;

namespace MonoMod.Core.Platforms
{
    // Optional hosting contract for a statically linked CoreCLR JIT. Systems
    // without this contract keep ordinary clrjit module discovery unchanged.
    internal interface IEmbeddedJitSystem
    {
        IntPtr GetJitObject();
        IntPtr GetCompileMethod();
        bool TrySetCompileMethodHook(IntPtr expected, IntPtr callback);
    }
}
