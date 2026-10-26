"""Restore Unity coordinates and avoid drawing the serialized Spine mesh twice.

Historical geometry was exported for a canvas with Y/V pointing down. Native
Spine frames retain Y-up bones but also use top-origin atlas UVs. Check native
prefab meshes before removing baked meshes or converting their UNorm8 colors.
"""
import hashlib
import json
import struct
from pathlib import Path

import numpy as np
from official_apk import Apk, ROOT
from map10_geometry import decode, version
from prepare_unity_map11_base_ground import encoded, write
from UnityPy.helpers.MeshHelper import MeshHandler

DEST = ROOT / 'unity/Assets/Official/World/Map/12'


def texture_imports(apk):
    from PIL import Image
    records = {}
    for name in ('models', 'extra-models', 'story-actors', 'materials-12', 'minimaps'):
        data = json.loads((ROOT / f'assets/reference/official-map/{name}-source-manifest.json').read_bytes())
        for uri, record in data.get('resources', {}).items():
            records[Path(uri).name] = record
    base = json.loads((DEST / 'base-ground-source-manifest.json').read_bytes())
    for record in base['sprites']:
        records[Path(record['path']).name] = record['source']
    objects, imports = {}, []
    # Dialogue and light lookup textures have independent native import receipts.
    # Only these three source families use the world geometry import adapter.
    paths = [path for directory in ('Textures', 'BaseGround', 'MiniMapTextures')
             for path in (DEST / 'Resources/Map12' / directory).glob('*.png')]
    for path in sorted(paths):
        record = records[path.name]
        if record.get('origin') == 'derived_official':
            record = record['source']
        bundle = record['bundle']
        if bundle not in objects:
            objects[bundle] = {str(o.path_id): o for o in apk.objects(bundle)}
        obj = objects[bundle][record['path_id']]
        source = obj
        if obj.type.name == 'Sprite':
            sprite = obj.read()
            rd = sprite.m_RD
            if sprite.m_SpriteAtlas:
                atlas = sprite.m_SpriteAtlas.read()
                rd = next(v for k, v in atlas.m_RenderDataMap if k == sprite.m_RenderDataKey)
            elif not rd.texture.m_PathID and sprite.m_AtlasTags:
                atlas = next(o.read() for o in obj.assets_file.objects.values()
                    if o.type.name == 'SpriteAtlas' and o.peek_name() == sprite.m_AtlasTags[0])
                rd = next(v for k, v in atlas.m_RenderDataMap if k == sprite.m_RenderDataKey)
            obj = rd.texture.deref()
        if obj.type.name != 'Texture2D':
            raise ValueError('Restore native texture import source: ' + path.name)
        texture = obj.read()
        settings = texture.m_TextureSettings
        with Image.open(path) as image:
            width, height = image.size
        imports.append(dict(path=path.relative_to(ROOT / 'unity').as_posix(), width=width, height=height,
            filter=settings.m_FilterMode, wrapU=settings.m_WrapU, wrapV=settings.m_WrapV,
            srgb=texture.m_ColorSpace == 1, source=apk.provenance(source), texture=apk.provenance(obj)))
    return imports


def transform(t):
    q = t.m_LocalRotation
    x, y, z, w = q.x, q.y, q.z, q.w
    m = np.array([[1-2*y*y-2*z*z, 2*x*y-2*z*w, 2*x*z+2*y*w, t.m_LocalPosition.x],
        [2*x*y+2*z*w, 1-2*x*x-2*z*z, 2*y*z-2*x*w, t.m_LocalPosition.y],
        [2*x*z-2*y*w, 2*y*z+2*x*w, 1-2*x*x-2*y*y, t.m_LocalPosition.z], [0, 0, 0, 1.]])
    return m @ np.diag([t.m_LocalScale.x, t.m_LocalScale.y, t.m_LocalScale.z, 1])


