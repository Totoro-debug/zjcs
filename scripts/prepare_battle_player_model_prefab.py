"""Restore the verified current three-node Game player prefab with native renderer ports."""
import copy
import hashlib
import json
from pathlib import Path

import yaml
from UnityPy.classes import PPtr
from current_component_schemas import CurrentSchemas
from official_apk import Apk, ROOT
from prepare_battle_scene_appearance_runtime import identity, meta, write_changed
from prepare_unity_profession_character_resources import guid
from prepare_battle_game_animators import documents, verify_graph, GUID


DEST = ROOT / 'unity/Assets/Official/Combat/SceneCharacterAppearance/Resources/Combat/SceneAppearance/PlayerModel_Game.prefab'
OFFICIAL = ROOT / 'unity/Assets/Game/Runtime/Official'
OFFLINE = ROOT / 'unity/Assets/Game/Runtime/Offline/ProfessionUI'
SCRIPTS = {
    'Spine.Unity.SkeletonMecanim': OFFLINE / 'ProfessionNativeSceneRenderer.cs',
    'AppearanceSystem.Runtime.AppearanceMecanim': OFFLINE / 'ProfessionNativeSceneAppearance.cs',
    'AppearanceSystem.Runtime.AppearanceCharacter': OFFICIAL / 'CombatAppearance/AppearanceCharacter.cs',
    'AppearanceSystem.PlayerAnimatorAppearance': OFFICIAL / 'CombatAppearance/PlayerAnimatorAppearance.cs',
    'AppearanceRendererPartHandler': OFFICIAL / 'ProfessionUI/AppearanceRendererPartHandler.cs',
    'AppearanceSystem.Runtime.AppearanceSkinClip': OFFICIAL / 'ProfessionUI/AppearanceSkinClip.cs',
    'PreviewTool': OFFICIAL / 'BattleSkillPlayables/PreviewTool.cs',
    'AppearanceSystem.SkeletonClipAttachment': OFFICIAL / 'CombatAppearance/SkeletonClipAttachment.cs',
    'AppearanceSystem.SpineExt.SkeletonRendererWeaponClipSet': OFFICIAL / 'CombatAppearance/SkeletonRendererWeaponClipSet.cs',
}


