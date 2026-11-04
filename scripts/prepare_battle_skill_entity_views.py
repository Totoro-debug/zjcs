"""Extract only the original MyFightSkill view inputs; does not infer server entities."""
import json
import re
from hashlib import sha256
from pathlib import Path

from inspect_battle_skill_graph_bindings import EXPORT, EXPORT_RECEIPT, ROOT, script_file_id
from prepare_battle_scene_appearance_runtime import identity, meta, write_changed

DATA = ROOT / 'unity/Assets/Game/Resources/Combat/skill-entity-views.json'
RECEIPT = ROOT / 'assets/reference/battle-animation/skill-entity-views-source-manifest.json'


def main():
    inputs = json.loads(EXPORT_RECEIPT.read_text(encoding='utf-8'))
    verified = {item['path']: item['sha256'] for item in inputs['outputs']}
    records, sources = {}, []
    for path in sorted(EXPORT.glob('[0-9]*/[0-9]*.prefab')):
        key = path.relative_to(EXPORT.parents[5]).as_posix()
        if sha256(path.read_bytes()).hexdigest() != verified[key]:
            raise ValueError(f'Official prefab hash differs from export receipt: {path}')
        text = path.read_text(encoding='utf-8-sig')
        for component in re.findall(r'(?ms)^--- !u!114 &\d+\nMonoBehaviour:\n(.*?)(?=^--- !u!|\Z)', text):
            if not re.search(rf'(?m)^  m_Script: \{{fileID: {script_file_id()},', component):
                continue
            skill_id = int(path.stem)
            if skill_id in records:
                raise ValueError(f'Duplicate official skill entity: {skill_id}')
            def flag(name):
                match = re.search(rf'(?m)^  {name}: ([01])$', component)
                if match is None:
                    raise ValueError(f'Missing serialized {name}: {path}')
                return match[1] == '1'
            records[skill_id] = dict(skillEntityId=skill_id,
                preferredPosToPlayable=flag('PreferredPosToPlayable'),
                showSkillReplay=flag('ShowSkillReplay'))
            sources.append(identity(path))
    write_changed(DATA, json.dumps(dict(manifest='884_156692', skills=list(records.values())),
                                  ensure_ascii=False, indent=2) + '\n')
    meta(DATA)
    source = ROOT / '.tools/source-packages/current/884_156692/official-code/client-source/MyFightSkill.cs'
    proof = dict(manifest='884_156692', generator=identity(Path(__file__)), source=identity(source),
                 native_export_manifest=identity(EXPORT_RECEIPT), inputs=sources,
                 outputs=[identity(DATA), identity(DATA.with_name(DATA.name + '.meta'))],
                 limits=['Serialized MyFightSkill view flags only. Server skill creation, other '
                         'entity components and production graph acceptance are separate.'])
    write_changed(RECEIPT, json.dumps(proof, ensure_ascii=False, indent=2) + '\n')
    print('Original MyFightSkill view configurations:', len(records))


if __name__ == '__main__':
    main()
