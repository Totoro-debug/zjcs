"""Compare actual arc-shield incoming hits with the pinned original Damage body."""
import argparse
import hashlib
import json
import subprocess
from pathlib import Path

import prepare_battle_damage_reference as original
from validation_workspace import validation_directory


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--unity-core', type=Path, default=Path(
        'D:/Tool/Unity/Editor/Data/Managed/UnityEngine/UnityEngine.CoreModule.dll'))
    args = parser.parse_args()
    work = validation_directory()
    assert work.parent == original.ROOT / 'output/validation'
    assert (work / '.zjcs-validation.json').is_file()
    cases = json.loads((work / 'arc-damage-cases.json').read_text(encoding='utf-8-sig'))
    assert len(cases) == 54
    reference = original.reference_source()
    # Reuse the existing reviewed original-code identities; never silently accept
    # a different decompilation or Unity arithmetic implementation.
    proof = json.loads((original.ROOT / 'assets/reference/battle-rules/damage-reference.json').read_bytes())
    pinned = {item['path']: item['sha256'] for item in proof['original_code']}
    for path in original.USED:
        identity = original.identity(path)
        assert pinned[identity['path']] == identity['sha256'], identity['path']
    assert hashlib.sha256(args.unity_core.read_bytes()).hexdigest() == proof['unity_core_sha256']
    rules = json.loads((original.CONFIG / 'rules.json').read_bytes())
    battle = json.loads((original.CONFIG / 'battle-rules.json').read_bytes())
    for item in proof['input']:
        assert original.identity(original.ROOT / item['path'])['sha256'] == item['sha256'], item['path']
    source = work / 'arc-original-damage-reference.cs'
    source.write_text(reference, encoding='utf-8')
    inputs = work / 'arc-original-damage-input.json'
    inputs.write_text(json.dumps(dict(cases=cases, configuration=dict(
        game_settings=rules['game_settings'], prop_cfg=rules['prop_cfg'], tables=battle['tables']))), encoding='utf-8')
    def quote(path):
        return str(path).replace("'", "''")
    runner = work / 'arc-original-damage-runner.ps1'
    runner.write_text("$ErrorActionPreference='Stop'\n[Reflection.Assembly]::LoadFrom('" + quote(args.unity_core) + "') | Out-Null\n"
        "$refs=@((Get-ChildItem (Join-Path $PSHOME 'ref') -Filter '*.dll').FullName) + @('" + quote(args.unity_core) + "')\n"
        "Add-Type -Path '" + quote(source) + "' -ReferencedAssemblies $refs\n"
        "[SourceDamageReference.Reference]::Evaluate([IO.File]::ReadAllText('" + quote(inputs) + "'))\n", encoding='utf-8')
    result = subprocess.run(['pwsh', '-NoProfile', '-File', str(runner)],
                            capture_output=True, text=True, encoding='utf-8')
    (work / 'arc-original-damage.log').write_text(result.stdout + result.stderr, encoding='utf-8')
    if result.returncode:
        raise RuntimeError(result.stderr[-6000:])
    lines = result.stdout.splitlines()
    assert len(lines) == len(cases), (len(lines), len(cases))
    for case, line in zip(cases, lines):
        expected = json.loads(line)
        assert expected['damage'] == case['actualDamage'], (case['name'], expected, case)
        assert expected['absolute'] == case['actualAbsolute'], (case['name'], expected, case)
        case['original'] = expected
    (work / 'arc-damage-comparison.json').write_text(json.dumps(cases, indent=2), encoding='utf-8')
    print('Arc incoming damage: 54 original Damage comparisons passed, including active/expired block, avoidance and misses.')


if __name__ == '__main__':
    main()
