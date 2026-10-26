"""Resolve the original UIParticle object reference; decode its body separately."""
from pathlib import Path
import subprocess
from official_apk import ROOT


def load(schemas, cecil_path=None):
    folder=ROOT/'.tools/home-particle-schema';folder.mkdir(exist_ok=True)
    target=folder/'Coffee.UIParticle.dll'
    if not target.exists():
        cecil = Path(cecil_path) if cecil_path else next(iter(sorted(
            (ROOT / '.tools').glob('*/tools/net*/any/Mono.Cecil.dll'))), None)
        if cecil is None or not cecil.is_file():
            raise FileNotFoundError('Mono.Cecil.dll is required to build the UI object-reference schema')
        script=folder/'build.ps1'
        script.write_text(f'''$ErrorActionPreference = 'Stop'
Add-Type -Path '{cecil.as_posix()}'
$homeSchema = [Mono.Cecil.AssemblyDefinition]::ReadAssembly('{ROOT.as_posix()}/.tools/unity-schema/UnityEngine.CoreModule.dll')
$homeBase = $homeSchema.MainModule.Types | Where-Object FullName -eq 'UnityEngine.MonoBehaviour'
$homeParticle = [Mono.Cecil.TypeDefinition]::new('Coffee.UIExtensions','UIParticle',[Mono.Cecil.TypeAttributes]::Public,$homeBase)
$homeSchema.MainModule.Types.Add($homeParticle)
$homeSchema.Name.Name = 'Coffee.UIParticle'
$homeSchema.MainModule.Name = 'Coffee.UIParticle.dll'
$homeSchema.Write('{target.as_posix()}')
$homeSchema.Dispose()
''',encoding='utf8')
        subprocess.run(['pwsh','-NoProfile','-File',str(script)],check=True)
    schemas.generator.load_dll(target.read_bytes())
