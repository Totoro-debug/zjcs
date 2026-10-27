"""Compile the C# achievement migration and exercise original-config boundary fixtures."""
from pathlib import Path
import subprocess
import tempfile
import shutil
import json
import re
import struct
from xml.sax.saxutils import escape
ROOT = Path(__file__).resolve().parents[1]

def managed(path):
    raw = path.read_bytes()
    offset = struct.unpack_from('<I', raw, 0x3c)[0] + 24
    magic = struct.unpack_from('<H', raw, offset)[0]
    directories = offset + (112 if magic == 0x20b else 96)
    return bool(struct.unpack_from('<I', raw, directories + 14 * 8)[0])

def main():
    cache = ROOT / '.tools'
    with tempfile.TemporaryDirectory(prefix='profession-achievement-tests-', dir=cache) as temporary:
        work = Path(temporary)
        sources = [ROOT / 'unity/Assets/Game/Runtime/Offline/Progression/OfficialProfessionAchievementRules.cs',
                   ROOT / 'scripts/fixtures/profession_achievements_harness.cs']
        includes = ''.join('<Compile Include="' + escape(str(path), {'"': '&quot;'}) + '" />' for path in sources)
        project = '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><EnableDefaultCompileItems>false</EnableDefaultCompileItems></PropertyGroup><ItemGroup>' + includes + '</ItemGroup></Project>'
        (work / 'ProfessionAchievements.csproj').write_text(project, encoding='utf-8')
        dotnet = shutil.which('dotnet')
        if not dotnet:
            raise FileNotFoundError('The installed .NET runtime was not found.')
        sdk = subprocess.check_output([dotnet, '--list-sdks'], text=True).strip()
        if sdk:
            subprocess.run([dotnet, 'run', '--project', str(work / 'ProfessionAchievements.csproj'), '--',
                            str(ROOT / 'assets/official/rules.json')], cwd=ROOT, check=True)
        else:
            # Reuse Unity's Roslyn compiler and installed .NET runtime. No SDK download.
            editor = Path('D:/Tool/Unity/Editor/Data')
            compiler = editor / 'DotNetSdkRoslyn/csc.dll'
            compiler_host = editor / 'NetCoreRuntime/dotnet.exe'
            runtimes = subprocess.check_output([dotnet, '--list-runtimes'], text=True)
            candidates = re.findall(r'^Microsoft.NETCore.App (\S+) \[(.*?)\]$', runtimes, re.M)
            version, root = max(candidates, key=lambda pair: tuple(map(int, pair[0].split('.'))))
            runtime = Path(root) / version
            output = work / 'ProfessionAchievements.dll'
            arguments = [str(compiler_host), str(compiler), '/nologo', '/noconfig', '/nostdlib+',
                         '/target:exe', '/out:' + str(output)]
            arguments.extend('/r:' + str(path) for path in sorted(runtime.glob('*.dll')) if managed(path))
            arguments.extend(str(path) for path in sources)
            compilation = subprocess.run(arguments, cwd=ROOT)
            if compilation.returncode: raise SystemExit(compilation.returncode)
            config = {'runtimeOptions': {'tfm': 'net' + version.split('.')[0] + '.0',
                      'framework': {'name': 'Microsoft.NETCore.App', 'version': version}}}
            output.with_suffix('.runtimeconfig.json').write_text(json.dumps(config), encoding='utf-8')
            subprocess.run([dotnet, str(output), str(ROOT / 'assets/official/rules.json')], cwd=ROOT, check=True)


if __name__ == '__main__':
    main()
