# Horizon MonoMod integration

> [!IMPORTANT]
> This fork contains AI-assisted changes. Most of the work was done by
> GPT-6 Astra. The produced code was not audited or verified by a human beyond
> running it and confirming that it works as expected. Human maintainability or
> readability was not a goal for this project.
>
> THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
> IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
> FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
> AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
> LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
> OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
> SOFTWARE.

Based on upstream commit 14b9f28a04f9281fb032cb1d1e2339305b734869.
MonoMod is MIT (LICENSE); upstream's iced submodule is MIT at
c50f29b7bc305696895c075f3fc7719751426b12. Preserve upstream source/licenses.
No Nintendo SDK or proprietary derivative is an input.

The integration supports runtime Hook/ILHook and assembly loading on
[Horizon CoreCLR .NET 10](https://github.com/pixelomer/dotnet-runtime), built via
[dotnet-switch](https://github.com/pixelomer/dotnet-switch). Applications can
reference the net8 assemblies when embedding that runtime.

## Standalone build

Install Python 3.12+, Git and the .NET 10.0.1xx SDK on Linux x86-64. Run:

```sh
python3 build-horizon.py
```

The script fetches the exact iced submodule, then builds RuntimeDetour, Patcher
and HookGen in Release using SDK Roslyn 5.0.0. Outputs are under
`artifacts/horizon/`; individual DLLs are in `bin/PROJECT/release_net8.0/`.
`--framework net10.0` builds a .NET 10 consumer variant. Native helper generation
uses the upstream pinned tool dependencies fetched during restore. To link a
Horizon host, compile `native/libnx/exception-helper.c` and the existing ARM64
assembly helper
`src/MonoMod.Core/Platforms/Architectures/arm64/exhelper_linux_macos_arm64.S`
into the NRO, registering the runtime bridge exports.

`--source-mirrors JSON` maps canonical URLs to Git source mirrors. Generated
Horizon restore locks are separate from upstream package locks.

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

The source-build helpers' recursive source-fetch controls can be run with
`python3 tests/horizon/test_sources.py`; these tests create only temporary,
original Git fixtures and do not require a console or game files.
