"""Compile the production story module and test through its caller interface."""
import argparse
import json
import os
from pathlib import Path
import subprocess

from validation_workspace import temporary_validation_directory

ROOT = Path(__file__).resolve().parents[1]


def run(editor, output):
    runtime = max((editor / 'Data/NetCoreRuntime/shared/Microsoft.NETCore.App').glob('6.*'),
                  key=lambda p: tuple(map(int, p.name.split('.'))))
    dotnet = editor / 'Data/NetCoreRuntime/dotnet.exe'
    target = output / 'StoryModuleRules.dll'
    references = ['System.Private.CoreLib', 'System.Runtime', 'System.Console', 'System.Collections']
    sources = [ROOT / 'unity/Assets/Game/Runtime/Offline/Story/StoryModule.cs',
               ROOT / 'scripts/fixtures/story_module_harness.cs']
    response = output / 'compile.rsp'
    response.write_text('\n'.join(['-target:exe', '-langversion:9.0', '-nostdlib+',
        '-out:"' + str(target) + '"'] + ['-r:"' + str(runtime / (r + '.dll')) + '"' for r in references]
        + ['"' + str(p) + '"' for p in sources]), encoding='utf-8')
    result = subprocess.run([str(dotnet), str(editor / 'Data/DotNetSdkRoslyn/csc.dll'), '@' + str(response)],
                            capture_output=True, text=True, encoding='utf-8', errors='replace')
    if result.returncode:
        raise RuntimeError(result.stdout + result.stderr)
    target.with_suffix('.runtimeconfig.json').write_text(json.dumps(dict(runtimeOptions=dict(
        tfm='net6.0', framework=dict(name='Microsoft.NETCore.App', version=runtime.name)))), encoding='utf-8')
    result = subprocess.run([str(dotnet), str(target)], capture_output=True, text=True,
                            encoding='utf-8', errors='replace')
    print(result.stdout, end='')
    if result.returncode:
        raise RuntimeError(result.stderr)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--editor', type=Path, default=Path('D:/Tool/Unity/Editor'))
    args = parser.parse_args()
    if not os.environ.get('ZJCS_VALIDATION_DIR') or Path(os.environ.get('ZJCS_REPOSITORY_ROOT', '')).resolve() != ROOT:
        parser.error('Run scripts/test_story_module.ps1 to create an owned validation workspace.')
    with temporary_validation_directory('compiled-') as temporary:
        run(args.editor.resolve(), Path(temporary))
