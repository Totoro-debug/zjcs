"""Recover original actor material-property locations for installed native models."""
import json
from pathlib import Path
import sys

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / '.tools/python-packages'))
from UnityPy.classes import PPtr
from current_component_schemas import CurrentSchemas
from official_apk import Apk, ROOT
from prepare_battle_scene_appearance_runtime import identity, meta, write_changed


def main():
    apk, schemas = Apk.current(), CurrentSchemas()
    inventory = ROOT / 'unity/Assets/Official/Combat/NativeMonsters/inventory.json'
    models = json.loads(inventory.read_bytes())['models']
    rows, sources, omitted = [], {}, []

    def record(obj):
        from hashlib import sha256
        sources[obj.assets_file.name + ':' + str(obj.path_id)] = dict(
            source=apk.provenance(obj), payload_sha256=sha256(obj.get_raw_data()).hexdigest())
        return obj

    def location(go):
        if isinstance(go, PPtr):
            go = go.deref()
        go = record(go)
        transform, = [ref.component.deref() for ref in go.read().m_Component if ref.component.deref().type.name == 'Transform']
        value = record(transform).read()
        parent = location(value.m_Father.read().m_GameObject) if value.m_Father.path_id else ''
        return parent + '/' + go.read().m_Name if parent else go.read().m_Name

    try:
        assert apk.version == '884_156692'
        for entity in sorted({3100} | {row['entityId'] for row in models}):
            families = [family for family in ('fightcharacter', 'fightmonster', 'fightpet')
                if f'defaultpackage_assets_prefabs_ecentity_fight_{family}_{entity}.bundle' in apk.index]
            if not families:
                omitted.append(dict(entityId=entity, reason='Installed RT actor has no matching fight-prefab family.'))
                continue
            assert len(families) == 1, ('Review original actor prefab family.', entity)
            properties = [obj for obj in apk.objects(f'prefabs_ecentity_fight_{families[0]}_{entity}')
                if obj.type.name == 'MonoBehaviour'
                and obj.read(check_read=False).m_Script.read().m_ClassName == 'MeshMaterialProperty']
            if not properties:
                omitted.append(dict(entityId=entity, reason='Original fight prefab has no material-property component.'))
                continue
            prop, = properties
            value = schemas.read(record(prop))
            root = str(entity)
            prop_path = location(prop.read(check_read=False).m_GameObject)
            assert prop_path == root, ('Review original actor property root.', entity, prop_path)
            row = dict(entityId=entity, enabled=bool(value['m_Enabled']))
            for field, name in [('MeshRenderer', 'renderer'), ('mAppearanceRenderer', 'appearance'), ('mAppearanceRendererLite', 'appearanceLite')]:
                ptr = value[field]
                row[name] = None
                if ptr['m_PathID']:
                    obj = record(PPtr(assetsfile=prop.assets_file, **ptr).deref())
                    path = location(obj.read(check_read=False).m_GameObject)
                    assert path.startswith(root + '/'), ('Review original actor material reference.', entity, path)
                    row[name] = path[len(root) + 1:]
            if row['renderer'] is None:
                omitted.append(dict(entityId=entity, reason='Original MeshRenderer is null; appearance-only material collection requires a separate binding.'))
                continue
            rows.append(row)
    finally:
        apk.close()
    output = ROOT / 'unity/Assets/Game/Resources/Combat/actor-material-bindings.json'
    write_changed(output, json.dumps(dict(manifest='884_156692', actors=rows), ensure_ascii=False, indent=2) + '\n')
    meta(output)
    runtime = ROOT / 'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleActorMaterialBinding.cs'
    meta(runtime)
    proof = dict(manifest='884_156692', sources=sources, schemas=schemas.records,
        inputs=[identity(inventory)], outputs=[identity(output), identity(Path(str(output) + '.meta'))],
        generator=identity(Path(__file__)), bindings=[identity(runtime), identity(Path(str(runtime) + '.meta'))], omitted=omitted,
        operation='Restore original root MeshMaterialProperty enable state and exact renderer/appearance paths. Existing original appearance components retain independent-material collection. Native skeleton backends without that appearance component expose their existing per-actor material instances through the original MeshRenderer reference; no replacement shader or material is generated.',
        limits=['No claim that all original appearance loaders, attached world actors, RT actors or uninstalled NPC prefabs are restored.'])
    receipt = ROOT / 'assets/reference/battle-animation/actor-material-bindings-source-manifest.json'
    write_changed(receipt, json.dumps(proof, ensure_ascii=False, indent=2) + '\n')
    print(f'Recovered original material bindings for {len(rows)} actors; {len(omitted)} explicit exclusions.')


if __name__ == '__main__':
    main()
