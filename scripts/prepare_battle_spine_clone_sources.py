"""Read clone renderer settings from the selected official bundle, not defaults."""
import hashlib
import json
from pathlib import Path

from UnityPy.classes import PPtr
from current_component_schemas import CurrentSchemas
from official_apk import Apk, ROOT
from prepare_battle_scene_appearance_runtime import identity, meta, write_changed


def main():
    apk = Apk.current()
    assert apk.version == '884_156692'
    schemas = CurrentSchemas()
    bundle = 'defaultpackage_assets_prefabs_ecentity_fight_fightskill_13100.bundle'
    targets = {}
    for obj in apk.objects(bundle):
        if obj.type.name != 'MonoBehaviour':
            continue
        script = obj.read(check_read=False).m_Script.read()
        if script.m_ClassName == 'MyPlayableSpineClone' and not script.m_Namespace:
            fields = schemas.read(obj)
            pointer = PPtr(**fields['SkeletonAnimation'], assetsfile=obj.assets_file).deref()
            targets[pointer.path_id] = pointer
    assert len(targets) == 5, len(targets)

    def record(obj, fields):
        return dict(source=apk.provenance(obj), object_sha256=hashlib.sha256(obj.get_raw_data()).hexdigest(), fields=fields)

    inputs, nodes = [], []
    for target in targets.values():
        fields = schemas.read(target)
        go = PPtr(**fields['m_GameObject'], assetsfile=target.assets_file).deref()
        mesh = next(ref.component.deref() for ref in go.read().m_Component
                    if ref.component.deref().type.name == 'MeshRenderer')
        mesh_fields = mesh.read_typetree()
        inputs.extend((record(target, fields), record(mesh, mesh_fields), record(go, go.read_typetree())))
        # The settings loader does not consume ownership pointers; retain the
        # originals in the receipt and compare every remaining serialized field.
        def settings(value):
            return {key: item for key, item in value.items() if key != 'm_GameObject'}
        nodes.append(dict(name=go.read().m_Name, components=[
            dict(type='Spine.Unity.SkeletonAnimation', fields=settings(fields)),
            dict(type='MeshRenderer', fields=settings(mesh_fields))]))
    assert all(node == nodes[0] for node in nodes), 'Review differing original clone renderers individually.'
    target = ROOT / 'unity/Assets/Game/Resources/Combat/SkillClones/renderer-settings.json'
    target.parent.mkdir(parents=True, exist_ok=True)
    meta(target.parent, folder=True)
    write_changed(target, json.dumps(dict(manifest=apk.version, nodes=[nodes[0]]), ensure_ascii=False, indent=2) + '\n')
    meta(target)
    receipt = ROOT / 'assets/reference/battle-animation/spine-clone-settings-source-manifest.json'
    write_changed(receipt, json.dumps(dict(manifest=apk.version, inputs=inputs, schemas=schemas.records,
        outputs=[identity(target), identity(Path(str(target) + '.meta'))], generator=identity(Path(__file__)),
        operation='Resolve all five original MyPlayableSpineClone target renderers and verify identical settings '
            'after excluding only owner pointers. Preserve original mesh, skeleton and clock settings.'),
        ensure_ascii=False, indent=2) + '\n')
    apk.archive.close()
    print('Verified five original clone renderers and packaged their exact shared settings.')


if __name__ == '__main__':
    main()