def restore_default_controller(apk, original):
    cache = ROOT / '.tools/source-packages/current/884_156692/native-conversion'
    receipt_path = cache / 'native-export-all-source-manifest.json'
    receipt = json.loads(receipt_path.read_bytes())
    assert receipt['completed'] and receipt['manifest'] == apk.version
    assert identity(cache / 'source-manifest.json')['sha256'] == receipt['input_manifest_sha256']
    export = cache / receipt['export_directory']
    base = export / 'ExportedProject/Assets/AppearanceAssets/Character/Model/Female_1/Anim'
    native = base / (original.read().m_Name + '.controller')
    checked = {row['path']: row['sha256'] for row in receipt['outputs']}
    index = {GUID.search(p.read_text(encoding='utf-8'))[1]: p.with_suffix('') for p in base.rglob('*.meta')}
    queue, closure = [native], set()
    while queue:
        path = queue.pop()
        if path in closure: continue
        closure.add(path)
        for digest in GUID.findall(path.read_text(encoding='utf-8')): queue.append(index[digest])
    for path in closure:
        for item in (path, path.with_name(path.name + '.meta')):
            assert identity(item)['sha256'] == checked[item.relative_to(export).as_posix()]
    names = {GUID.search(p.with_name(p.name + '.meta').read_text(encoding='utf-8'))[1]:
        next(iter(documents(p.read_bytes()).values()))['m_Name'] for p in closure}
    converted = documents(native.read_bytes())
    compiled = original.read_typetree()['m_Controller']
    if not original.read().m_AnimationClips:
        # This actual prefab reference is an empty base, not a Game controller.
        assert not compiled['m_Values']['data']['m_ValueArray']
        assert len(compiled['m_LayerArray']) == len(converted[9100000]['m_AnimatorLayers']) == 1
        machine = compiled['m_StateMachineArray'][0]['data']
        layer = converted[9100000]['m_AnimatorLayers'][0]
        native_machine = converted[layer['m_StateMachine']['fileID']]
        assert not machine['m_StateConstantArray'] and not machine['m_AnyStateTransitionConstantArray']
        assert machine['m_DefaultState'] == 0
        assert not converted[9100000]['m_AnimatorParameters']
        assert not native_machine['m_ChildStates'] and not native_machine['m_AnyStateTransitions']
        assert native_machine['m_DefaultState'] == dict(fileID=0)
        assert layer['m_Name'] == dict(original.read_typetree()['m_TOS'])[compiled['m_LayerArray'][0]['data']['m_Binding']]
        graph = dict(empty=True, layers=1, states=0, clips=0, parameters=0, default_state=None)
    else:
        graph = verify_graph(original, converted, names)
    originals = {('AnimatorController', original.read().m_Name): original}
    for pointer in original.read().m_AnimationClips:
        obj = pointer.deref()
        key = ('AnimationClip', obj.read().m_Name)
        assert key not in originals or originals[key].get_raw_data() == obj.get_raw_data()
        originals[key] = obj
    dest = DEST.parent / 'InitialAnimator'
    remap = {GUID.search(p.with_name(p.name + '.meta').read_text(encoding='utf-8'))[1]:
        hashlib.sha256(('zjcs:initial-player-animator:' + p.relative_to(base).as_posix()).encode()).hexdigest()[:32] for p in closure}
    outputs, inputs = [], []
    for path in sorted(closure):
        fields = next(iter(documents(path.read_bytes()).values()))
        obj = originals['AnimationClip' if path.suffix == '.anim' else 'AnimatorController', fields['m_Name']]
        if path.suffix == '.anim':
            assert abs(fields['m_AnimationClipSettings']['m_StopTime'] - obj.read().m_MuscleClip.m_StopTime) < .000001
        target = dest / path.relative_to(base)
        target.parent.mkdir(parents=True, exist_ok=True)
        for first, last in ((path, target), (path.with_name(path.name + '.meta'), target.with_name(target.name + '.meta'))):
            text = GUID.sub(lambda m: 'guid: ' + remap[m[1]], first.read_text(encoding='utf-8'))
            text = '\n'.join(line.rstrip() for line in text.splitlines()) + '\n'
            write_changed(last, text); outputs.append(last)
        inputs.append(dict(native=identity(path), source=apk.provenance(obj), payload_sha256=hashlib.sha256(obj.get_raw_data()).hexdigest()))
    for parent in {p.parent for p in outputs}:
        while parent.is_relative_to(dest):
            meta(parent, folder=True); outputs.append(parent.with_name(parent.name + '.meta')); parent = parent.parent
    return dest / native.relative_to(base), dict(native_receipt=identity(receipt_path), graph=graph, inputs=inputs), sorted(set(outputs))


