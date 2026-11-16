"""Recover the selected dining-table repair target and its official slot chain."""
import hashlib
import argparse
import json
from pathlib import Path

import yaml
from home_native_assets import install_staged_assets, write_staged_assets
from official_apk import ROOT
from install_home_story_options import metadata
from prepare_current_partner_native import PartnerNativeRecovery, documents, identity, pointer
from prepare_partner_card_native import Dumper, guid, sha
from validation_workspace import validation_directory


def named_child(transform, name):
    matches = [child.deref() for child in transform.read().m_Children
               if child.read().m_GameObject.read().m_Name == name]
    if len(matches) != 1:
        raise ValueError('Original hierarchy is not unique: ' + name)
    return matches[0]


def chain(transform, recovery):
    result = []
    while transform:
        value = transform.read()
        position, scale, rotation = value.m_LocalPosition, value.m_LocalScale, value.m_LocalRotation
        result.append(dict(name=value.m_GameObject.read().m_Name, source=identity(transform),
            sha256=sha(transform.get_raw_data()), position=[position.x, position.y, position.z],
            scale=[scale.x, scale.y, scale.z], rotation=[rotation.x, rotation.y, rotation.z, rotation.w],
            provenance=recovery.apk.provenance(transform)))
        transform = value.m_Father.deref() if value.m_Father.path_id else None
    return result


