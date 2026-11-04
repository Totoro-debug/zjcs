"""Recover configured original shadow subtrees and their native Sprite dependencies."""
from __future__ import annotations

import hashlib
import json
import os
import re
from pathlib import Path

from PIL import Image, ImageChops

from official_apk import Apk, ROOT
from prepare_battle_performance_camera import documents, EXPORT
from inspect_battle_skill_graph_bindings import EXPORT_RECEIPT
from prepare_battle_scene_appearance_runtime import identity, meta, write_changed

DEST = ROOT / 'unity/Assets/Official/Combat/CharacterShadows/Resources/Combat/CharacterShadows'
DATA = ROOT / 'unity/Assets/Game/Resources/Combat/character-shadow-bindings.json'
RECEIPT = ROOT / 'assets/reference/battle-scenes/character-shadow-source-manifest.json'
GUID = re.compile(r'guid: ([a-f0-9]{32})')


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def main():
    work = Path(os.environ['ZJCS_VALIDATION_DIR']).resolve()
    if work.parent != (ROOT / 'output/validation').resolve() or not (work / '.zjcs-validation.json').is_file():
        raise ValueError('An owned validation workspace is required.')
    proof = json.loads(EXPORT_RECEIPT.read_bytes())
    verified = {row['path']: row['sha256'] for row in proof['outputs']}
    inputs, outputs, converted, originals = {}, set(), [], []

    def read(path):
        raw = path.read_bytes()
        if sha(raw) != verified[path.relative_to(EXPORT.parents[1]).as_posix()]:
            raise ValueError('Original native export changed: ' + str(path))
        inputs[str(path)] = identity(path)
        return raw

    def save(path, raw, importer):
        path.parent.mkdir(parents=True, exist_ok=True)
        write_changed(path, raw.decode('utf-8-sig'))
        meta(path)
        sidecar = Path(str(path) + '.meta')
        guid = GUID.search(sidecar.read_text(encoding='utf-8-sig'))[1]
        write_changed(sidecar, f'fileFormatVersion: 2\nguid: {guid}\n{importer}:\n  externalObjects: {{}}\n')
        outputs.update((path, sidecar))
        parent = path.parent
        while parent.is_relative_to(DEST.parents[2]):
            if not Path(str(parent) + '.meta').exists():
                meta(parent, folder=True)
            outputs.add(Path(str(parent) + '.meta'))
            parent = parent.parent
        return guid

    apk = Apk.current()
    if apk.version != '884_156692' or proof['manifest'] != apk.version:
        raise ValueError('Review the selected original shadow source version.')
    texture_root = EXPORT.parents[2] / 'all-assets/windows-textures'
    texture_receipt = texture_root / 'source-manifest.json'
    texture_proof = json.loads(texture_receipt.read_bytes())
    if texture_proof['manifest'] != apk.version or not texture_proof['completed']:
        raise ValueError('Original native texture conversion is incomplete.')
    texture_records = {row['source']['object_sha256']: row for row in texture_proof['textures']}
    dependency_guids = {}

    def sprite(old_guid):
        if old_guid in dependency_guids:
            return dependency_guids[old_guid]
        # Both known original flat Sprite export locations are checked before
        # treating an unresolved reference as a conversion failure.
        candidates = [path for directory in ('Image', 'Sprite')
            for path in (EXPORT / directory).glob('*shadow*.asset.meta')
            if GUID.search(read(path).decode('utf-8-sig'))[1] == old_guid]
        if len(candidates) != 1:
            raise ValueError(f'Resolve the original shadow Sprite GUID {old_guid}: {candidates}')
        source = candidates[0].with_suffix('')
        text = read(source).decode('utf-8-sig')
        texture_guid = re.search(r'texture: \{fileID: 2800000, guid: ([a-f0-9]{32}), type: 3\}', text)[1]
        textures = [path for directory in ('Image', 'Texture2D')
            for path in (EXPORT / directory).glob('*shadow*.png.meta')
            if GUID.search(read(path).decode('utf-8-sig'))[1] == texture_guid]
        if len(textures) != 1:
            raise ValueError('Resolve the original shadow texture GUID: ' + texture_guid)
        texture_source = textures[0].with_suffix('')
        texture_meta = read(textures[0]).decode('utf-8-sig')
        bundle = re.search(r'(?m)^  assetBundleName: (.+)$', texture_meta)[1].strip() + '.bundle'
        objects = [obj for obj in apk.objects(bundle) if obj.type.name == 'Texture2D'
                   and obj.read().m_Name == texture_source.stem]
        if len(objects) != 1:
            raise ValueError('Original shadow texture is ambiguous: ' + bundle)
        obj = objects[0]
        record = texture_records[sha(obj.get_raw_data())]
        value = obj.read()
        compressed = bytes(value.get_image_data())
        for mip in record['mips']:
            if 'source_offset' in mip:
                start, size, expected = mip['source_offset'], mip['source_size'], mip['source_sha256']
            else:
                start, size, expected = mip['source_face_offset'] + mip['astc_offset'], mip['astc_size'], mip['astc_sha256']
            if sha(compressed[start:start + size]) != expected:
                raise ValueError('Original shadow texture mip differs.')
        entry, = [row for row in record['outputs'] if row['path'].endswith('.asset')]
        native = texture_root / entry['path']
        raw = native.read_bytes()
        if sha(raw) != entry['sha256'] or record['native_format'] not in (3, 4):
            raise ValueError('Original shadow native texture cache differs.')
        rgba = bytes.fromhex(re.search(rb'(?m)^  _typelessdata: ([a-fA-F0-9]+)', raw)[1].decode())
        if sha(rgba) != record['pixels_sha256']:
            raise ValueError('Original shadow decoded pixels differ.')
        mode, stride = ('RGBA', 4) if record['native_format'] == 4 else ('RGB', 3)
        decoded = Image.frombytes(mode, (value.m_Width, value.m_Height),
            rgba[:value.m_Width * value.m_Height * stride]).convert('RGBA')
        read(texture_source)
        with Image.open(texture_source) as image:
            image = image.convert('RGBA')
            deltas = [max(hi for lo, hi in ImageChops.difference(image, sample).getextrema())
                for sample in (decoded, decoded.transpose(Image.Transpose.FLIP_TOP_BOTTOM))]
        if min(deltas) > 1:
            raise ValueError('Original shadow native texture disagrees with the verified export.')
        target = DEST / 'Textures' / (texture_source.stem + '-' + texture_guid + '.asset')
        new_texture_guid = save(target, raw, 'NativeFormatImporter')
        text = text.replace(texture_guid, new_texture_guid).replace(
            'guid: ' + new_texture_guid + ', type: 3', 'guid: ' + new_texture_guid + ', type: 2')
        result = save(DEST / 'Sprites' / (source.stem + '-' + old_guid + '.asset'), text.encode(), 'NativeFormatImporter')
        dependency_guids[old_guid] = result
        sprite_bundle = re.search(r'(?m)^  assetBundleName: (.+)$', read(candidates[0]).decode())[1].strip() + '.bundle'
        sprite_objects = [item for item in apk.objects(sprite_bundle) if item.type.name == 'Sprite'
                          and item.read().m_Name == source.stem]
        if len(sprite_objects) != 1:
            raise ValueError('Original shadow Sprite is ambiguous: ' + sprite_bundle)
        originals.append(dict(sprite=apk.provenance(sprite_objects[0]), sprite_sha256=sha(sprite_objects[0].get_raw_data()),
            texture=apk.provenance(obj), texture_sha256=sha(obj.get_raw_data())))
        converted.append(dict(record=record, converted=identity(native), png_channel_max_delta=min(deltas)))
        return result

    source_catalog = ROOT / 'unity/Assets/Game/Resources/Combat/character-visibility-bindings.json'
    catalog = json.loads(source_catalog.read_bytes())
    follower_script = ROOT / 'unity/Assets/Game/Runtime/Official/BattleSpinePlayables/AttachmentFollower.cs'
    follower_guid = GUID.search(Path(str(follower_script) + '.meta').read_text(encoding='utf-8-sig'))[1]
    bone_script = ROOT / 'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleShadowBoneFollower.cs'
    bone_guid = GUID.search(Path(str(bone_script) + '.meta').read_text(encoding='utf-8-sig'))[1]
    rows = []
    for role in catalog['roles']:
        shadow_paths = [path for path in role['parts'] if path and '/ShadowBoneFollower' in path]
        if not shadow_paths:
            continue
        kind = 'FightCharacter' if role['prefab'] in ('3100', '3102', '3991') else 'FightMonster'
        source = EXPORT / 'Prefabs/EcEntity/Fight' / kind / (role['prefab'] + '.prefab')
        raw = read(source).decode('utf-8-sig')
        docs = documents(source)
        gos = {key: data['GameObject'] for key, data in docs.items() if 'GameObject' in data}
        transforms = {key: data['Transform'] for key, data in docs.items() if 'Transform' in data}
        roots = [key for key, transform in transforms.items()
                 if gos[transform['m_GameObject']['fileID']]['m_Name'] == 'ShadowBoneFollower']
        if len(roots) != 1:
            raise ValueError(f'Expected one original shadow subtree in {source}: {roots}')
        root_id = roots[0]
        parent_id = transforms[root_id]['m_Father']['fileID']
        parent_path, ancestors = [], []
        while parent_id:
            node = transforms[parent_id]
            go = gos[node['m_GameObject']['fileID']]
            parent_path.insert(0, go['m_Name'])
            ancestors.insert(0, dict(name=go['m_Name'], active=bool(go['m_IsActive']),
                position=list(node['m_LocalPosition'].values()), rotation=list(node['m_LocalRotation'].values()),
                scale=list(node['m_LocalScale'].values())))
            parent_id = transforms[parent_id]['m_Father']['fileID']
        selected = set()
        pending = [root_id]
        while pending:
            transform_id = pending.pop()
            transform = transforms[transform_id]
            go_id = transform['m_GameObject']['fileID']
            selected.add(go_id)
            selected.update(item['component']['fileID'] for item in gos[go_id]['m_Component'])
            pending.extend(child['fileID'] for child in transform['m_Children'])
        pieces, followers, renderer_bindings = [], 0, []
        for match in re.finditer(r'(?ms)^--- !u!(\d+) &(\d+)\n(.*?)(?=^--- !u!|\Z)', raw):
            class_id, file_id, body = int(match[1]), int(match[2]), match[3]
            if file_id not in selected:
                continue
            if class_id not in (1, 4, 114, 212):
                raise ValueError('Restore an additional original shadow component: ' + match[1])
            if file_id == root_id:
                body = re.sub(r'm_Father: \{fileID: \d+\}', 'm_Father: {fileID: 0}', body)
            if class_id == 114:
                component = docs[file_id]['MonoBehaviour']
                script = component['m_Script']['fileID']
                if script == -1421533442 and not component['Gos']:
                    mapped, field = follower_guid, 'SkeletonRenderer'
                elif script == 879447986:
                    expected = dict(followXYPosition=1, followZPosition=0, followBoneRotation=0,
                        followSkeletonFlip=0, followParentWorldScale=0, maintainedAxisOrientation=1)
                    if any(component[key] != value for key, value in expected.items()):
                        raise ValueError(('Restore the additional original bone-follower branch.', role['prefab'], component))
                    mapped, field = bone_guid, 'skeletonRenderer'
                else:
                    raise ValueError(('Restore the additional original shadow follower contract.',
                        role['prefab'], component['m_Script'], component.get('Gos')))
                body = re.sub(r'm_Script: \{[^\n]+\}',
                    'm_Script: {fileID: 11500000, guid: ' + mapped + ', type: 3}', body)
                body = re.sub(field + r': \{fileID: \d+\}', field + ': {fileID: 0}', body)
                followers += 1
            if class_id == 212:
                component = docs[file_id]['SpriteRenderer']
                if component['m_Materials'] != [dict(fileID=10754, guid='0000000000000000f000000000000000', type=0)]:
                    raise ValueError('Restore the original additional shadow material.')
                old = component['m_Sprite']['guid']
                body = body.replace(old, sprite(old))
                renderer_transform = next(key for key in transforms
                    if transforms[key]['m_GameObject'] == component['m_GameObject'])
                path = []
                while renderer_transform != root_id:
                    node = transforms[renderer_transform]
                    path.insert(0, gos[node['m_GameObject']['fileID']]['m_Name'])
                    renderer_transform = node['m_Father']['fileID']
                renderer_bindings.append(dict(path='/'.join(path), layerIndex=component['m_SortingLayer'],
                    layerId=component['m_SortingLayerID'], order=component['m_SortingOrder']))
            # All local references must stay in the recovered subtree. The
            # original external skeleton pointer is the sole explicit rebind.
            for pointer in re.findall(r'\{fileID: (-?\d+)\}', body):
                if int(pointer) and int(pointer) not in selected:
                    raise ValueError('Unbound local original shadow reference: ' + pointer)
            pieces.append(f'--- !u!{class_id} &{file_id}\n' + body)
        if followers != 1:
            raise ValueError('Original shadow has an unexpected follower count: ' + role['prefab'])
        target = DEST / 'Prefabs' / (role['prefab'] + '.prefab')
        save(target, ('%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n' + ''.join(pieces)).encode(), 'PrefabImporter')
        rows.append(dict(prefab=role['prefab'], name=gos[transforms[root_id]['m_GameObject']['fileID']]['m_Name'],
                         parent='/'.join(parent_path[1:]), ancestors=ancestors[1:],
                         resource='Combat/CharacterShadows/Prefabs/' + role['prefab'], renderers=renderer_bindings))
    write_changed(DATA, json.dumps(dict(manifest=apk.version, roles=rows), indent=2) + '\n')
    meta(DATA)
    outputs.update((DATA, Path(str(DATA) + '.meta')))
    receipt = dict(manifest=apk.version, generator=identity(Path(__file__)),
        original_export=identity(EXPORT_RECEIPT), visibility_catalog=identity(source_catalog),
        texture_conversion=identity(texture_receipt), native_inputs=list(inputs.values()), originals=originals,
        converted=converted, follower=identity(follower_script), follower_meta=identity(Path(str(follower_script) + '.meta')),
        bone_follower=identity(bone_script), bone_follower_meta=identity(Path(str(bone_script) + '.meta')),
        outputs=[identity(path) for path in sorted(outputs)],
        operation='Recover every configured original ShadowBoneFollower subtree, retaining all GameObjects, '
            'Transforms, Sprite geometry, renderer fields and original follower settings. AttachmentFollower '
            'uses the migrated application implementation; the restricted shadow BoneFollower configuration '
            'uses independent Unity spatial math over live native bone ports. Rebind only '
            'the external skeleton pointer at runtime. Preserve native full-mip texture data and original Sprite '
            'vertices/UVs/pivot/rect; no replacement art or sampled follower positions.')
    write_changed(RECEIPT, json.dumps(receipt, ensure_ascii=False, indent=2) + '\n')
    print('Recovered original configured shadow subtrees:', len(rows), 'Sprite dependencies:', len(dependency_guids))


if __name__ == '__main__':
    main()
