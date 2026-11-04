"""Recover original partner Transform branches and serialized attachment bindings.

Reads prefab data and component field schemas, never animation SDK algorithms.
"""
import argparse
import hashlib
import json
from pathlib import Path

from current_component_schemas import CurrentSchemas
from official_apk import Apk, ROOT
from official_manifest import read_asset_manifest
from prepare_battle_scene_appearance_runtime import identity, meta, write_changed
from UnityPy.classes import PPtr


def build(work, monsters=False):
    work = work.resolve()
    if work.parent != ROOT / 'output/validation' or not (work / '.zjcs-validation.json').is_file():
        raise ValueError('Use an owned attachment validation workspace')
    inventory = ROOT / ('unity/Assets/Official/Combat/NativeMonsters/inventory.json' if monsters
        else 'unity/Assets/Official/Combat/NativePartners/catalog.json')
    selected = json.loads(inventory.read_bytes())
    apk, schemas = Apk.current(), CurrentSchemas()
    if apk.version != selected['manifest'] or apk.version != '884_156692':
        raise ValueError('Review the original partner attachment source version')
    objects, results = [], []

    def key(obj):
        return str(obj.assets_file.name) + ':' + str(obj.path_id)

    def pointer(owner, value):
        return PPtr(**value, assetsfile=owner.assets_file).deref() if value['m_PathID'] else None

    def resolved_references(owner, value, path='', output=None):
        if output is None:
            output = {}
        if isinstance(value, dict):
            if set(value) == {'m_FileID', 'm_PathID'}:
                obj = pointer(owner, value)
                if obj:
                    output[path] = dict(sourceKey=key(obj), type=obj.type.name)
            else:
                for name, child in value.items():
                    resolved_references(owner, child, path + '/' + name, output)
        elif isinstance(value, list):
            for index, child in enumerate(value):
                resolved_references(owner, child, path + '/' + str(index), output)
        return output

    def walk(transform, parent, nodes, sibling=0):
        data = transform.read()
        go = data.m_GameObject.deref()
        obj = go.read()
        row = dict(sourceKey=key(transform), gameObjectKey=key(go), parentKey=parent, siblingIndex=sibling,
            name=obj.m_Name, active=obj.m_IsActive, layer=obj.m_Layer,
            position={axis: getattr(data.m_LocalPosition, axis) for axis in 'xyz'},
            rotation={axis: getattr(data.m_LocalRotation, axis) for axis in 'xyzw'},
            scale={axis: getattr(data.m_LocalScale, axis) for axis in 'xyz'},
            components=[])
        row['transformType'] = transform.type.name
        if transform.type.name == 'RectTransform':
            fields = transform.read_typetree()
            row['rect'] = {name: fields[name] for name in ('m_AnchorMin', 'm_AnchorMax',
                'm_AnchoredPosition', 'm_SizeDelta', 'm_Pivot')}
        for source in (transform, go):
            objects.append(dict(source=apk.provenance(source),
                object_sha256=hashlib.sha256(source.get_raw_data()).hexdigest()))
        nodes.append(row)
        for reference in obj.m_Component:
            component = reference.component.deref()
            if component.type.name != 'MonoBehaviour':
                row['components'].append(dict(type=component.type.name, sourceKey=key(component)))
                continue
            name = component.read(check_read=False).m_Script.read().m_ClassName
            entry = dict(type=name, sourceKey=key(component))
            row['components'].append(entry)
            if name in ('SkeletonMecanim', 'SkeletonAnimation'):
                renderer = schemas.read(component)
                skeleton_data = pointer(component, renderer['skeletonDataAsset'])
                entry['skeletonDataName'] = skeleton_data.read(check_read=False).m_Name
                for source in (component, skeleton_data):
                    objects.append(dict(source=apk.provenance(source),
                        object_sha256=hashlib.sha256(source.get_raw_data()).hexdigest()))
            if name in ('AttachAgent', 'BoneFollower', 'AttachmentFollower'):
                fields = schemas.read(component)
                entry.update(fields=fields, references=resolved_references(component, fields))
                objects.append(dict(source=apk.provenance(component),
                    object_sha256=hashlib.sha256(component.get_raw_data()).hexdigest(), fields=fields))
        for index, child in enumerate(data.m_Children):
            walk(child.deref(), key(transform), nodes, index)

    try:
        assets = read_asset_manifest(apk.source.manifest_bytes('DefaultPackage', apk.archive))
        for partner in selected['models' if monsters else 'fighters']:
            entity = partner['entityId']
            candidates = [row for row in assets if row['path'].endswith('/' + str(entity) + '.prefab')]
            if len(candidates) != 1:
                raise ValueError('Ambiguous/missing original partner entity: ' + str(entity))
            root = apk.asset(candidates[0]['bundle'], '/' + str(entity) + '.prefab')
            components = [ref.component.deref() for ref in root.read().m_Component]
            transform, = [obj for obj in components if obj.type.name == 'Transform']
            nodes = []
            walk(transform, '', nodes)
            if (nodes[0]['name'] != partner['transforms'][0]['name'] if monsters else
                    nodes[0]['sourceKey'] != partner['transforms'][0]['sourceKey']):
                raise ValueError('Original partner entity hierarchy identity differs')
            bindings = [component['type'] for node in nodes for component in node['components'] if 'fields' in component]
            results.append(dict(npcId=entity if monsters else partner['npcId'], entityId=entity, nodes=nodes))
            print(entity if monsters else partner['npcId'], 'nodes', len(nodes), 'bindings', bindings, flush=True)
    finally:
        apk.archive.close()
    prefix = 'monster' if monsters else 'partner'
    target = work / (prefix + '-native-attachments.json')
    write_changed(target, json.dumps(dict(manifest=apk.version, partners=results), ensure_ascii=False, separators=(',', ':')) + '\n')
    proof = dict(manifest=apk.version, selection=identity(inventory), objects=objects,
        component_schemas=schemas.records, output=identity(target), generator=identity(Path(__file__).resolve()))
    write_changed(work / (prefix + '-native-attachments-source-manifest.json'), json.dumps(proof, ensure_ascii=False, indent=2) + '\n')
    return results


