"""Restore complete original scene weapon prefabs across the native pose boundary."""
import hashlib
import json
import re
from pathlib import Path

from UnityPy.classes import PPtr
from current_component_schemas import CurrentSchemas
from official_apk import Apk, ROOT
from official_manifest import read_asset_manifest
from prepare_battle_scene_appearance_runtime import identity, write_changed
from prepare_battle_status_orbit_assets import build, EXPORTED, CACHE
from prepare_unity_profession_character_resources import asset, guid, meta
from validation_workspace import validation_directory

RUNTIME = ROOT / 'unity/Assets/Game/Runtime/Offline/ProfessionUI'
OFFICIAL = ROOT / 'unity/Assets/Game/Runtime/Official/ProfessionUI'
DEST = ROOT / 'unity/Assets/Official/Combat/WeaponPrefabs/Resources/Combat/WeaponPrefabs'


def main():
    work = validation_directory()
    high_path = ROOT / 'assets/reference/battle-animation/weapon-high-textures-source-manifest.json'
    low_path = ROOT / 'assets/reference/battle-animation/weapon-low-textures-source-manifest.json'
    high, low = [json.loads(p.read_bytes()) for p in (high_path, low_path)]
    assert high['manifest'] == low['manifest'] == '884_156692'
    receipt = json.loads((CACHE / 'native-export-all-source-manifest.json').read_bytes())
    verified = {r['path']: r['sha256'] for r in receipt['outputs']}
    apk, schemas = Apk.current(), CurrentSchemas()
    table = {r['path']: r for r in read_asset_manifest(apk.source.manifest_bytes('DefaultPackage', apk.archive))}
    roots = {Path(r['path']).stem.removesuffix('_Integrated'): r['path'].removeprefix('Assets/')
        for r in table.values() if '/PlayerPart/pWeapon_' in r['path']
        and r['path'].endswith('_Integrated.prefab') and '_UI_' not in r['path']}
    assert len(roots) == 390
    source_directory = DEST.parents[2] / 'Sources'
    source_directory.mkdir(parents=True, exist_ok=True); meta(source_directory, folder=True)
    sources, entries, source_outputs, cases = [], [], [], []
    for ordinal, (skin, relative) in enumerate(sorted(roots.items()), 1):
        row = table['Assets/' + relative]
        go = apk.asset(row['bundle'], row['path'])
        nodes, skeletons = [], []

        def record(obj):
            result = dict(source=apk.provenance(obj), payload_sha256=hashlib.sha256(obj.get_raw_data()).hexdigest())
            sources.append(result); return result

        def visit(obj, parent):
            value = obj.read(); components, children = [], []
            for pointer in value.m_Component:
                component = pointer.component.deref(); kind = component.type.name
                if kind == 'MonoBehaviour':
                    script = component.read(check_read=False).m_Script.read()
                    kind = (script.m_Namespace + '.' if script.m_Namespace else '') + script.m_ClassName
                    fields = schemas.read(component)
                else: fields = component.read_typetree()
                components.append(dict(type=kind, fields=fields, runtime_fields=fields, **record(component)))
                if kind == 'Spine.Unity.SkeletonAnimation': skeletons.append((component, fields))
                if kind == 'Transform': children.extend(component.read().m_Children)
            nodes.append(dict(name=value.m_Name, parent=parent,
                game_object=dict(fields=obj.read_typetree(), **record(obj)), components=components))
            for child in children: visit(child.read().m_GameObject.deref(), str(obj.path_id))

        visit(go, None)
        assert len(skeletons) == 1
        component, fields = skeletons[0]
        model = PPtr(**fields['skeletonDataAsset'], assetsfile=component.assets_file).deref()
        model_fields = schemas.read(model)
        binary = PPtr(**model_fields['skeletonJSON'], assetsfile=model.assets_file).deref()
        assert hashlib.sha256(binary.get_raw_data()).hexdigest() == high['model']['payload_sha256']
        assert all(c['type'] != 'AppearanceCombineSkin' for n in nodes for c in n['components'])
        source = source_directory / (skin + '.json')
        write_changed(source, json.dumps(dict(path=row['path'], guid=row['guid'], nodes=nodes),
            ensure_ascii=False, separators=(',', ':')) + '\n'); meta(source)
        source_outputs.extend((source, Path(str(source) + '.meta')))
        entries.append(dict(Skin=skin, Path=row['path'], Guid=row['guid'], InitialSkin=fields['initialSkinName'],
            InitialAnimation=fields['_animationName'], Hierarchy=dict(fileID=4900000, guid=guid(source), type=3)))
        kinds = [c['type'] for n in nodes for c in n['components']]
        node_indices = {n['game_object']['source']['path_id']: i for i, n in enumerate(nodes)}
        fixture_nodes = []
        for n in nodes:
            transform = next(c['fields'] for c in n['components'] if c['type'] == 'Transform')
            fixture_nodes.append(dict(name=n['name'], parent=node_indices.get(n['parent'], -1),
                position=transform['m_LocalPosition'], rotation=transform['m_LocalRotation'], scale=transform['m_LocalScale'],
                active=bool(n['game_object']['fields']['m_IsActive']), layer=n['game_object']['fields']['m_Layer']))
        cases.append(dict(name=skin, initial_skin=fields['initialSkinName'], initial_animation=fields['_animationName'],
            z_spacing=fields['zSpacing'], nodes=fixture_nodes, particles=kinds.count('ParticleSystem'), particle_renderers=kinds.count('ParticleSystemRenderer'),
            sprites=kinds.count('SpriteRenderer'), followers=kinds.count('SkeletonRendererFollower'),
            rotations=kinds.count('AppearanceSystem.SkeletonBoneRotOverride')))
        if ordinal % 50 == 0: print('Recovered complete weapon source', ordinal, '/390', flush=True)
    apk.archive.close()
    write_changed(work / 'weapon-native-prefab-cases.json', json.dumps(dict(cases=cases), ensure_ascii=False, separators=(',', ':')) + '\n')

    def checked_native(path):
        raw = path.read_bytes()
        assert hashlib.sha256(raw).hexdigest() == verified[path.relative_to(CACHE / 'native-export-all').as_posix()], path
        return raw.decode('utf-8-sig')

    aliases = {}
    for material in high['materials'][:398]:
        name = material['original_fields']['m_Name']
        path = EXPORTED / 'AppearanceAssets/Character/pWeapon/Materials' / (name + '.mat')
        text = checked_native(path)
        assert re.search(r'(?m)^  m_Name: (.+)$', text)[1] == name
        target = ROOT / material['output']['path']
        raw = target.read_bytes()
        assert hashlib.sha256(raw).hexdigest() == material['output']['sha256'] or hashlib.sha256(raw.replace(b'\r\n', b'\n')).hexdigest() == material['output']['sha256']
        original_guid = re.search(r'^guid: ([0-9a-f]{32})$', checked_native(Path(str(path) + '.meta')), re.M)[1]
        aliases[original_guid] = dict(source=path.relative_to(EXPORTED).as_posix(), target=target.relative_to(ROOT).as_posix())
    for level, evidence, native_path in [('High', high, 'unity/Assets/Official/Combat/WeaponTextures/Bindings/texture-loader-high.asset'),
                                       ('Low', low, 'unity/Assets/Official/Combat/WeaponLowTextures/Bindings/texture-loader-low.asset')]:
        original = EXPORTED / ('AppearanceAssets/Character/pWeapon/primaryWeapon_TextureLoader_' + level + '.asset')
        original_guid = re.search(r'^guid: ([0-9a-f]{32})$', checked_native(Path(str(original) + '.meta')), re.M)[1]
        aliases[original_guid] = dict(source=original.relative_to(EXPORTED).as_posix(), target=native_path)

    mapped = {}
    for file_id, name, namespace, source in [
        (1509019811, 'AppearanceSkeletonPart', '', 'AppearanceSkeletonPart.cs'),
        (1513771027, 'SkeletonRendererFollower', '', 'SkeletonRendererFollower.cs'),
        (34885126, 'AppearanceRendererLite', 'AppearanceSystem', 'AppearanceSystem/AppearanceRendererLite.cs'),
        (-738041613, 'SkeletonBoneController', 'AppearanceSystem', 'AppearanceSystem/SkeletonBoneController.cs'),
        (1694013511, 'SkeletonBoneRotOverride', 'AppearanceSystem', 'AppearanceSystem/SkeletonBoneRotOverride.cs'),
        (1216268992, 'TransformEulerZLocker', '', 'TransformEulerZLocker.cs'),
        (502961096, 'SkeletonRendererHighTexturePreview', '', 'SkeletonRendererHighTexturePreview.cs')]:
        target = OFFICIAL / (name + '.cs')
        if not Path(str(target) + '.meta').exists(): meta(target)
        mapped[file_id] = dict(name=name, namespace=namespace, source=source,
            target_namespace='Zjcs.Official.ProfessionUI', runtime=target.relative_to(ROOT).as_posix())
    renderer_guid = guid(RUNTIME / 'ProfessionNativeSceneWeaponRenderer.cs')
    mesh_guid = guid(RUNTIME / 'ProfessionNativeSceneMeshPart.cs')

    def native_transform(text, path):
        if path.relative_to(EXPORTED).as_posix() not in roots.values(): return text
        chunks = re.split(r'(?=^--- !u!)', text, flags=re.M)
        skeleton = [i for i, c in enumerate(chunks) if 'guid: ' + renderer_guid in c]
        assert len(skeleton) == 1, path
        index = skeleton[0]; chunk = chunks[index]
        owner = re.search(r'  m_GameObject: \{fileID: (\d+)\}', chunk)[1]
        # All SDK fields remain in the lossless source JSON. The native drawing
        # endpoint receives them through the catalog before component activation.
        chunks[index] = chunk[:re.search(r'(?m)^  m_EditorClassIdentifier:[^\n]*\n', chunk).end()]
        new_id = 114999999999999999
        assert not re.search(r'^--- !u!\d+ &' + str(new_id) + r'$', text, re.M)
        game_object = next(i for i, c in enumerate(chunks) if c.startswith('--- !u!1 &' + owner + '\n'))
        chunks[game_object], count = re.subn(r'(?m)^(  m_Layer:)', '  - component: {fileID: ' + str(new_id) + '}\n\\1', chunks[game_object])
        assert count == 1, path
        chunks.append('--- !u!114 &' + str(new_id) + '\nMonoBehaviour:\n  m_ObjectHideFlags: 0\n'
            '  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n'
            '  m_GameObject: {fileID: ' + owner + '}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n'
            '  m_Script: {fileID: 11500000, guid: ' + mesh_guid + ', type: 3}\n  m_Name:\n  m_EditorClassIdentifier:\n')
        return ''.join(chunks)

    asset_receipt = ROOT / 'assets/reference/battle-animation/weapon-prefab-assets-source-manifest.json'
    build(DEST=DEST, DATA=ROOT / 'unity/Assets/Game/Resources/Combat/Weapons/native-prefabs.json',
        RECEIPT=asset_receipt, ROOTS={}, MAPPED={}, family='WeaponPrefabs', shader_prefix='WeaponPrefab_',
        graph_roots=roots, source_scripts=mapped,
        reference_scripts={354902358: dict(native_asset=next(iter(roots.values())), namespace='Spine.Unity', name='SkeletonAnimation',
            runtime=(RUNTIME / 'ProfessionNativeSceneWeaponRenderer.cs').relative_to(ROOT).as_posix(),
            receipt='assets/reference/battle-animation/weapon-physics-source-manifest.json')},
        native_scripts=[(RUNTIME / 'ProfessionNativeSceneMeshPart.cs').relative_to(ROOT).as_posix()],
        native_transform=native_transform, external_assets=aliases,
        runtime_receipts=['weapon-high-textures-source-manifest.json', 'weapon-low-textures-source-manifest.json', 'weapon-bone-overrides-source-manifest.json'],
        generator=Path(__file__),
        operation='Complete original scene weapon hierarchy and components, including particles, sprites and internal references. Retain native local fileIDs. Replace the serialized SDK animation component with the independent native drawing endpoint plus its mesh component; original SDK configuration remains losslessly in explicit source JSON. Reuse only verified original atlas materials and High/Low loaders.',
        limits=['Production character factory/battle binding and complete Windows Player validation remain separate.'])
    for entry in entries:
        target = DEST / roots[entry['Skin']]
        # The source prefab root local fileID is preserved by the conversion.
        root_chunk, = [c for c in re.split(r'(?=^--- !u!)', target.read_text(), flags=re.M)
            if c.startswith('--- !u!1 &') and re.search(r'(?m)^  m_Name: ' + re.escape(entry['Skin'] + '_Integrated') + '$', c)]
        root_id = re.search(r'^--- !u!1 &(\d+)$', root_chunk, re.M)[1]
        entry['Prefab'] = dict(fileID=int(root_id), guid=guid(target), type=3)
    catalog = DEST.parent / 'Weapons/scene-prefabs.asset'
    catalog.parent.mkdir(parents=True, exist_ok=True); meta(catalog.parent, folder=True)
    asset(catalog, 'Complete original integrated scene weapons', RUNTIME / 'ProfessionNativeSceneWeaponCatalog.cs', dict(
        weapon=dict(fileID=11400000, guid=guid(ROOT / 'unity/Assets/Game/Resources/ProfessionUI/weapon-resources.asset'), type=2),
        textures=dict(fileID=11400000, guid=guid(ROOT / 'unity/Assets/Official/Combat/WeaponLowTextures/Resources/Combat/Weapons/full-texture-resources.asset'), type=2), entries=entries))
    source_outputs.extend((catalog, Path(str(catalog) + '.meta')))
    proof = dict(manifest=high['manifest'], sources=sources, asset_conversion=identity(asset_receipt),
        high_receipt=identity(high_path), low_receipt=identity(low_path), entries=entries,
        outputs=[identity(p) for p in source_outputs], generator=identity(Path(__file__)),
        runtime=[identity(p) for p in [RUNTIME / (n + '.cs') for n in (
            'ProfessionNativeSceneWeapon', 'ProfessionNativeSceneWeaponCatalog', 'ProfessionNativeSceneWeaponRenderer',
            'OfflineProfessionScenePartPoolBindings', 'ProfessionNativeWeaponTextureResources', 'ProfessionNativeSceneRendererSettings')]
            + [OFFICIAL / 'SkeletonRendererHighTexturePreview.cs']],
        validation=[identity(ROOT / ('unity/Assets/Game/Editor/Combat/AnimatorPlayback/' + name + '.cs')) for name in (
            'OfficialBattlePlayerModelClipValidation', 'OfficialBattlePlayerModelClipValidation.HighTextures',
            'OfficialBattlePlayerModelClipValidation.NativeWeaponPrefabs')],
        binding='Combat/Weapons/scene-prefabs',
        limits=['Original G_073 preview uses its explicit Low loader, no combine-skin component and no model default skin. Unused combine-skin/default-skin preview branches are outside this binding.',
            'Full production character/battle host and Windows Player remain unverified.'])
    write_changed(ROOT / 'assets/reference/battle-animation/weapon-prefabs-source-manifest.json', json.dumps(proof, ensure_ascii=False, indent=2) + '\n')
    print('Complete native scene weapons:', len(entries))


if __name__ == '__main__': main()