def prepare():
    work = validation_directory()
    recovery = PartnerNativeRecovery()
    bundle = 'defaultpackage_assets_prefabs_home_dynamic_furniture_diningtable.bundle'
    relative = 'Prefabs/Home/Dynamic/Furniture/DiningTable/DiningTable.prefab'
    source_objects = recovery.apk.objects(bundle)
    root, = [obj for obj in source_objects if obj.type.name == 'GameObject' and obj.read().m_Name == 'DiningTable'
             and not obj.read().m_Component[0].component.read().m_Father.path_id]
    root_scripts = []
    for entry in root.read().m_Component:
        if entry.component.type.name != 'MonoBehaviour': continue
        component = entry.component.deref()
        kind = component.read(check_read=False).m_Script.read().m_ClassName
        root_scripts.append(dict(name=kind, source=identity(component), sha256=sha(component.get_raw_data())))
        if kind == 'HomeEntityCreateData':
            offset = recovery.fields(component)['mLocalPosOffset']
            if any(offset[axis] != 0 for axis in ('x', 'y', 'z')):
                raise ValueError('The original entity creation offset must be applied to the collider.')
    root_transform = root.read().m_Component[0].component.deref()
    unrepaired = named_child(root_transform, 'unrepaired')
    target_transform = named_child(unrepaired, 'touch')
    target = target_transform.read().m_GameObject.deref()
    colliders = [entry.component.deref() for entry in target.read().m_Component if entry.component.type.name == 'BoxCollider']
    if len(colliders) != 1:
        raise ValueError('Original unrepaired furniture collider is not unique.')
    native = recovery.native / relative
    docs = documents(native.read_text('utf8'))
    candidates = []
    for file_id, doc in docs.items():
        go = doc.get('GameObject')
        if not go or go.get('m_Name') != 'touch': continue
        trans = docs[go['m_Component'][0]['component']['fileID']]['Transform']
        parent = docs[trans['m_Father']['fileID']]['Transform']
        if docs[parent['m_GameObject']['fileID']]['GameObject']['m_Name'] == 'unrepaired': candidates.append(file_id)
    native_id, = candidates
    recovery.recover('DiningTableUnrepairedTouch', work / 'home-story-collider-recovery',
        bundle=bundle, native_relative=relative, source_root=target, native_root_id=native_id)
    recovery.finalize()
    _, _, panel, record = recovery.outputs[0]
    if panel['scripts'] or panel['unresolved'] or panel['external']:
        raise ValueError('The original collider subtree has unbound dependencies.')
    out_guid = hashlib.md5(b'zjcs:home-story:dining-table-unrepaired-touch').hexdigest()
    outputs = write_staged_assets([(Path('DiningTableUnrepairedTouch.prefab'), out_guid, panel['documents'])],
        work / 'home-story-collider-assets')

    scene_objects = recovery.apk.objects('defaultpackage_assets_scenes_main.bundle')
    manager, = [obj for obj in scene_objects if obj.type.name == 'MonoBehaviour'
                and obj.read(check_read=False).m_Script.deref().read(check_read=False).m_ClassName == 'HomeEntityManagerMono']
    fields = recovery.fields(manager)
    house_row, = [row for row in fields['HomeThemeSlotDict']['mSerializeBuffer'] if row['Key'] == 1]
    house_chain = chain(pointer(house_row['Value'], manager), recovery)
    house_objects = recovery.apk.objects('defaultpackage_assets_prefabs_home_dynamic_theme_house.bundle')
    house, = [obj for obj in house_objects if obj.type.name == 'GameObject' and obj.read().m_Name == 'House'
              and not obj.read().m_Component[0].component.read().m_Father.path_id]
    def descendants(transform):
        yield transform
        for child in transform.read().m_Children: yield from descendants(child.deref())
    slot, = [obj for obj in descendants(house.read().m_Component[0].component.deref())
             if obj.read().m_GameObject.read().m_Name == 'DiningTableSlot']
    all_chains = dict(furniture=chain(target_transform, recovery), slot=chain(slot, recovery), house=house_chain)
    for entries in all_chains.values():
        for entry in entries:
            if entry['scale'] != [1, 1, 1] or entry['rotation'] != [0, 0, 0, 1]:
                raise ValueError('Original nonidentity hierarchy requires full transform composition.')
    native_meta = Path(str(native) + '.meta')
    inputs = {path.relative_to(ROOT).as_posix(): sha(path.read_bytes())
              for path in (Path(__file__), native, native_meta,
                           ROOT / 'scripts/prepare_current_partner_native.py',
                           ROOT / '.tools/source-packages/official-code/client-source/RepairFurnitureNode.cs',
                           ROOT / '.tools/source-packages/official-code/client-source/BaseHomeEntityCreator.cs')}
    proof = dict(manifest=recovery.apk.version, inputs=inputs, recovery=record, chain=all_chains, rootScripts=root_scripts,
        collider=dict(source=identity(colliders[0]), sha256=sha(colliders[0].get_raw_data()),
                      provenance=recovery.apk.provenance(colliders[0]), fields=recovery.fields(colliders[0])),
        outputs=outputs, root=dict(fileID=panel['root'], guid=out_guid, type=3),
        scope='Original unrepaired collision subtree and placement evidence; not yet bound to home or its story mask.')
    (work / 'home-story-collider-source-manifest.json').write_text(
        json.dumps(proof, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    print('Recovered original dining-table repair collider and placement.', flush=True)


def install():
    work = validation_directory()
    proof = json.loads((work / 'home-story-collider-source-manifest.json').read_bytes())
    if proof['manifest'] != '884_156692': raise ValueError('Incorrect selected furniture source.')
    for path, expected in proof['inputs'].items():
        if sha((ROOT / path).read_bytes()) != expected: raise ValueError('Changed collider input: ' + path)
    outputs = install_staged_assets(work, work / 'home-story-collider-assets', proof['outputs'],
        ROOT / 'unity/Assets/Official/Home/StoryColliderNative', 'home-story-collider')
    script = ROOT / 'unity/Assets/Game/Runtime/Offline/HomeStoryDialog/HomeStoryFurnitureColliderAssets.cs'
    script_meta = metadata(script, 'MonoImporter')
    pos = [sum(row['position'][i] for rows in proof['chain'].values() for row in rows) for i in range(3)]
    catalog = ROOT / 'unity/Assets/Game/Resources/Home/HomeStoryFurnitureColliderAssets.asset'
    data = dict(MonoBehaviour=dict(m_ObjectHideFlags=0, m_CorrespondingSourceObject=dict(fileID=0),
        m_PrefabInstance=dict(fileID=0), m_PrefabAsset=dict(fileID=0), m_GameObject=dict(fileID=0),
        m_Enabled=1, m_EditorHideFlags=0, m_Script=dict(fileID=11500000, guid=guid(script_meta), type=3),
        m_Name='HomeStoryFurnitureColliderAssets', m_EditorClassIdentifier='',
        DiningTableUnrepairedTouch=proof['root'], DiningTableWorldPosition=dict(zip(('x', 'y', 'z'), pos))))
    raw = ('%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\n'
           + yaml.dump(data, Dumper=Dumper, sort_keys=False)).encode('utf8')
    previous_path = ROOT / 'assets/reference/home-story-furniture-collider-source-manifest.json'
    if catalog.exists() and catalog.read_bytes() != raw:
        if not previous_path.is_file(): raise ValueError('Preserve existing collider catalog.')
        previous = json.loads(previous_path.read_bytes())
        if previous.get('installed_output_sha256', {}).get(catalog.relative_to(ROOT).as_posix()) != sha(catalog.read_bytes()):
            raise ValueError('Existing collider catalog differs from its receipt.')
    catalog.write_bytes(raw)
    catalog_meta = metadata(catalog, 'NativeFormatImporter')
    for path in (script, script_meta, catalog, catalog_meta): outputs[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
    proof['installed_output_sha256'] = outputs
    proof['validation_inputs'] = {path.relative_to(ROOT).as_posix(): sha(path.read_bytes()) for path in
        (ROOT / 'scripts/prepare_home_story_mask_validation.py', ROOT / 'scripts/fixtures/home_story_mask_validation.cs')}
    proof['scope'] = 'Original unrepaired collision subtree and explicit catalog; actual story-host binding remains pending.'
    previous_path.write_text(json.dumps(proof, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    print('Installed the original dining-table story collider catalog.', flush=True)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--install', action='store_true')
    args = parser.parse_args()
    install() if args.install else prepare()
