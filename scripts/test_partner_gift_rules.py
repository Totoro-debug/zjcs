"""Compile the migrated C# rules and exercise official gift selection edge cases."""
import json
import os
from pathlib import Path
import subprocess
from validation_workspace import temporary_validation_directory

ROOT = Path(__file__).resolve().parents[1]

def run(output):
    editor = Path('D:/Tool/Unity/Editor')
    runtime = max((editor / 'Data/NetCoreRuntime/shared/Microsoft.NETCore.App').glob('6.*'))
    target = output / 'PartnerGifts.dll'
    references = [runtime / (name + '.dll') for name in
                  ('System.Private.CoreLib', 'System.Runtime', 'System.Collections', 'System.Console', 'System.Linq')]
    sources = [ROOT / 'unity/Assets/Game/Runtime/Official/Partners/NpcGiftRules.cs',
               ROOT / 'scripts/fixtures/partner_gift_rules_harness.cs']
    response = output / 'compile.rsp'
    response.write_text('\n'.join(['-target:exe', '-langversion:9', '-nostdlib+', f'-out:"{target}"']
        + [f'-r:"{path}"' for path in references] + [f'"{path}"' for path in sources]), encoding='utf8')
    dotnet = editor / 'Data/NetCoreRuntime/dotnet.exe'
    subprocess.run([str(dotnet), str(editor / 'Data/DotNetSdkRoslyn/csc.dll'), '@' + str(response)], check=True)
    target.with_suffix('.runtimeconfig.json').write_text(json.dumps(dict(runtimeOptions=dict(
        tfm='net6.0', framework=dict(name='Microsoft.NETCore.App', version=runtime.name)))), encoding='utf8')
    subprocess.run([str(dotnet), str(target)], check=True)

if __name__ == '__main__':
    if not os.environ.get('ZJCS_VALIDATION_DIR') or Path(os.environ.get('ZJCS_REPOSITORY_ROOT', '')).resolve() != ROOT:
        raise RuntimeError('Set ZJCS_VALIDATION_DIR and ZJCS_REPOSITORY_ROOT for an owned validation workspace.')
    with temporary_validation_directory('partner-rules-') as directory:
        run(Path(directory))
