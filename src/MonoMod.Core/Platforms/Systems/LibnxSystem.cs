using MonoMod.Core.Platforms.Memory;
using MonoMod.Core.Utils;
using MonoMod.Utils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace MonoMod.Core.Platforms.Systems
{
    internal sealed class LibnxSystem : ISystem, IInitialize<IArchitecture>, IEmbeddedJitSystem
    {
        public OSKind Target => OSKind.Libnx;
        public SystemFeature Features => SystemFeature.RXPages;
        public Abi? DefaultAbi { get; } = new Abi(
            new[] { SpecialArgumentKind.ThisPointer, SpecialArgumentKind.UserArguments },
            SystemVABI.ClassifyARM64, false);
        public IMemoryAllocator MemoryAllocator { get; } = new Allocator();
        public INativeExceptionHelper? NativeExceptionHelper { get; private set; }

        void IInitialize<IArchitecture>.Initialize(IArchitecture architecture)
        {
            if (architecture.Target != ArchitectureKind.Arm64)
                throw new PlatformNotSupportedException("The libnx backend requires ARM64");
            NativeExceptionHelper = new ExceptionHelper(architecture);
        }

        public IntPtr GetJitObject() => Native.GetJit();
        public IntPtr GetCompileMethod() => Native.GetCompileCallback();
        public bool TrySetCompileMethodHook(IntPtr expected, IntPtr callback) => Native.SetCompileCallback(expected, callback) != 0;

        // PAL currently supports lookup of resident modules, but no public
        // enumeration snapshot. The embedded JIT path does not require one.
        public IEnumerable<LoadedModule> EnumerateLoadedModules()
            => throw new PlatformNotSupportedException("Resident module enumeration is not exposed by this Horizon host");
        public IEnumerable<string?> EnumerateLoadedModuleFiles()
            => throw new PlatformNotSupportedException("Resident module enumeration is not exposed by this Horizon host");
        public IntPtr GetNativeJitHookConfig(int runtimeMajMin) => IntPtr.Zero;

        public nint GetSizeOfReadableMemory(IntPtr start, nint guess)
            => guess <= 0 ? 0 : (nint)Native.Readable(start, (nuint)guess);

        public unsafe void PatchData(PatchTargetKind targetKind, IntPtr patchTarget, ReadOnlySpan<byte> data, Span<byte> backup)
        {
            if (!backup.IsEmpty && backup.Length < data.Length)
                throw new ArgumentException("Backup is shorter than the patch", nameof(backup));
            fixed (byte* source = data)
            fixed (byte* previous = backup)
            {
                if (Native.Patch(patchTarget, source, previous, (nuint)data.Length) == 0)
                    throw new InvalidOperationException("Horizon memory patch or protection restoration failed");
            }
        }

        private sealed class ExceptionHelper : PosixExceptionHelper
        {
            public ExceptionHelper(IArchitecture architecture) : base(architecture, GetEntry(0), GetEntry(1), GetEntry(2)) { }
            private static IntPtr GetEntry(int index)
            {
                var entry = Native.ExceptionHelper(index);
                if (entry == IntPtr.Zero)
                    throw new PlatformNotSupportedException("The ARM64 native exception helper is not linked into the host");
                return entry;
            }
        }

        private sealed class Allocator : PagedMemoryAllocator
        {
            private readonly ConcurrentDictionary<IntPtr, IntPtr> handles = new();
            public Allocator() : base((nint)Native.Granularity()) { }

            private bool Allocate(AllocationRequest request, IntPtr low, IntPtr high,
                [MaybeNullWhen(false)] out IAllocatedMemory allocated)
            {
                allocated = null;
                if (Native.Allocate((nuint)PageSize, request.Executable ? 1 : 0, low, high, out var handle, out var address) == 0)
                    return false;
                var page = new Page(this, address, (uint)PageSize, request.Executable);
                if (!handles.TryAdd(address, handle))
                {
                    Native.Free(handle);
                    throw new InvalidOperationException("Host returned a duplicate live allocation");
                }
                InsertAllocatedPage(page);
                if (page.TryAllocate((uint)request.Size, (uint)request.Alignment, out var result))
                {
                    allocated = result;
                    return true;
                }
                RegisterForCleanup(page);
                return false;
            }

            protected override bool TryAllocateNewPage(AllocationRequest request, [MaybeNullWhen(false)] out IAllocatedMemory allocated)
                => Allocate(request, IntPtr.Zero, IntPtr.Zero, out allocated);

            protected override bool TryAllocateNewPage(PositionedAllocationRequest request,
                nint targetPage, nint lowPageBound, nint highPageBound, [MaybeNullWhen(false)] out IAllocatedMemory allocated)
                => Allocate(request.Base, lowPageBound, highPageBound, out allocated);

            protected override bool TryFreePage(Page page, [NotNullWhen(false)] out string? errorMsg)
            {
                if (!handles.TryRemove(page.BaseAddr, out var handle))
                {
                    errorMsg = "Missing Horizon allocation handle";
                    return false;
                }
                Native.Free(handle);
                errorMsg = null;
                return true;
            }
        }

        private static class Native
        {
            private const string Library = "__Internal";
            [DllImport(Library, EntryPoint = "coreclr_libnx_get_jit", CallingConvention = CallingConvention.Cdecl)]
            internal static extern IntPtr GetJit();
            [DllImport(Library, EntryPoint = "coreclr_libnx_jit_get_compile_callback", CallingConvention = CallingConvention.Cdecl)]
            internal static extern IntPtr GetCompileCallback();
            [DllImport(Library, EntryPoint = "coreclr_libnx_jit_set_compile_callback", CallingConvention = CallingConvention.Cdecl)]
            internal static extern int SetCompileCallback(IntPtr expected, IntPtr callback);
            [DllImport(Library, EntryPoint = "coreclr_libnx_memory_granularity", CallingConvention = CallingConvention.Cdecl)]
            internal static extern nuint Granularity();
            [DllImport(Library, EntryPoint = "coreclr_libnx_memory_allocate", CallingConvention = CallingConvention.Cdecl)]
            private static extern unsafe int AllocateCore(nuint size, int executable, IntPtr low, IntPtr high, IntPtr* handle, IntPtr* address);
            internal static unsafe int Allocate(nuint size, int executable, IntPtr low, IntPtr high, out IntPtr handle, out IntPtr address)
            {
                IntPtr allocatedHandle, allocatedAddress;
                int result = AllocateCore(size, executable, low, high, &allocatedHandle, &allocatedAddress);
                handle = allocatedHandle;
                address = allocatedAddress;
                return result;
            }
            [DllImport(Library, EntryPoint = "coreclr_libnx_memory_free", CallingConvention = CallingConvention.Cdecl)]
            internal static extern void Free(IntPtr handle);
            [DllImport(Library, EntryPoint = "coreclr_libnx_memory_readable", CallingConvention = CallingConvention.Cdecl)]
            internal static extern nuint Readable(IntPtr address, nuint size);
            [DllImport(Library, EntryPoint = "coreclr_libnx_memory_patch", CallingConvention = CallingConvention.Cdecl)]
            internal static extern unsafe int Patch(IntPtr address, byte* data, byte* backup, nuint size);
            [DllImport(Library, EntryPoint = "monomod_libnx_exception_helper", CallingConvention = CallingConvention.Cdecl)]
            internal static extern IntPtr ExceptionHelper(int index);
        }
    }
}