def native_meshes(apk, model_path):
    parts = ('Prefabs/' + model_path).split('/')
    names = ['defaultpackage_assets_' + '_'.join(parts[:n]).lower() + '.bundle'
             for n in range(len(parts)-1, 0, -1)]
    bundle = next((name for name in names if name in apk.index), None)
    if bundle is None:
        raise ValueError('Resolve original model bundle: ' + model_path)
    root = apk.asset(bundle, '/' + model_path.split('/')[-1] + '.prefab')
    records = []

    def visit(obj, parent):
        go = obj.read()
        components = [c.component.deref() for c in go.m_Component]
        t = next((c.read() for c in components if c.type.name in ('Transform', 'RectTransform')), None)
        if t is None:
            return
        matrix = parent @ transform(t)
        for c in components:
            if c.type.name != 'MeshFilter' or not c.read().m_Mesh.m_PathID:
                continue
            mesh_obj = c.read().m_Mesh.deref()
            mesh = mesh_obj.read()
            h = MeshHandler(mesh)
            h.process()
            points = (matrix @ np.array([list(v[:3])+[1] for v in h.m_Vertices]).T).T[:, :2]
            points[:, 1] *= -1
            uv = np.array(h.m_UV0)
            if uv.size == 0:
                continue
            uv[:, 1] = 1 - uv[:, 1]
            kinds = [v.read(check_read=False).m_Script.read().m_ClassName for v in components
                     if v.type.name == 'MonoBehaviour']
            records.append(dict(points=points, uv=uv, colors=h.m_Colors,
                spine=any(k in ('SkeletonAnimation', 'SkeletonMecanim', 'SkeletonRenderer') for k in kinds),
                unorm8=mesh.m_VertexData.m_Channels[3].format == 2,
                source=apk.provenance(mesh_obj), renderer=apk.provenance(c), node=go.m_Name))
        for child in t.m_Children:
            visit(child.read().m_GameObject.deref(), matrix)
    visit(root, np.eye(4))
    return records