def main():
    source = ROOT / 'unity/Assets/Official/Combat/SceneCharacterSources/PlayerModel_Game-source.json'
    hierarchy = json.loads(source.read_bytes())
    core = ROOT / 'assets/reference/battle-animation/scene-core-source-manifest.json'
    assert json.loads(core.read_bytes())['hierarchy'] == identity(source)
    apk, schemas = Apk.current(), CurrentSchemas()
    assert apk.version == '884_156692'
    objects = {str(o.path_id): o for o in apk.objects('defaultpackage_assets_prefabs_appearance_player.bundle')}
    inputs = []
    controller = None
    try:
        for node in hierarchy['nodes']:
            for row in [node['game_object'], *node['components']]:
                obj = objects[row['source']['path_id']]
                assert hashlib.sha256(obj.get_raw_data()).hexdigest() == row['payload_sha256']
                fields = schemas.read(obj) if obj.type.name == 'MonoBehaviour' else obj.read_typetree()
                assert fields == row['fields']
                inputs.append(dict(source=apk.provenance(obj), payload_sha256=row['payload_sha256']))
                if obj.type.name == 'Animator':
                    target = PPtr(**fields['m_Controller'], assetsfile=obj.assets_file).deref()
                    controller = dict(name=target.read().m_Name, source=apk.provenance(target),
                        payload_sha256=hashlib.sha256(target.get_raw_data()).hexdigest())
                    controller_path, controller_proof, controller_outputs = restore_default_controller(apk, target)
    finally:
        apk.archive.close()
    assert len(hierarchy['nodes']) == 3 and len(inputs) == 18
    controller['binding'] = identity(controller_path)
    export_receipt = controller_proof['native_receipt']
    native_receipt = json.loads((ROOT / export_receipt['path']).read_bytes())
    export = ROOT / '.tools/source-packages/current/884_156692/native-conversion' / native_receipt['export_directory']
    native_prefab = export / 'ExportedProject/Assets/Prefabs/Appearance/Player/PlayerModel_Game.prefab'
    assert identity(native_prefab)['sha256'] == next(row['sha256'] for row in native_receipt['outputs']
        if row['path'] == native_prefab.relative_to(export).as_posix())
    native_root = next(row for row in documents(native_prefab.read_bytes()).values()
        if row.get('m_Name') == 'PlayerModel_Game' and 'm_Component' in row)
    assert native_root['m_TagString'] == 'Character'
    mesh_script = OFFLINE / 'ProfessionNativeSceneMeshPart.cs'
    own_paths = [OFFICIAL / 'CombatAppearance' / (name + '.cs') for name in (
        'ISkeletonClipSet', 'SkeletonClipAttachment', 'SkeletonRendererWeaponClipSet')]
    own_paths += [OFFLINE / 'OfflineProfessionSceneSkinClipBindings.cs']
    for path in own_paths: meta(path)

    all_ids = {int(row['source']['path_id']) for node in hierarchy['nodes'] for row in [node['game_object'], *node['components']]}
    def pointers(value):
        if isinstance(value, dict):
            if set(value) == {'m_FileID', 'm_PathID'}:
                assert not value['m_FileID'] and (not value['m_PathID'] or value['m_PathID'] in all_ids), value
                return dict(fileID=value['m_PathID'])
            return {key: pointers(item) for key, item in value.items()}
        if isinstance(value, list): return [pointers(item) for item in value]
        return int(value) if isinstance(value, bool) else value
    def script(path): return dict(fileID=11500000, guid=guid(path), type=3)
    def common(go):
        return dict(m_ObjectHideFlags=0, m_CorrespondingSourceObject=dict(fileID=0),
            m_PrefabInstance=dict(fileID=0), m_PrefabAsset=dict(fileID=0), m_GameObject=dict(fileID=go))
    docs = []
    backend_id = 114000000000000001
    assert backend_id not in all_ids
    for index, node in enumerate(hierarchy['nodes']):
        go = int(node['game_object']['source']['path_id'])
        fields = copy.deepcopy(node['game_object']['fields'])
        tag = fields.pop('m_Tag')
        assert tag == (20018 if index == 0 else 0)
        fields['m_TagString'] = 'Character' if tag else 'Untagged'
        fields = pointers(fields)
        if index == 0: fields['m_Component'].append(dict(component=dict(fileID=backend_id)))
        docs.append((1, go, 'GameObject', dict(serializedVersion=6, **{k:v for k,v in common(go).items() if k != 'm_GameObject'}, **fields)))
        for row in node['components']:
            kind = row['type']
            fields = copy.deepcopy(row['fields'])
            if kind in SCRIPTS: fields.pop('m_Script')
            if kind == 'Animator': fields.pop('m_Controller')
            if kind == 'Spine.Unity.SkeletonMecanim':
                # SDK drawing fields remain losslessly bound through the source hierarchy asset.
                fields = {k:v for k,v in fields.items() if k in ('m_GameObject', 'm_Enabled')}
            fields = {**common(go), **pointers(fields)}
            if kind in SCRIPTS:
                fields['m_Script'] = script(SCRIPTS[kind])
                class_id, name = 114, 'MonoBehaviour'
            else:
                class_id, name = dict(Transform=(4, 'Transform'), MeshFilter=(33, 'MeshFilter'),
                    MeshRenderer=(23, 'MeshRenderer'), Animator=(95, 'Animator'))[kind]
            if kind == 'Animator': fields['m_Controller'] = dict(fileID=9100000, guid=guid(controller_path), type=2)
            docs.append((class_id, int(row['source']['path_id']), name, fields))
        if index == 0:
            docs.append((114, backend_id, 'MonoBehaviour', dict(**common(go), m_Enabled=1, m_Script=script(mesh_script))))
    text = '%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n'
    for kind, obj_id, name, fields in docs:
        text += f'--- !u!{kind} &{obj_id}\n' + yaml.safe_dump({name: fields}, sort_keys=False, allow_unicode=True, width=120)
    write_changed(DEST, text); meta(DEST)
    code = ROOT / '.tools/source-packages/current/884_156692/official-code'
    code_receipt = code.parent / 'code-manifest.json'
    assembly = code / 'managed/decoded/Assembly-CSharp.dll'
    assert identity(assembly)['sha256'] == json.loads(code_receipt.read_bytes())['assemblies'][assembly.name]['decoded_sha256']
    application = [code / 'client-source' / path for path in ('ISkeletonClipSet.cs',
        'AppearanceSystem/SkeletonClipAttachment.cs', 'AppearanceSystem.SpineExt/SkeletonRendererWeaponClipSet.cs')]
    outputs = [DEST, DEST.with_name(DEST.name + '.meta'), *controller_outputs]
    for path in own_paths: outputs.extend((path, path.with_name(path.name + '.meta')))
    outputs += [OFFLINE / 'ProfessionNativeAttachment.cs', OFFLINE / 'OfflineProfessionScenePartPoolBindings.cs',
        OFFLINE / 'ProfessionNativeSceneRenderer.Parts.cs']
    validation = ROOT / 'unity/Assets/Game/Editor/Combat/AnimatorPlayback/OfficialBattlePlayerModelClipValidation.cs'
    if validation.exists():
        meta(validation); outputs.extend((validation, validation.with_name(validation.name + '.meta')))
    for path in outputs:
        if path.suffix == '.meta':
            write_changed(path, '\n'.join(line.rstrip() for line in path.read_text(encoding='utf-8').splitlines()) + '\n')
    proof = dict(manifest=apk.version, hierarchy=identity(source), source_receipt=identity(core), inputs=inputs,
        application=[identity(path) for path in application], code_receipt=identity(code_receipt), assembly=identity(assembly),
        controller=controller, controller_conversion=controller_proof, native_prefab=identity(native_prefab),
        script_bindings={name:identity(path) for name,path in SCRIPTS.items()},
        backend=identity(mesh_script), outputs=[identity(path) for path in outputs], generator=identity(Path(__file__).resolve()),
        adaptations=['Retain every source node, component order, transform, enabled value and local reference using original path IDs. Add only the independent native mesh endpoint to the root.',
            'Replace the SDK renderer/Mecanim with the existing independent native endpoints. Complete renderer settings remain in the verified source hierarchy consumed by the native renderer. No SDK implementation is copied.',
            'Port the original ground-attachment/material application branches to existing follower/renderer ports and a clipping-attachment type identity. Serialize the appearance reference as Component at the native boundary and cast to the original application type on use; validate the imported prefab reference. Preserve no-weapon, independent-material, fallback-material, particle, world-transform, cache and destruction branches.',
            'Bind original head clipping via actor-owned scene appearance ports captured at Awake. Preserve original Game controller reference; player appearance selects the correct current controller before skin refresh.',
            'Resolve native part-pool targets through inactive parents as well, so all original application/renderer ports can be configured before activating the complete player prefab.'],
        binding='Combat/SceneAppearance/PlayerModel_Game',
        limits=['A caller must configure resources, player appearance, pose/physics/mixing, independent parts and skin-clip Awake bindings before activation. This prefab alone does not connect production battles or prove full Windows Player skill presentation.'])
    write_changed(ROOT / 'assets/reference/battle-animation/player-model-prefab-source-manifest.json', json.dumps(proof, ensure_ascii=False, indent=2) + '\n')
    print('Restored current complete three-node Game player prefab and original ground/weapon clipping application ports.')


if __name__ == '__main__': main()
