#!/usr/bin/env python3
"""Build MonoMod libraries for Horizon .NET 10."""
import argparse, json, os, subprocess, sys
from pathlib import Path
ROOT = Path(__file__).resolve().parent
sys.path.insert(0, str(ROOT / 'eng/horizon'))
from support import digest, read_mirrors, run, submodules
p = argparse.ArgumentParser(description=__doc__)
p.add_argument('--output', type=Path, default=ROOT / 'artifacts/horizon')
p.add_argument('--framework', choices=['net8.0', 'net10.0'], default='net8.0')
p.add_argument('--dotnet', default='dotnet')
p.add_argument('--source-mirrors', type=Path)
p.add_argument('--fetch-only', action='store_true')
a = p.parse_args()
submodules(ROOT, read_mirrors(a.source_mirrors))
if a.fetch_only: sys.exit(0)
out = a.output.resolve()
for project in ['MonoMod.RuntimeDetour', 'MonoMod.Patcher', 'MonoMod.RuntimeDetour.HookGen']:
    run([a.dotnet, 'build', ROOT / 'src' / project / (project + '.csproj'), '-c', 'Release', '-f', a.framework,
         '-p:RoslynVersion=5.0.0', '-p:MMUseSdkCompiler=true', '-p:UseSharedCompilation=false',
         '-p:ArtifactsPath=' + str(out), '-p:NuGetLockFilePath=packages.horizon.lock.json'], cwd=ROOT)
files = {str(f.relative_to(out)): digest(f) for f in (out / 'bin').rglob('*.dll')}
(out / 'manifest.json').write_text(json.dumps({'framework': a.framework, 'runtime': 'Horizon CoreCLR .NET 10',
    'sdk': subprocess.check_output([a.dotnet, '--version'], cwd=ROOT, text=True).strip(),
    'revision': subprocess.check_output(['git', '-C', ROOT, 'rev-parse', 'HEAD'], text=True).strip(),
    'assemblies': files}, indent=2) + '\n')
print(out)
