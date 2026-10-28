"""Compile actual invitation dependencies and exercise original NPC/comparer behavior."""
from __future__ import annotations

import argparse
import json
import os
from pathlib import Path
import re
import subprocess

from validation_workspace import temporary_validation_directory

ROOT = Path(__file__).resolve().parents[1]


def run(editor, output):
    dotnet = editor / 'Data/NetCoreRuntime/dotnet.exe'
    compiler = editor / 'Data/DotNetSdkRoslyn/csc.dll'
    cached = list((ROOT / 'unity/Library/Bee/artifacts').glob('*/Zjcs.Official.StoryPlayables.rsp'))
    if not cached:
        raise FileNotFoundError('A cached Unity C# response file is required; this check does not start Unity.')
    options = []
    for line in max(cached, key=lambda p: p.stat().st_mtime).read_text().splitlines():
        if not line or re.match(r'^[/-](out|refout|analyzer|additionalfile):|^".*\.cs"$', line):
            continue
        if re.match(r'^[/-]r:.*Library/', line):
            continue
        options.append(line)

    definitions, guids, compiled = {}, {}, {}
    for path in (ROOT / 'unity/Assets/Game').rglob('*.asmdef'):
        value = json.loads(path.read_bytes())
        definitions[value['name']] = (path.parent, value)
        match = re.search(r'^guid: (\w+)', path.with_name(path.name + '.meta').read_text(), re.M)
        if match:
            guids[match[1]] = value['name']

    def compile_rsp(name, lines):
        response = output / (name + '.rsp')
        response.write_bytes(('\n'.join(lines) + '\n').encode())
        result = subprocess.run([str(dotnet), str(compiler), '@' + str(response)],
                                cwd=ROOT / 'unity', capture_output=True)
        (output / (name + '.log')).write_bytes(result.stdout + result.stderr)
        if result.returncode:
            raise RuntimeError(name + ':\n' + (result.stdout + result.stderr).decode('utf-8', errors='replace'))

    def compile_module(name):
        if name in compiled:
            return compiled[name]
        if name not in definitions:
            path = ROOT / 'unity/Library/ScriptAssemblies' / (
                ('UnityEngine.UI' if name == 'Unity.ugui' else name) + '.dll')
            if not path.is_file():
                raise FileNotFoundError('Missing cached external reference ' + str(path))
            return path
        directory, config = definitions[name]
        references = [compile_module(guids.get(ref[5:], ref) if ref.startswith('GUID:') else ref)
                      for ref in config.get('references', [])]
        sources = []
        for path in sorted(directory.rglob('*.cs')):
            owner = path.parent
            while owner != directory and not list(owner.glob('*.asmdef')) and not list(owner.glob('*.asmref')):
                owner = owner.parent
            if owner == directory:
                sources.append(path)
        target = output / (name + '.dll')
        compile_rsp(name, options + ['-out:"' + str(target) + '"']
                    + ['-r:"' + str(p) + '"' for p in references] + ['"' + str(p) + '"' for p in sources])
        compiled[name] = target
        print(name, len(sources), 'sources compiled', flush=True)
        return target

    compile_module('Zjcs.Official.TeamUI')
    versions = list((editor / 'Data/NetCoreRuntime/shared/Microsoft.NETCore.App').glob('6.*'))
    if not versions:
        raise FileNotFoundError('The editor-bundled .NET 6 runtime is required.')
    runtime = max(versions, key=lambda p: tuple(int(x) for x in p.name.split('.')))
    references = [runtime / (name + '.dll') for name in (
        'System.Private.CoreLib', 'System.Runtime', 'System.Console', 'System.Collections', 'System.Linq',
        'System.Text.Json', 'System.Memory', 'System.Runtime.Loader', 'System.Threading.Tasks', 'netstandard')]
    references += list(compiled.values()) + [editor / 'Data/Managed/UnityEngine/UnityEngine.CoreModule.dll']
    target = output / 'Map12TeamInviteRules.dll'
    compile_rsp('Map12TeamInviteRules', ['-target:exe', '-langversion:9.0', '-nostdlib+',
                '-out:"' + str(target) + '"'] + ['-r:"' + str(p) + '"' for p in references]
                + ['"' + str(ROOT / 'scripts/fixtures/map12_team_invite_rules_harness.cs') + '"'])
    target.with_suffix('.runtimeconfig.json').write_bytes(json.dumps(dict(runtimeOptions=dict(
        tfm='net6.0', framework=dict(name='Microsoft.NETCore.App', version=runtime.name)))).encode())
    subprocess.run([str(dotnet), str(target), str(ROOT), str(output), str(editor)], cwd=ROOT, check=True)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--editor', type=Path, default=Path('D:/Tool/Unity/Editor'))
    args = parser.parse_args()
    if not os.environ.get('ZJCS_VALIDATION_DIR') or Path(os.environ.get('ZJCS_REPOSITORY_ROOT', '')).resolve() != ROOT:
        parser.error('Run scripts/test_map12_team_invite_rules.ps1 to create an owned validation workspace.')
    with temporary_validation_directory('compiled-') as temporary:
        run(args.editor.resolve(), Path(temporary))
