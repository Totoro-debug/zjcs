"""Recover application rotation-override inputs; do not read SDK implementation."""
import hashlib
import json
import re
from pathlib import Path

from current_component_schemas import CurrentSchemas
from official_apk import Apk
from official_manifest import read_asset_manifest
from prepare_battle_scene_appearance_runtime import ROOT, identity, meta, write_changed
from validation_workspace import validation_directory


def build():
    work = validation_directory()
    apk = Apk.current()
    assert apk.version == '884_156692'
    base = ROOT / '.tools/source-packages/current' / apk.version
    receipt_path = base / 'native-conversion/native-export-all-source-manifest.json'
    receipt = json.loads(receipt_path.read_bytes())
    export = receipt_path.parent / receipt['export_directory']
    rows = [r for r in receipt['outputs'] if '/Prefabs/Appearance/PlayerPart/pWeapon_' in r['path']
            and r['path'].endswith('_Integrated.prefab') and '_UI_' not in r['path']]
    assert len(rows) == 390
    selected = []
    for row in rows:
        raw = (export / row['path']).read_bytes()
        assert hashlib.sha256(raw).hexdigest() == row['sha256']
        if b'fileID: 1694013511,' in raw:
            selected.append(row)
    assert len(selected) == 15
    schemas = CurrentSchemas()
    table_raw = apk.source.manifest_bytes('DefaultPackage', apk.archive)
    table = {r['path']: r for r in read_asset_manifest(table_raw)}
    cases, inputs = [], []
    for row in selected:
        path = row['path'].removeprefix('ExportedProject/')
        item = table[path]
        roots = [ref.deref() for name, ref in apk.container(item['bundle']) if name == path and ref.deref().type.name == 'GameObject']
        assert len(roots) == 1
        nodes, overrides, skeletons = [], [], []

        def visit(obj, parent):
            go = obj.read()
            components, children = [], []
            for ref in go.m_Component:
                component = ref.component.deref()
                kind = component.type.name
                if kind == 'MonoBehaviour':
                    script = component.read(check_read=False).m_Script.read()
                    kind = (script.m_Namespace + '.' if script.m_Namespace else '') + script.m_ClassName
                    fields = schemas.read(component)
                else:
                    fields = component.read_typetree()
                inputs.append(dict(source=apk.provenance(component), payload_sha256=hashlib.sha256(component.get_raw_data()).hexdigest()))
                components.append(dict(type=kind, fields=fields))
                if kind == 'Transform': children.extend(component.read().m_Children)
                if kind == 'Spine.Unity.SkeletonAnimation': skeletons.append(fields)
            nodes.append(dict(name=go.m_Name, parent=parent, components=components))
            rot = next((c['fields'] for c in components if c['type'] == 'AppearanceSystem.SkeletonBoneRotOverride'), None)
            if rot is not None:
                locker = next(c['fields'] for c in components if c['type'] == 'TransformEulerZLocker')
                assert rot['RotMode'] == 1 and rot['UseDelta'] == 0 and locker['IsUI'] == 0
                overrides.append(dict(bone=rot['BoneName'], controller=bool(rot['Controller']['m_PathID']),
                    enabled=bool(rot['m_Enabled']), mode=rot['RotMode'], delta=bool(rot['UseDelta']),
                    world_z=locker['WorldEulerZ'], ui=bool(locker['IsUI'])))
            for child in children: visit(child.read().m_GameObject.deref(), str(obj.path_id))

        visit(roots[0], None)
        assert len(skeletons) == 1
        name = Path(path).stem.removesuffix('_Integrated')
        hierarchy = work / (name + '-source.json')
        write_changed(hierarchy, json.dumps(dict(nodes=nodes), ensure_ascii=False, separators=(',', ':')) + '\n')
        cases.append(dict(name=name, skin=skeletons[0]['initialSkinName'], motion=skeletons[0]['_animationName'],
            hierarchy_file=str(hierarchy), overrides=overrides))
    assert sum(len(c['overrides']) for c in cases) == 18
    assert sum(not r['controller'] for c in cases for r in c['overrides']) == 1
    fixture = json.dumps(dict(cases=cases), ensure_ascii=False, separators=(',', ':')) + '\n'
    write_changed(work / 'weapon-bone-overrides.json', fixture)
    app = base / 'official-code/client-source'
    official = ROOT / 'unity/Assets/Game/Runtime/Official/ProfessionUI'
    offline = ROOT / 'unity/Assets/Game/Runtime/Offline/ProfessionUI'
    editor = ROOT / 'unity/Assets/Game/Editor/Combat/AnimatorPlayback'
    outputs = [official / (name + '.cs') for name in (
        'ProfessionBoneOverrideBindings', 'SkeletonBoneController', 'SkeletonBoneOverride', 'SkeletonBoneRotOverride', 'TransformEulerZLocker')]
    outputs += [offline / 'ProfessionNativeSceneWeaponRenderer.BoneOverrides.cs', editor / 'OfficialBattlePlayerModelClipValidation.BoneOverrides.cs']
    for output in outputs:
        sidecar = Path(str(output) + '.meta')
        # A concurrently running editor may have imported these new scripts
        # already. Keep that published identity; committed metas make replay
        # deterministic without changing live Unity object references.
        if sidecar.exists():
            assert re.search(r'^guid: [0-9a-f]{32}$', sidecar.read_text(), re.M)
        else: meta(output)
        # Unity emits spaces after empty importer scalars on first import.
        # Canonicalize only whitespace, retaining its GUID and importer values.
        canonical = '\n'.join(line.rstrip() for line in sidecar.read_text().splitlines()) + '\n'
        sidecar.write_text(canonical, encoding='utf-8', newline='\n')
    proof = dict(manifest=apk.version, apk_manifest=apk.source.apk_version, apk_sha256=apk.source.apk_sha256,
        native_export_receipt=identity(receipt_path), inspected_prefabs=390, selected_prefabs=selected,
        asset_table_sha256=hashlib.sha256(table_raw).hexdigest(), source_objects=inputs,
        application=[identity(app / path) for path in (
            'AppearanceSystem/SkeletonBoneController.cs', 'AppearanceSystem/SkeletonBoneOverride.cs',
            'AppearanceSystem/SkeletonBoneRotOverride.cs', 'AppearanceSystem/OverrideUpdateMode.cs',
            'AppearanceSystem/OverrideRotMode.cs', 'TransformEulerZLocker.cs')],
        cases=[{k: v for k, v in c.items() if k != 'hierarchy_file'} for c in cases],
        outputs=[identity(path) for output in outputs for path in (output, Path(str(output) + '.meta'))],
        bridge=[identity(offline / (name + '.cs')) for name in (
            'ProfessionNativeSceneWeaponRenderer', 'ProfessionNativeSceneWeaponPlayback',
            'ProfessionNativeConstraintPose', 'ProfessionNativePlayerPhysicsPose')],
        validation_entry=identity(editor / 'OfficialBattlePlayerModelClipValidation.cs'), generator=identity(Path(__file__)),
        scope='Original application registration, cached bone lookup, rotation branches and TransformEulerZLocker; native local callbacks before ordered world constraints, complete callbacks after physics. The 18 source overrides all use Override mode without delta; the null Controller remains unbound.',
        limits=['Diagnostic instantiation uses controlled parents, not complete production weapon prefabs.',
            'Offset/delta branches are checked at the application interface only; the native integration here validates the original absolute-override configurations.',
            'Unused controller blend queries, Mecanim position lookup, NewPosOverride/NewRotate throwing APIs and SkeletonBonePosOverride are not ported here.',
            'AppliedRotation is a local-stage application boundary; complete-stage position overrides and applied-local decomposition are not implemented by this module.',
            'No SDK implementation/source or DLL method bodies are read or copied.'])
    write_changed(ROOT / 'assets/reference/battle-animation/weapon-bone-overrides-source-manifest.json',
        json.dumps(proof, ensure_ascii=False, indent=2) + '\n')
    print('Original weapon bone overrides: 15 prefabs, 18 rotations, one null Controller; 390 native prefab hashes verified.')


if __name__ == '__main__': build()
