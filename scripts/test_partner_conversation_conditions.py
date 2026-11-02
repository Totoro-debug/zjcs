"""Compile actual original dialogue condition rules and test their real input graph."""
import json
import os
from pathlib import Path
import subprocess

from validation_workspace import temporary_validation_directory

ROOT = Path(__file__).resolve().parents[1]


def run(output):
    editor = Path('D:/Tool/Unity/Editor')
    runtime = max((editor / 'Data/NetCoreRuntime/shared/Microsoft.NETCore.App').glob('6.*'))
    target = output / 'PartnerConversationConditions.dll'
    references = [runtime / (name + '.dll') for name in ('System.Private.CoreLib',
        'System.Runtime', 'System.Collections', 'System.Console', 'System.Linq')]
    runtime_root = ROOT / 'unity/Assets/Game/Runtime'
    sources = [runtime_root / 'Official/Partners' / (name + '.cs') for name in (
        'NpcRosterRules', 'PartnerConditionRules', 'PartnerConversationRules', 'PartnerConversationConditionRules')]
    sources += [runtime_root / 'Offline/Equipment/OfficialEquipmentJson.cs',
        runtime_root / 'Offline/Progression/OfficialPartnerConversationCatalog.cs',
        ROOT / 'scripts/fixtures/partner_conversation_conditions_harness.cs']
    response = output / 'compile.rsp'
    response.write_text('\n'.join(['-target:exe', '-langversion:9', '-nostdlib+', f'-out:"{target}"']
        + [f'-r:"{path}"' for path in references] + [f'"{path}"' for path in sources]), 'utf8')
    dotnet = editor / 'Data/NetCoreRuntime/dotnet.exe'
    subprocess.run([str(dotnet), str(editor / 'Data/DotNetSdkRoslyn/csc.dll'), '@' + str(response)], check=True)
    target.with_suffix('.runtimeconfig.json').write_text(json.dumps(dict(runtimeOptions=dict(
        tfm='net6.0', framework=dict(name='Microsoft.NETCore.App', version=runtime.name)))), 'utf8')
    base = ROOT / 'unity/Assets/Official/Partners'
    subprocess.run([str(dotnet), str(target), str(base / 'partner-rules.json'), str(base / 'partner-dialogue-rules.json')], check=True)


if __name__ == '__main__':
    if not os.environ.get('ZJCS_VALIDATION_DIR') or Path(os.environ.get('ZJCS_REPOSITORY_ROOT', '')).resolve() != ROOT:
        raise RuntimeError('Set the owned validation workspace and repository root.')
    with temporary_validation_directory('partner-conversation-conditions-') as directory:
        run(Path(directory))
