"""Convert the original whale island ground's 42 regional world labels."""
import json
import numpy as np

from official_apk import ROOT
from prepare_map_regions import Regions, save
from prepare_map_minimap_ui import MinimapExporterBase, world_prefab, digest
from prepare_home_decoration import components
from prepare_official_city import transform
from npc_travel_language import language


def build():
    exporter = MinimapExporterBase()
    exporter.minimap_sprite_directory = 'assets/official/map16-world-sprites'
    _, ground = Regions.locate(exporter,'Assets/Prefabs/Map/Map_16/MapGround_16.prefab')
    matrices = {}
    def layout(obj, parent=np.eye(4), parent_size=None, parent_pivot=None):
        cs=[c.component.deref() for c in obj.read().m_Component]
        component=next(c for c in cs if c.type.name in ['Transform','RectTransform'])
        t=component.read();local=transform(t);size=pivot=None
        if component.type.name=='RectTransform':
            pivot=np.array([t.m_Pivot.x,t.m_Pivot.y]);anchors=np.array([t.m_AnchorMax.x-t.m_AnchorMin.x,t.m_AnchorMax.y-t.m_AnchorMin.y])
            size=np.array([t.m_SizeDelta.x,t.m_SizeDelta.y])
            position=np.array([t.m_AnchoredPosition.x,t.m_AnchoredPosition.y])
            if parent_size is not None:
                size=size+parent_size*anchors
                position+=parent_size*(np.array([t.m_AnchorMin.x,t.m_AnchorMin.y])+anchors*pivot-parent_pivot)
            local[:2,3]=position
        matrix=parent@local;matrices[obj.path_id]=matrix
        for child in t.m_Children:layout(child.read().m_GameObject.deref(),matrix,size,pivot)
    layout(ground)
    items = []
    for go,path,matrix,cs in components(exporter,ground):
        support = next((c for c in cs if c.type.name == 'MonoBehaviour'
                        and exporter.mono(c)[0] == 'MapRegionDescSupportUI'),None)
        if support is None:
            continue
        exporter.input(support)
        prefab = world_prefab(exporter,go)
        # Unity references resolve against the complete ground hierarchy;
        # this prefab's runtime nodes start at its own root instead.
        prefix=path.rsplit('/',1)[0]
        def localize(value):
            if isinstance(value,str):return value[len(prefix):] if value.startswith(prefix+'/') else value
            if isinstance(value,list):return [localize(v) for v in value]
            if isinstance(value,dict):return {k:localize(v) for k,v in value.items()}
            return value
        prefab['bindings']=localize(prefab['bindings'])
        objects={path:obj for obj,path,_,_ in components(exporter,go)}
        for node in prefab['nodes']:
            node['matrix'] = matrices[objects[node['path']].path_id].tolist()
            if 'ui' in node:
                # The accumulated world matrix already includes this rotation.
                node['ui']['rotation'] = 0.0
        items.append(dict(path=path,position=matrix[:2,3].tolist(),prefab=prefab))
    if len(items) != 42:
        raise ValueError('Original whale island regional world labels changed')
    strings, languages = language(exporter.keys)
    if exporter.keys-set(strings):
        raise ValueError('Original world-label Chinese language is incomplete')
    target = ROOT/'assets/official/map16-world-ui.json'
    save(target,dict(manifest='854_156383',map=16,items=items,texts=strings))
    source = ROOT/'.tools/source-packages/official-code/client-source'
    code = {f'{name}.cs':digest(source/f'{name}.cs') for name in [
        'MapRegionDescSupportUI','MapRegionDescItemUI','MapRegionStrengthDisplayUI','UIColorByLevel']}
    save(ROOT/'assets/reference/official-map/world-ui-16-source-manifest.json',dict(
        manifest='854_156383',map=16,source=exporter.apk.provenance(ground),
        inputs=exporter.inputs,resources=exporter.records,schemas=exporter.schemas.records,
        language_inputs=languages,code_inputs=code,
        outputs={target.relative_to(ROOT).as_posix():digest(target)},
        operation='Original MapRegionDescSupportUI world transforms, TMP, sprites and managed state bindings'))
    print('Converted 42 original whale island regional world labels')


if __name__ == '__main__':
    build()
