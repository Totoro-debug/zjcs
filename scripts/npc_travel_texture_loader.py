"""Strict original YooAssetTextureLoader reader, isolated from shared schemas.

The two base PPtrs follow Spine.Unity.OnDemandTextureLoader 4.2. The generic
AppearanceOnDemandTextureLoader and AssetProxyBase fields follow the pinned
decompiled assemblies. Every byte and every GUID is checked before use.
"""
import hashlib
import re
import struct
from UnityPy.classes.PPtr import PPtr


def read_loader(obj, rows, apk):
    raw = obj.get_raw_data()
    at = 28

    def string():
        nonlocal at
        size = struct.unpack_from('<i', raw, at)[0]
        if size < 0 or at + 4 + size > len(raw):
            raise ValueError('Truncated original texture loader string')
        value = raw[at + 4:at + 4 + size].decode('utf8')
        at = (at + 4 + size + 3) & ~3
        return value

    def pointer():
        nonlocal at
        file_id, path_id = struct.unpack_from('<iq', raw, at)
        at += 12
        return PPtr(m_FileID=file_id, m_PathID=path_id, assetsfile=obj.assets_file)

    name = string()
    atlas, skeleton = pointer(), pointer()
    maximum, unused, count = struct.unpack_from('<ifi', raw, at)
    at += 12
    if not 0 <= count <= 10000 or maximum <= 0 or unused < 0:
        raise ValueError('Invalid original texture loader header')
    by_guid = {r['guid']: r for r in rows}
    mappings = []
    for _ in range(count):
        placeholder = pointer()
        target_guid, dye_guid = string(), string()
        for guid in (target_guid, dye_guid):
            if guid and (not re.fullmatch('[a-f0-9]{32}', guid) or guid not in by_guid):
                raise ValueError('Original loader GUID absent from pinned AssetTable: ' + guid)
        texture = placeholder.read()
        if placeholder.type.name != 'Texture2D':
            raise ValueError('Original placeholder is not a Texture2D')
        mappings.append(dict(placeholder=texture.m_Name, source=apk.provenance(placeholder.deref()),
            target=by_guid[target_guid] if target_guid else None,
            dye=by_guid[dye_guid] if dye_guid else None))
    resolution = struct.unpack_from('<i', raw, at)[0]
    at += 4
    paths = [string(), string(), string()]
    # ManagedReferencesRegistry v2, zero entries (the original type has none).
    trailer = raw[at:]
    if trailer not in (b'', struct.pack('<ii', 2, 0)):
        raise ValueError('Unconsumed original loader bytes: ' + str((at, len(raw), trailer.hex())))
    return dict(name=name, source=apk.provenance(obj), raw_sha256=hashlib.sha256(raw).hexdigest(),
        consumed_bytes=len(raw), atlas=str(atlas.path_id), skeleton=str(skeleton.path_id),
        maximum_placeholder_size=maximum, unload_seconds=unused, resolution=resolution,
        paths=paths, mappings=mappings,
        base_schema_source='https://raw.githubusercontent.com/EsotericSoftware/spine-runtimes/4.2/spine-unity/Assets/Spine/Runtime/spine-unity/Asset%20Types/OnDemandTextureLoader.cs')


def apply_loaders(e, meshes, rows):
    loaders, receipts = {}, []
    for key, value in meshes.items():
        cab, object_id = key.split(':')[:2]
        bundle = e.apk.cabs[cab.lower()]['bundle']
        roots = [p.rsplit('/', 1)[0] for p, ptr in e.apk.container(bundle)
                 if str(ptr.path_id) == object_id and ptr.deref().assets_file.name == cab]
        if len(roots) != 1:
            raise ValueError('SkeletonDataAsset needs its exact container root')
        root = roots[0]
        candidates = [r for r in rows if r['path'].startswith(root + '/')
                      and r['path'].endswith('_TextureLoader_High.asset')]
        if len(candidates) != 1:
            raise ValueError('Exact original High texture loader not unique: ' + root)
        row = candidates[0]
        if root not in loaders:
            obj = e.apk.asset(row['bundle'], '/' + row['path'].rsplit('/', 1)[-1])
            e.input(obj)
            loaders[root] = read_loader(obj, rows, e.apk)
        loader = loaders[root]
        value['dye_textures'] = {}
        for page in sorted({m['page'] for f in value['frames'] for m in f}):
            matches = [m for m in loader['mappings'] if m['target'] and m['target']['path'].rsplit('/', 1)[-1] == page]
            if len(matches) != 1:
                raise ValueError('Page is not uniquely mapped by its actual loader: ' + page)
            mapping = matches[0]
            for field, destination in [('target', value['textures']), ('dye', value['dye_textures'])]:
                if not mapping[field]:
                    continue
                asset = mapping[field]
                obj = e.apk.asset(asset['bundle'], '/' + asset['path'].rsplit('/', 1)[-1])
                if obj.type.name != 'Texture2D':
                    raise ValueError('Original GUID target is not Texture2D')
                destination[page] = e.texture(PPtr(m_FileID=0, m_PathID=obj.path_id, assetsfile=obj.assets_file))
            receipts.append(dict(skeleton=key, page=page, loader=row, mapping=mapping))
    return dict(loaders=list(loaders.values()), pages=receipts)
