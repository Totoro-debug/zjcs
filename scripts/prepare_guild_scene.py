"""Convert the pinned LegionMap scene, including original building Spine meshes."""
import hashlib
import json
import subprocess
import numpy as np
from prepare_map_regions import Regions, save
from prepare_official_city import ART, ROOT, REFERENCE, transform
from official_resource_manifest import write_resources
from guild_asset_helpers import skeleton_job
from UnityPy.classes.PPtr import PPtr
import struct


class GuildSceneExporter(Regions):
    def skeleton(self, obj, matrix, path, renderer):
        raw = obj.get_raw_data()
        f, ident = struct.unpack_from('<iq', raw, 32)
        if not ident:
            return
        asset = PPtr(m_FileID=f, m_PathID=ident, assetsfile=obj.assets_file).deref()
        length = struct.unpack_from('<i', raw, 44)[0]
        identity = skeleton_job(self, asset, raw[48:48+length].decode())
        self.skeleton_instances.append(dict(id=identity, path=path, matrix=matrix.tolist(),
            sorting_layer=renderer.m_SortingLayer, sorting_order=renderer.m_SortingOrder))


def build():
    e = GuildSceneExporter()
    root = e.apk.asset('prefabs_legion', '/LegionMap.prefab')
    e.active_overrides = {}
    points, cameras, regions, labels, focuses = {}, {}, [], [], {}
    def visit(obj, matrix, path=''):
        go = obj.read()
        path += '/' + go.m_Name
        components = [c.component.deref() for c in go.m_Component]
        t = next(c.read() for c in components if c.type.name in ['Transform', 'RectTransform'])
        matrix = matrix @ transform(t)
        if go.m_Name in ['TipDotContent', 'Night']:
            e.active_overrides[obj.assets_file.name + ':' + str(obj.path_id)] = False
        if go.m_Name in ['NormalContent','BuildingContent','ConstructContent','NeedToBuildContent','LockContent']:
            e.active_overrides[obj.assets_file.name + ':' + str(obj.path_id)] = True
        if go.m_Name.startswith('LegionMapBuildingEntry'):
            points[go.m_Name] = matrix[:3, 3].tolist()
        for c in components:
            if c.type.name == 'BoxCollider' and go.m_Name == 'TouchGo':
                collider = c.read()
                center, size = collider.m_Center, collider.m_Size
                vertices = [(matrix @ [center.x + x*size.x/2, center.y + y*size.y/2, center.z, 1])[:2].tolist()
                            for x,y in [(-1,-1),(1,-1),(1,1),(-1,1)]]
                regions.append(dict(path=path, vertices=vertices, source=e.apk.provenance(c)))
            if c.type.name == 'MonoBehaviour':
                cls, raw, _ = e.mono(c)
                if cls == 'CameraFocusObject':
                    data = e.schemas.read(c)
                    focuses[go.m_Name] = dict(position=[data['Pos'][axis] for axis in ['x','y','z']],
                                             orthographic_size=data['OrthoSize'])
                if cls == 'TextMeshPro' and go.m_Name in ['NameTxt', 'LevelTxt'] and '/NameSlot/' in path:
                    labels.append(dict(path=path, matrix=matrix.tolist(), root=e.ui_node(obj), source=e.apk.provenance(c)))
                if cls == 'IgnoreUIDragController':
                    rectangles = struct.unpack_from('<i', raw, 104)[0]
                    offset = 108 + rectangles*24
                    cameras['border_center'] = list(struct.unpack_from('<3f', raw, offset))
                    cameras['border_extents'] = list(struct.unpack_from('<3f', raw, offset+12))
                    cameras['drag_sensitivity'] = struct.unpack_from('<f', raw, 44)[0]
                    cameras['drag_axes'] = [bool(raw[32]), bool(raw[36])]
                if cls == 'HomeCameraIniter':
                    cameras['position'] = matrix[:3, 3].tolist()
                    # Three PPtrs precede the inline resolution adaptor.
                    ratio_x, ratio_y, base_size = struct.unpack_from('<3f', raw, 68)
                    assert ratio_x > 0 and ratio_y > 0 and base_size > 0
                    cameras['aspect_ratio'] = [ratio_x, ratio_y]
                    cameras['orthographic_size'] = base_size
                if cls == 'OrthographicCameraExtendAdaptor':
                    # Preserve serialized LensSettings instead of assuming a zoom.
                    data = e.schemas.read(c)
                    cameras['adaptor'] = data
        for child in t.m_Children:
            visit(child.read().m_GameObject.deref(), matrix, path)
    visit(root, np.eye(4))
    layers = e.world('prefabs_legion', '/LegionMap.prefab')
    main_meshes, main_instances = e.meshes[:], e.skeleton_instances[:]
    life_worlds, life_sources = {}, {}
    for path, obj in e.apk.container('prefabs_ui_legionmap'):
        if '/World/' not in path or 'LegionMapLifeHangUpItem' not in path or obj.type.name != 'GameObject':
            continue
        e.meshes, e.skeleton_instances = [], []
        key = path.rsplit('/', 1)[-1].removesuffix('.prefab')
        life_worlds[key] = dict(layers=e.world('prefabs_ui_legionmap', path), meshes=e.meshes,
                               skeleton_instances=e.skeleton_instances)
        life_sources[key] = e.apk.provenance(obj.deref())
    cache = ROOT / '.tools/source-packages/guild-skeletons'
    cache.mkdir(exist_ok=True)
    save(cache / 'jobs.json', e.skeleton_jobs)
    subprocess.run(['node', str(ROOT / 'scripts/extract_home_meshes.mjs'), str(cache / 'jobs.json'), str(cache / 'meshes.json')], check=True)
    meshes = json.loads((cache / 'meshes.json').read_text())
    for ident, data in meshes.items():
        data['textures'] = e.skeleton_jobs[ident]['textures']
        data['scale'] = e.skeleton_jobs[ident]['scale']
    e.save('guild-scene.json', dict(layers=layers, meshes=main_meshes, skeletons=meshes,
        skeleton_instances=main_instances, buildings=points, camera=cameras, life_worlds=life_worlds,
        click_regions=regions, labels=labels, focuses=focuses))
    manifest = json.loads((REFERENCE / 'source-manifest.json').read_text(encoding='utf8'))
    manifest['resources'].update(e.records)
    manifest['data_sha256']['guild-scene.json'] = hashlib.sha256((ART / 'guild-scene.json').read_bytes()).hexdigest()
    save(REFERENCE / 'source-manifest.json', manifest)
    source = dict(version='854_156383', source=e.apk.provenance(root), resources=e.records,
        camera=cameras, buildings=points, life_worlds=life_sources, click_regions=regions, labels=labels, focuses=focuses,
        counts=dict(sprites=len(layers), meshes=len(main_meshes), skeletons=len(meshes), life_worlds=len(life_worlds)),
        unsupported={k:sorted(v) for k,v in e.unsupported.items()})
    save(ROOT / 'assets/reference/guild-ui/scene-manifest.json', source)
    write_resources(manifest)
    print('Guild scene:', source['counts'], flush=True)


if __name__ == '__main__':
    build()
