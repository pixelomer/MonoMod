# Horizon MonoMod integration

Based on upstream commit 14b9f28a04f9281fb032cb1d1e2339305b734869.
MonoMod is MIT (LICENSE); upstream's iced submodule is MIT at
c50f29b7bc305696895c075f3fc7719751426b12. Preserve upstream source/licenses.
No Nintendo SDK or proprietary derivative is an input.

The opt-in SDK compiler selection supports source generators that require the
runtime bundled with the selected .NET SDK. The Horizon hosting requirements
for Hook/ILHook and native memory access are described below.

## Desktop control build

Use the selected .NET SDK compiler for matching SDK source generators. The
upstream compiler package runs on .NET 9 and can retain its older dependencies
when using .NET 10 generators. MMUseSdkCompiler is an opt-in build selection;
it does not disable generators or change Hook APIs.

```
git submodule update --init external/iced
dotnet build src/MonoMod.RuntimeDetour/MonoMod.RuntimeDetour.csproj \
  -c Release -f net10.0 -p:RoslynVersion=5.0.0 -p:MMUseSdkCompiler=true \
  -p:UseSharedCompilation=false -p:ArtifactsPath=ABSOLUTE_OUTPUT_DIRECTORY
```

## Horizon integration boundaries

Use the existing ISystem/IMemoryAllocator/IRuntime boundaries. Horizon requires
real memory queries, owned RW/RX aliases, cache publication and native exception
helper integration. CoreCLR is embedded, so locate getJit through the embedding
contract rather than pretending a clrjit.so file exists. Retain .NET 10's exact
JIT GUID and private-layout checks. Test recompilation, generic/virtual dispatch,
callback ABI and hook lifetime; merely generating ARM64 branch bytes is not
sufficient. A native bridge should reuse the runtime's ExecutableAllocator and
PAL ownership/protection machinery where possible.
