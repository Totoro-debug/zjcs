"""Convert native CustomWater renderers, textures and material settings offline."""
import hashlib
import json
import numpy as np
from official_apk import ROOT
from prepare_map_regions import Regions, ART, REF, save, transform
from UnityPy.classes.PPtr import PPtr
from UnityPy.helpers.MeshHelper import MeshHandler


def build():
    exporter = Regions()
    maps = json.loads((ART / 'adventure.json').read_text(encoding='utf8'))['tables']['Map/map']
    output = {}; cache = {}; failures = {}
    for row in maps:
        mid = str(row['ClassId'])
        path = ('Assets/Partition/' + row['MapGroundBarePath'] if row['MapGroundBarePath'] else 'Assets/Prefabs/Map/' + row['MapGroundPath']) + '.prefab'
        if path in cache:
            output[mid] = cache[path]; continue
        try:
            bundle, root = exporter.locate(path)
            records = []; controller = {}; matrices = {}; renderers = {}; generators = []
            def visit(obj, parent):
                go = obj.read(); cs = [c.component.deref() for c in go.m_Component]
                t = next((c.read() for c in cs if c.type.name in ['Transform', 'RectTransform']), None)
                if t is None: return
                world = parent @ transform(t)
                matrices[obj.path_id] = world
                for c in cs:
                    if c.type.name != 'MonoBehaviour': continue
                    kind = exporter.mono(c)[0]
                    if kind in ['WaterController', 'WaterMaskRenderer', 'WaterMaskGenerator']:
                        d = exporter.schemas.read(c); exporter.input(c)
                        if kind == 'WaterController': controller.update(d)
                        elif kind == 'WaterMaskGenerator': generators.append((c,d))
                        else:
                            pointers = d['LakeRenderers'][:]
                            if d['SeaRendererReplacement']['m_PathID']:
                                replacement = PPtr(**d['SeaRendererReplacement'], assetsfile=c.assets_file).deref()
                                exporter.input(replacement)
                                pointers += exporter.schemas.read(replacement)['SeaPatchs']
                            elif d['SeaRenderer']['m_PathID']: pointers.append(d['SeaRenderer'])
                            for p in pointers:
                                if p['m_PathID']:
                                    r = PPtr(**p, assetsfile=c.assets_file).deref()
                                    renderers[r.path_id] = r
                for child in t.m_Children: visit(child.read().m_GameObject.deref(), world)
            visit(root, np.eye(4))
            for obj in renderers.values():
                r = obj.read(); go = r.m_GameObject.read()
                mesh_filter = next((c.component.read() for c in go.m_Component if c.component.type.name == 'MeshFilter'), None)
                if mesh_filter is None or not mesh_filter.m_Mesh.path_id: continue
                mesh_obj = mesh_filter.m_Mesh.deref()
                if mesh_obj.assets_file.name == 'unity default resources':
                    raw = mesh_obj.get_raw_data()
                    exporter.source_records['unity default resources:'+str(mesh_obj.path_id)] = dict(origin='apk_zip',zip_entry='assets/bin/Data/unity default resources',path_id=str(mesh_obj.path_id),bytes=len(raw),sha256=hashlib.sha256(raw).hexdigest())
                else: exporter.input(mesh_obj)
                mesh = MeshHandler(mesh_obj.read()); mesh.process()
                m = matrices[r.m_GameObject.path_id]
                vertices = (m @ np.array([list(v[:3])+[1] for v in mesh.m_Vertices]).T).T
                for index, indices in enumerate(mesh.get_triangles()):
                    material = r.m_Materials[min(index,len(r.m_Materials)-1)].deref(); exporter.input(material)
                    d = material.read()
                    textures = {k:exporter.texture(v.m_Texture) for k,v in d.m_SavedProperties.m_TexEnvs if v.m_Texture.path_id and v.m_Texture.deref().assets_file.name != 'unity default resources'}
                    colors = {k:[v.r,v.g,v.b,v.a] for k,v in d.m_SavedProperties.m_Colors}
                    records.append(dict(name=go.m_Name,vertices=[[float(v[0]),-float(v[1])] for v in vertices],uv=[list(v) for v in (mesh.m_UV0 or [])],indices=np.array(indices).flatten().tolist(),material=d.m_Name,textures=textures,colors=colors,parameters=dict(d.m_SavedProperties.m_Floats)))
            # Preserve the exact normal-to-mask mapping for shoreline adaptation.
            masks = {}
            for obj, data in generators:
                if not data['Config']['m_PathID']: continue
                cfg_obj = PPtr(**data['Config'],assetsfile=obj.assets_file).deref(); exporter.input(cfg_obj)
                cfg = exporter.schemas.read(cfg_obj)
                for pair in cfg['Configs']:
                    if not pair['Normal']['m_PathID'] or not pair['Mask']['m_PathID']: continue
                    normal_ptr = PPtr(**pair['Normal'],assetsfile=cfg_obj.assets_file)
                    mask_ptr = PPtr(**pair['Mask'],assetsfile=cfg_obj.assets_file)
                    if normal_ptr.deref().assets_file.name == 'unity default resources' or mask_ptr.deref().assets_file.name == 'unity default resources': continue
                    normal = exporter.sprite(normal_ptr)
                    mask = exporter.sprite(mask_ptr)
                    masks[normal['file']] = dict(mask=mask['file'],special=bool(pair['Special']))
            value = dict(renderers=records,controller={k:v for k,v in controller.items() if not k.startswith('m_')},masks=masks)
            cache[path] = value; output[mid] = value
            print(mid,len(records),'water renderers',flush=True)
        except Exception as exc:
            failures[mid] = str(exc); print(mid,repr(exc),flush=True)
    save(ART/'map-water.json',output)
    save(REF/'water-source-manifest.json',dict(manifest='854_156383',maps=len(output),failures=failures,inputs=exporter.source_records,resources=exporter.records,schemas=exporter.schemas.records,outputs={'map-water.json':hashlib.sha256((ART/'map-water.json').read_bytes()).hexdigest()}))


if __name__ == '__main__': build()
