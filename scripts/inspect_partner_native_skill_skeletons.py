"""Inspect serialized skeleton bindings in the selected partners' original skill prefabs.

Only component metadata and asset data are decoded; no animation SDK code is read.
"""
from collections import Counter
import hashlib
import json

from current_component_schemas import CurrentSchemas
from official_apk import Apk, ROOT
from official_manifest import read_asset_manifest
from UnityPy.classes import PPtr
from validation_workspace import validation_directory


def main():
    work = validation_directory()
    if work.parent != ROOT / 'output/validation' or not (work / '.zjcs-validation.json').is_file():
        raise ValueError('Use an owned partner skill skeleton workspace')
    selected = json.loads((ROOT / 'unity/Assets/Game/Resources/Combat/partner-native-playables.json').read_bytes())
    apk, schemas = Apk.current(), CurrentSchemas()
    if apk.version != selected['manifest'] or apk.version != '884_156692':
        raise ValueError('Original partner skill skeleton version differs')
    assets = read_asset_manifest(apk.source.manifest_bytes('DefaultPackage', apk.archive))
    counts, records, sources = Counter(), [], {}
    wanted = {'SkeletonAnimation', 'SkeletonMecanim', 'SkeletonRenderer', 'AttachmentFollower',
        'BoneFollower', 'AppearanceRendererLite', 'AppearanceCombineSkin', 'SkeletonBoneController',
        'SkeletonBonePosOverride', 'SkeletonBoneRotOverride', 'SkeletonRendererHighTexturePreview',
        'DynamicSkeletonRenderSeparator', 'SkeletonPartsRenderer'}

    def source(obj):
        key = str(obj.assets_file.name) + ':' + str(obj.path_id)
        if key not in sources:
            sources[key] = dict(source=apk.provenance(obj), object_sha256=hashlib.sha256(obj.get_raw_data()).hexdigest())
        return key

    def pointer(owner, value):
        return PPtr(**value, assetsfile=owner.assets_file).deref() if value['m_PathID'] else None

    def walk(go, path, row):
        data = go.read()
        node = dict(path=path + '/' + data.m_Name, source_key=source(go), components=[])
        transform = None
        for ref in data.m_Component:
            obj = ref.component.deref()
            if obj.type.name in ('Transform', 'RectTransform'):
                transform = obj.read()
            if obj.type.name == 'MeshRenderer':
                fields = obj.read_typetree()
                node.setdefault('mesh_renderers', []).append(dict(source_key=source(obj), fields=fields,
                    materials=[dict(source_key=source(ref.deref()), fields=ref.deref().read_typetree())
                        for ref in obj.read().m_Materials if ref.path_id]))
            if obj.type.name != 'MonoBehaviour':
                continue
            script = obj.read(check_read=False).m_Script.read()
            cls = script.m_ClassName
            counts[cls] += 1
            if cls not in wanted:
                continue
            fields = schemas.read(obj)
            item = dict(type=cls, namespace=script.m_Namespace, assembly=script.m_AssemblyName,
                source_key=source(obj), fields=fields)
            asset = fields.get('skeletonDataAsset')
            if asset and asset['m_PathID']:
                asset = pointer(obj, asset)
                item['skeleton_data_asset'] = dict(name=asset.read(check_read=False).m_Name,
                    source_key=source(asset), fields=schemas.read(asset))
            loader = fields.get('mTextureLoader')
            if loader and loader['m_PathID']:
                loader = pointer(obj, loader)
                item['texture_loader'] = dict(source_key=source(loader), fields=schemas.read(loader))
            node['components'].append(item)
        if node['components'] or node.get('mesh_renderers'):
            row['nodes'].append(node)
        if transform is None:
            raise ValueError('Original skill prefab has no Transform')
        for child in transform.m_Children:
            walk(child.read().m_GameObject.deref(), node['path'], row)

    try:
        for skill in selected['skills']:
            key = skill['skill_entity_id']
            candidates = [row for row in assets if row['path'].endswith('/' + str(key) + '.prefab')]
            if len(candidates) != 1:
                raise ValueError('Original partner skill entity is ambiguous: ' + str(key))
            go = apk.asset(candidates[0]['bundle'], '/' + str(key) + '.prefab')
            owner, = [ref.component.deref() for ref in go.read().m_Component
                if ref.component.deref().type.name == 'MonoBehaviour'
                and ref.component.deref().read(check_read=False).m_Script.read().m_ClassName == 'MyFightSkill']
            fields = schemas.read(owner)
            for variant in skill['variants']:
                row = dict(skill_id=key, field=variant['field'], nodes=[])
                walk(pointer(owner, fields[variant['field']]), '', row)
                records.append(row)
            print(key, 'serialized skill skeletons inspected', flush=True)
    finally:
        apk.archive.close()
    output = dict(manifest=apk.version, graphs=records, component_counts=dict(counts), sources=sources,
        component_schemas=schemas.records)
    target = work / 'partner-native-skill-skeletons.json'
    target.write_text(json.dumps(output, ensure_ascii=False, indent=2) + '\n', encoding='utf8')
    skeletons = [component['skeleton_data_asset']['name'] for row in records for node in row['nodes']
        for component in node['components'] if 'skeleton_data_asset' in component]
    print('Original embedded skeletons:', dict(Counter(skeletons)), flush=True)
    return output


if __name__ == '__main__':
    main()
