"""Recover original persistent monster particle prefabs and their Windows closure."""
from pathlib import Path
import json
import re
from official_apk import ROOT
from prepare_battle_status_orbit_assets import build
from prepare_battle_game_animators import documents
from prepare_battle_scene_appearance_runtime import identity
from prepare_battle_monster_embedded_particles import particle_subtrees


def embedded_overrides():
    cache = ROOT / '.tools/source-packages/current/884_156692/native-conversion'
    source = cache / 'native-export-all/ExportedProject/Assets/Prefabs/EcEntity/Fight/FightMonster/3803.prefab'
    receipt = ROOT / 'assets/reference/battle-animation/monster-body-effects-source-manifest.json'
    prior = json.loads(receipt.read_bytes())
    assert identity(cache / 'native-export-all-source-manifest.json') == prior['native_export_receipt']
    expected = prior['embedded_instance']['sha256']
    assert identity(source)['sha256'] == expected
    text = source.read_text(encoding='utf-8-sig')

    def indexed(raw):
        docs = documents(raw.encode())
        transforms = {key: value for key, value in docs.items() if 'm_Father' in value}
        def path(key):
            row = transforms[key]
            parent = row['m_Father']['fileID']
            return (path(parent) + '/' if parent in transforms else '') + str(docs[row['m_GameObject']['fileID']]['m_Name'])
        paths = {row['m_GameObject']['fileID']: path(key) for key, row in transforms.items()}
        blocks = re.findall(r'(?ms)^--- !u!(\d+) &(\d+)[^\n]*\n.*?(?=^--- !u!|\Z)', raw)
        raw_blocks = re.findall(r'(?ms)^--- !u!\d+ &\d+[^\n]*\n.*?(?=^--- !u!|\Z)', raw)
        return [(kind, paths.get(docs[int(key)].get('m_GameObject', {}).get('fileID')), block)
                for (kind, key), block in zip(blocks, raw_blocks)]

    originals = indexed(text)
    overrides = []
    def adapt(raw, path):
        if path.suffix != '.prefab' or not path.stem.startswith('Eff_Npc_ChaosMonarch_'): return raw
        for kind, name, block in indexed(raw):
            if kind not in ('4', '198') or name is None: continue
            candidates = [value for source_kind, source_name, value in originals
                if source_kind == kind and source_name and source_name.endswith('/' + name)]
            assert len(candidates) == 1, (path, name)
            for field in (('m_LocalPosition', 'm_LocalRotation', 'm_LocalScale') if kind == '4' else ('InitialModule',)):
                pattern = r'(?ms)^  ' + field + r':.*?(?=^  \w[^\n]*:|^--- !u!|\Z)'
                old, = re.findall(pattern, block)
                new, = re.findall(pattern, candidates[0])
                if old != new:
                    changed = block.replace(old, new)
                    raw = raw.replace(block, changed)
                    block = changed
                    overrides.append(dict(prefab=path.stem, node=name, field=field))
        return raw
    return adapt, source, overrides


def main():
    adapt, source, overrides = embedded_overrides()
    embedded = {}
    def native(raw, path):
        if path == source:
            raw, graph = particle_subtrees(raw)
            embedded.update(graph)
            return raw
        return adapt(raw, path)
    names = ["Eff_Npc_ChaosMonarch_" + name for name in (
        "LeftWings01", "LeftWings01_1", "RightWings01", "RightWings01_1")]
    graphs = {name: 'Effect/prefabs/Monster/Npc_ChaosMonarch/' + name + '.prefab' for name in names}
    graphs['3803'] = 'Prefabs/EcEntity/Fight/FightMonster/3803.prefab'
    build(DEST=ROOT / 'unity/Assets/Official/Combat/MonsterBodyEffects/Resources/Combat/MonsterBodyEffects',
        DATA=ROOT / 'unity/Assets/Game/Resources/Combat/monster-body-effects.json',
        RECEIPT=ROOT / 'assets/reference/battle-animation/monster-body-effects-source-manifest.json',
        ROOTS={}, MAPPED={}, family='MonsterBodyEffects', shader_prefix='MonsterBodyEffect_',
        graph_roots=graphs,
        generator=Path(__file__), runtime_receipts=(),
        native_preprocess=native,
        operation='Restore four complete first-phase wing prefabs with verified entity-instance overrides, '
            'and the nine exact second-phase particle subtrees selected from the verified complete 3803 entity. '
            'Preserve selected local objects and native particle fields; retain only their original transform '
            'ancestors and component/child references. Resolve original native dependency assets through '
            'the hash-matched Windows texture/shader cache. Mesh copies and application followers remain '
            'bound through the separately recorded original entity attachment graph.',
        limits=['This resource family restores persistent body particles; action effects and other entity components are bound separately.'])
    data = ROOT / 'unity/Assets/Game/Resources/Combat/monster-body-effects.json'
    config = json.loads(data.read_bytes())
    next(row for row in config['graphs'] if row['id'] == '3803')['embeddedRootPaths'] = embedded['root_paths']
    data.write_bytes((json.dumps(config, ensure_ascii=False, indent=2) + '\n').encode())
    receipt = ROOT / 'assets/reference/battle-animation/monster-body-effects-source-manifest.json'
    proof = json.loads(receipt.read_bytes())
    proof['runtime_receipts'] = []
    proof['embedded_instance'] = identity(source)
    proof['embedded_particle_graph'] = embedded
    proof['embedded_particle_generator'] = identity(Path(__file__).with_name('prepare_battle_monster_embedded_particles.py'))
    next(row for row in proof['outputs'] if row['path'] == data.relative_to(ROOT).as_posix())['sha256'] = identity(data)['sha256']
    proof['embedded_overrides'] = list({(row['prefab'], row['node'], row['field']): row for row in overrides}.values())
    proof['runtime_bindings'] = [identity(ROOT / name) for name in (
        'unity/Assets/Game/Runtime/Offline/ProfessionUI/BattleNativeMonsterRenderer.Attachments.cs',
        'unity/Assets/Game/Runtime/Offline/ProfessionUI/PartnerNativeBattleAttachments.cs',
        'unity/Assets/Game/Editor/ProfessionUI/OfficialBattleMonsterSceneValidation.Selected.cs',
        'unity/Assets/Game/Editor/ProfessionUI/Zjcs.Editor.ProfessionUI.asmdef')]
    receipt.write_bytes((json.dumps(proof, ensure_ascii=False, indent=2) + '\n').encode())


if __name__ == '__main__':
    main()