def geometry_bytes(batches):
    result = bytearray(b'ZJMAP001' + struct.pack('<II', len(batches), 0))
    for index, b in enumerate(batches):
        raw = json.dumps(b['metadata'], separators=(',', ':')).encode('utf8')
        if index:
            result.extend(struct.pack('<I', index))
        else:
            struct.pack_into('<I', result, 12, len(raw))
        result.extend(raw)
        result.extend(struct.pack('<I', len(b['positions'])//2))
        for key in ('positions', 'uvs', 'colors'):
            result.extend(struct.pack('<' + str(len(b[key])) + 'f', *b[key]))
        result.extend(struct.pack('<I', len(b['indices'])))
        result.extend(struct.pack('<' + str(len(b['indices'])) + 'I', *b['indices']))
    return bytes(result)


def restore_batch(b):
    b['positions'] = [value if i % 2 == 0 else -value for i, value in enumerate(b['positions'])]
    b['uvs'] = [value if i % 2 == 0 else 1-value for i, value in enumerate(b['uvs'])]
    x, y, w, h = b['metadata']['bounds']
    b['metadata']['bounds'] = [x, -y-h, w, h]
    return b


def build():
    apk = Apk.current()
    catalog_path = DEST / 'map12-entities.json'
    catalog = json.loads(catalog_path.read_bytes())
    receipts, output_hashes = [], {}
    removed, normalized = 0, 0
    for model in catalog['models']:
        natives = native_meshes(apk, model['modelPath'])
        variants = model.get('states') or [model]
        for variant in variants:
            resource = variant.get('geometryResource')
            if not resource:
                continue
            path = DEST / ('Resources/' + resource + '.bytes')
            raw = path.read_bytes()
            bs = decode(raw, version(raw))
            kept = []
            for b in bs:
                xy = np.array(b['positions']).reshape(-1, 2)
                uv = np.array(b['uvs']).reshape(-1, 2)
                matches = [n for n in natives if n['points'].shape == xy.shape and n['uv'].shape == uv.shape
                    and np.allclose(n['points'], xy, atol=0.0001) and np.allclose(n['uv'], uv, atol=0.00001)]
                native = matches[0] if matches else None
                if native and native['spine']:
                    removed += 1
                    receipts.append(dict(model=model['modelPath'], action='omit serialized mesh overwritten by native Spine component',
                        mesh=native['source'], component=native['renderer'], node=native['node']))
                    continue
                if max(b['colors']) > 1:
                    if native is None or not native['unorm8'] or not np.allclose(
                            np.array(native['colors']).flatten(), b['colors']):
                        raise ValueError('Unverified byte-color mesh: ' + model['modelPath'])
                    b['colors'] = [c/255 for c in b['colors']]
                    normalized += 1
                    receipts.append(dict(model=model['modelPath'], action='decode native UNorm8 vertex colors as Color floats',
                        mesh=native['source'], component=native['renderer']))
                kept.append(restore_batch(b))
            variant['geometryBatchCount'] = len(kept)
            if kept:
                new = geometry_bytes(kept)
                target_resource = resource.replace('/EntityGeometry/', '/NativeGeometry/')
                target = DEST / ('Resources/' + target_resource + '.bytes')
                variant['geometryResource'] = target_resource
                write(target, new)
                output_hashes[target.relative_to(ROOT).as_posix()] = hashlib.sha256(new).hexdigest()
            else:
                variant['geometryResource'] = None
        if model.get('states'):
            model['geometryBatchCount'] = next((v['geometryBatchCount'] for v in model['states'] if v['state'] == 0), 0)
        for variant in variants:
            for spine in variant['spines']:
                spine['resource'] = spine['resource'].replace('/Spines/', '/NativeSpines/')
    path = DEST / 'map12.geom.bytes'
    raw = path.read_bytes()
    bs = decode(raw, version(raw))
    if any(max(b['colors']) > 1 for b in bs):
        raise ValueError('Terrain byte colors require native mesh validation')
    target = DEST / 'map12-native.geom.bytes'
    write(target, geometry_bytes([restore_batch(b) for b in bs]))
    output_hashes[target.relative_to(ROOT).as_posix()] = hashlib.sha256(target.read_bytes()).hexdigest()
    for path in sorted((DEST / 'Resources/Map12/Spines').glob('*.json')):
        data = json.loads(path.read_bytes())
        for frame in data['frames']:
            for attachment in frame['attachments']:
                attachment['uv'] = [v if i % 2 == 0 else 1-v for i, v in enumerate(attachment['uv'])]
        target = path.parent.parent / 'NativeSpines' / path.name
        write(target, encoded(data))
        output_hashes[target.relative_to(ROOT).as_posix()] = hashlib.sha256(target.read_bytes()).hexdigest()
    for path in (DEST / 'map12-native-entities.json', DEST / 'Resources/Map12/Entities/native-catalog.json'):
        write(path, encoded(catalog))
        output_hashes[path.relative_to(ROOT).as_posix()] = hashlib.sha256(path.read_bytes()).hexdigest()
    from prepare_unity_map11_rendering import text_meta, folder_meta
    for directory in (DEST / 'Resources/Map12/NativeGeometry', DEST / 'Resources/Map12/NativeSpines'):
        write(Path(str(directory)+'.meta'), folder_meta(directory.relative_to(DEST).as_posix(), 12))
    for relative in output_hashes:
        target = ROOT / relative
        write(Path(str(target)+'.meta'), text_meta(target.relative_to(DEST).as_posix(), 12))
    imports = texture_imports(apk)
    write(DEST / 'texture-imports.json', encoded(dict(textures=imports)))
    from prepare_unity_map11_rendering import text_meta
    write(DEST / 'texture-imports.json.meta', text_meta('texture-imports.json', 12))
    receipt = dict(manifest=apk.version, mapClassId=12,
        operation='Convert historical downward Y and top-origin V to Unity; native bone Y stays unchanged. Exclude only baked meshes matched to their current official Spine components. Normalize only verified UNorm8 vertex colors.',
        nativeMeshChecks=receipts, omittedBakedSpineMeshes=removed, normalizedByteColorMeshes=normalized,
        textureImports=imports, outputs=output_hashes)
    write(ROOT / 'assets/reference/official-map/map12-coordinate-source-manifest.json', encoded(receipt))
    apk.archive.close()
    print(f'Map12 native coordinates: {removed} duplicate Spine meshes omitted, {normalized} byte colors normalized', flush=True)


if __name__ == '__main__':
    build()
