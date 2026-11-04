"""Bind selected original monster attachment registries and additional shadows."""
import hashlib
import json
from pathlib import Path

from official_apk import ROOT
from prepare_battle_character_shadows import main as shadows
from prepare_battle_scene_appearance_runtime import identity, meta, write_changed
from prepare_partner_native_battle_attachments import build
from validation_workspace import validation_directory


def main():
    work = validation_directory()
    rows = build(work, monsters=True)
    proof = json.loads((work / 'monster-native-attachments-source-manifest.json').read_bytes())
    target = ROOT / 'unity/Assets/Official/Combat/NativeMonsters/Resources/Combat/NativeMonsters/attachments.json'
    receipt = ROOT / 'assets/reference/battle-animation/monster-attachments-source-manifest.json'
    raw = (work / 'monster-native-attachments.json').read_bytes()
    if target.exists() and target.read_bytes() != raw:
        if not receipt.exists() or hashlib.sha256(target.read_bytes()).hexdigest() != json.loads(receipt.read_bytes())['output']['sha256']:
            raise ValueError('Preserve modified original monster attachment data')
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_bytes(raw)
    source = ROOT / 'unity/Assets/Game/Runtime/Offline/ProfessionUI/BattleNativeMonsterRenderer.Attachments.cs'
    for path in (target, source):
        if not Path(str(path) + '.meta').exists():
            meta(path)
    proof['output'] = identity(target)
    proof['installer'] = identity(Path(__file__).resolve())
    proof['bindings'] = [identity(path) for path in (source, Path(str(source) + '.meta'),
        ROOT / 'unity/Assets/Game/Runtime/Offline/ProfessionUI/PartnerNativeBattleAttachments.cs',
        ROOT / 'unity/Assets/Game/Runtime/Offline/ProfessionUI/BattleNativeMonsterRenderer.cs',
        ROOT / 'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleLiveMonsterModelView.cs')]
    write_changed(receipt, json.dumps(proof, ensure_ascii=False, indent=2) + '\n')

    existing_path = ROOT / 'unity/Assets/Game/Resources/Combat/character-shadow-bindings.json'
    existing = {row['prefab'] for row in json.loads(existing_path.read_bytes())['roles']}
    additions = []
    for row in rows:
        if str(row['entityId']) in existing:
            continue
        nodes = {node['sourceKey']: node for node in row['nodes']}
        shadow, = [node for node in nodes.values() if node['name'] == 'ShadowBoneFollower']
        path = []
        while shadow:
            path.insert(0, shadow['name'])
            shadow = nodes.get(shadow['parentKey'])
        additions.append(dict(prefab=str(row['entityId']), parts=['/'.join(path)]))
    destination = ROOT / 'unity/Assets/Official/Combat/NativeMonsters/Resources/Combat/NativeMonsters/Shadows'
    shadows(destination=destination,
        data=target.with_name('shadow-bindings.json'),
        receipt=ROOT / 'assets/reference/battle-animation/monster-additional-shadows-source-manifest.json',
        role_catalog=dict(manifest=proof['manifest'], roles=additions), catalog_source=target,
        resource_root='Combat/NativeMonsters/Shadows', generator=Path(__file__).resolve())


if __name__ == '__main__':
    main()
