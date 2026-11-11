"""Port the two empty original airborne client wrappers onto the existing status view boundary."""
import json
import re
from pathlib import Path

from official_apk import ROOT
from prepare_battle_scene_appearance_runtime import identity, meta, write_changed


def main():
    code = ROOT / '.tools/source-packages/current/884_156692/official-code'
    manifest = json.loads((code.parent / 'code-manifest.json').read_bytes())
    assembly = identity(code / 'managed/decoded/Assembly-CSharp.dll')
    assert manifest['manifest'] == '884_156692'
    assert assembly['sha256'] == manifest['assemblies']['Assembly-CSharp.dll']['decoded_sha256']
    dest = ROOT / 'unity/Assets/Game/Runtime/Official/BattleGridItemEntities'
    inputs, outputs = [], []
    for name in ('MyFightStatusDamageRemove', 'MyFightStatusDamageSkill'):
        original = code / 'client-source' / (name + '.cs')
        text = original.read_text(encoding='utf-8-sig')
        assert re.fullmatch(r'using Common;\s*public class ' + name
            + r' : MyFightStatusBase<\w+, \w+>\s*\{\s*\}\s*', text)
        text = text.replace('using Common;\n', '')
        text = re.sub(r'MyFightStatusBase<\w+, \w+>', 'MyFightStatusBase<IBattleGridComponentViewData>', text)
        text = ('// Original empty client wrapper; see airborne runtime source receipt.\n'
            'namespace Zjcs.Official.BattleGridItemEntities\n{\n' + text.strip() + '\n}\n')
        path = dest / (name + '.cs')
        write_changed(path, text); meta(path)
        inputs.append(identity(original))
        outputs.extend((identity(path), identity(path.with_name(path.name + '.meta'))))
    receipt = ROOT / 'assets/reference/battle-animation/air-combo-runtime-source-manifest.json'
    write_changed(receipt, json.dumps(dict(manifest='884_156692', original_assembly=assembly,
        original_code=inputs, outputs=outputs, generator=identity(Path(__file__)),
        existing_base=identity(dest / 'MyFightStatusBase.cs'),
        operation='Preserve both literally empty original client classes and their MyFightStatusBase '
            'inheritance; replace unavailable server component generic arguments with the existing '
            'host-owned view-data boundary. Gameplay seconds live in the confirmed offline executor.'),
        ensure_ascii=False, indent=2) + '\n')


if __name__ == '__main__':
    main()
