"""Exercise migrated conversation state against the actual selected node graph."""
import json
import os
from pathlib import Path
import subprocess

from validation_workspace import temporary_validation_directory, validation_directory

ROOT = Path(__file__).resolve().parents[1]


def run(output):
    editor = Path('D:/Tool/Unity/Editor')
    runtime = max((editor / 'Data/NetCoreRuntime/shared/Microsoft.NETCore.App').glob('6.*'))
    target = output / 'PartnerConversations.dll'
    # Runtime implementation is pure C#; all framework references belong to the
    # editor's bundled .NET runtime rather than developer-machine SDK packages.
    references = [runtime / (name + '.dll') for name in ('System.Private.CoreLib',
        'System.Runtime', 'System.Collections', 'System.Console', 'System.Linq',
        'System.Text.Json', 'System.Memory')]
    sources = [ROOT / 'unity/Assets/Game/Runtime/Official/Partners/PartnerConversationRules.cs',
        ROOT / 'unity/Assets/Game/Runtime/Offline/Progression/OfficialPartnerConversationCatalog.cs',
        ROOT / 'unity/Assets/Game/Runtime/Offline/Equipment/OfficialEquipmentJson.cs',
        ROOT / 'scripts/fixtures/partner_conversation_rules_harness.cs']
    response = output / 'compile.rsp'
    response.write_text('\n'.join(['-target:exe', '-langversion:9', '-nostdlib+', f'-out:"{target}"']
        + [f'-r:"{path}"' for path in references] + [f'"{path}"' for path in sources]), 'utf8')
    dotnet = editor / 'Data/NetCoreRuntime/dotnet.exe'
    subprocess.run([str(dotnet), str(editor / 'Data/DotNetSdkRoslyn/csc.dll'), '@' + str(response)], check=True)
    target.with_suffix('.runtimeconfig.json').write_text(json.dumps(dict(runtimeOptions=dict(
        tfm='net6.0', framework=dict(name='Microsoft.NETCore.App', version=runtime.name)))), 'utf8')
    subprocess.run([str(dotnet), str(target), str(validation_directory() / 'partner-dialogue-rules.json')], check=True)


if __name__ == '__main__':
    if not os.environ.get('ZJCS_VALIDATION_DIR') or Path(os.environ.get('ZJCS_REPOSITORY_ROOT', '')).resolve() != ROOT:
        raise RuntimeError('Set the owned validation workspace and repository root.')
    with temporary_validation_directory('partner-conversations-') as directory:
        run(Path(directory))
