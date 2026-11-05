"""Recover original persistent monster particle prefabs and their Windows closure."""
from pathlib import Path
import json
import re
from official_apk import ROOT
from prepare_battle_status_orbit_assets import build
from prepare_battle_game_animators import documents
from prepare_battle_scene_appearance_runtime import identity


def embedded_overrides():
    cache = ROOT / '.tools/source-packages/current/884_156692/native-conversion'
    source = cache / 'native-export-all/ExportedProject/Assets/Prefabs/EcEntity/Fight/FightMonster/3803.prefab'
    proof = json.loads((cache / 'native-export-all-source-manifest.json').read_bytes())
    expected = next(row['sha256'] for row in proof['outputs']
        if row['path'] == source.relative_to(cache / 'native-export-all').as_posix())
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
    names = ["Eff_Npc_ChaosMonarch_" + name for name in (
        "LeftWings01", "LeftWings01_1", "RightWings01", "RightWings01_1")]
    build(DEST=ROOT / 'unity/Assets/Official/Combat/MonsterBodyEffects/Resources/Combat/MonsterBodyEffects',
        DATA=ROOT / 'unity/Assets/Game/Resources/Combat/monster-body-effects.json',
        RECEIPT=ROOT / 'assets/reference/battle-animation/monster-body-effects-source-manifest.json',
        ROOTS={}, MAPPED={}, family='MonsterBodyEffects', shader_prefix='MonsterBodyEffect_',
        graph_roots={name: 'Effect/prefabs/Monster/Npc_ChaosMonarch/' + name + '.prefab' for name in names},
        generator=Path(__file__), runtime_receipts=(),
        native_preprocess=adapt,
        limits=['Four original first-phase wing particle prefabs are bound to 3803 with original embedded instance overrides; other embedded effects and components remain separate.'])
    receipt = ROOT / 'assets/reference/battle-animation/monster-body-effects-source-manifest.json'
    proof = json.loads(receipt.read_bytes())
    proof['runtime_receipts'] = []
    proof['embedded_instance'] = identity(source)
    proof['embedded_overrides'] = list({(row['prefab'], row['node'], row['field']): row for row in overrides}.values())
    proof['runtime_bindings'] = [identity(ROOT / name) for name in (
        'unity/Assets/Game/Runtime/Offline/ProfessionUI/BattleNativeMonsterRenderer.Attachments.cs',
        'unity/Assets/Game/Runtime/Offline/ProfessionUI/PartnerNativeBattleAttachments.cs',
        'unity/Assets/Game/Editor/ProfessionUI/OfficialBattleMonsterSceneValidation.Selected.cs',
        'unity/Assets/Game/Editor/ProfessionUI/Zjcs.Editor.ProfessionUI.asmdef')]
    receipt.write_bytes((json.dumps(proof, ensure_ascii=False, indent=2) + '\n').encode())


if __name__ == '__main__':
    main()