def install(work):
    proof = json.loads((work / 'partner-native-attachments-source-manifest.json').read_bytes())
    target = ROOT / 'unity/Assets/Official/Combat/NativePartnerAssets/Resources/Combat/NativePartnerAssets/attachments.json'
    receipt = ROOT / 'assets/reference/partner-native-attachments-source-manifest.json'
    raw = (work / 'partner-native-attachments.json').read_bytes()
    if target.exists() and target.read_bytes() != raw and (not receipt.exists() or
            hashlib.sha256(target.read_bytes()).hexdigest() != json.loads(receipt.read_bytes())['output']['sha256']):
        raise ValueError('Preserve the existing attachment payload until its provenance matches')
    target.write_bytes(raw)
    owned = [target,
        ROOT / 'unity/Assets/Game/Runtime/Offline/ProfessionUI/PartnerNativeBattleAttachments.cs',
        ROOT / 'unity/Assets/Game/Runtime/Offline/ProfessionUI/PartnerNativeBattleRendererPort.cs',
        ROOT / 'unity/Assets/Game/Editor/PartnerBattlePlayables/OfficialPartnerNativeAttachmentAcceptance.cs']
    for path in owned:
        metadata = Path(str(path) + '.meta')
        if not metadata.exists():
            meta(path)
        write_changed(metadata, '\n'.join(line.rstrip() for line in metadata.read_text('utf8').splitlines()) + '\n')
    proof['output'] = identity(target)
    proof['outputs'] = [identity(path) for path in owned[1:] + [Path(str(path) + '.meta') for path in owned]]
    proof['bindings'] = [identity(ROOT / 'unity/Assets/Game' / name) for name in (
        'Runtime/Offline/ProfessionUI/PartnerNativeBattleActor.cs',
        'Runtime/Offline/ProfessionUI/Zjcs.Offline.ProfessionUI.asmdef',
        'Runtime/Official/StoryAttachment/AttachAgent.cs',
        'Runtime/Official/StoryAttachment/AttachmentRegistryUtils.cs',
        'Runtime/Official/StoryAttachment/AttachPosRotCfg.cs',
        'Runtime/Official/BattleSpinePlayables/AttachmentFollower.cs',
        'Editor/PartnerBattlePlayables/Zjcs.Editor.PartnerBattlePlayables.asmdef')]
    proof['validation_factories'] = [identity(ROOT / 'scripts' / name) for name in (
        'prepare_partner_native_battle_validation.py', 'prepare_partner_native_playables_validation.py',
        'prepare_partner_native_attachments_validation.py')]
    proof['decoding_inputs'] = [identity(ROOT / 'scripts' / name) for name in (
        'current_component_schemas.py', 'official_metadata.py', 'official_unity_schema.py',
        'official_apk.py', 'official_manifest.py', 'prepare_battle_scene_appearance_runtime.py')]
    code = ROOT / '.tools/source-packages/current' / proof['manifest'] / 'official-code'
    proof['application_sources'] = [identity(code / 'client-source' / (name + '.cs'))
        for name in ('AttachAgent', 'AttachPosRotCfg', 'AttachmentFollower')]
    proof['metadata_inputs'] = [identity(code / 'global-metadata.dat')]
    proof['limits'] = ['Original Transform/RectTransform data and AttachAgent lists are bound; other recorded prefab components are not recreated by this module.',
        'AttachmentFollower is original game application code on the independent native pose interfaces. No animation SDK algorithms are read or ported.',
        'Four serialized BoneFollower configurations are recorded only; their runtime behavior is not implemented by this binding.']
    write_changed(receipt, json.dumps(proof, ensure_ascii=False, indent=2) + '\n')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--workspace', required=True, type=Path)
    parser.add_argument('--install', action='store_true')
    args = parser.parse_args()
    build(args.workspace)
    if args.install:
        install(args.workspace)
