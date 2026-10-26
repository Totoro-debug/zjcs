"""Recover the missing native Map11 base ground from its pinned Tilemap.

The legacy region geometry contains decorations but omits this serialized Tilemap.
Keep native sprite polygons, tile matrices/colors, hierarchy and renderer provenance.
"""
from pathlib import Path
import io,json,struct,hashlib
import numpy as np
from official_apk import Apk,ROOT
from official_unity_schema import ScriptSchemas
from UnityPy.helpers.MeshHelper import MeshHandler
from prepare_unity_map11_rendering import texture_meta,text_meta,folder_meta

OUTPUT=ROOT/'unity/Assets/Official/World/Map/11'
REL='Resources/Map11/BaseGround'
BUNDLE='defaultpackage_assets_partition_mapground_11_mapground_11_bare.bundle'

def sha(raw):return hashlib.sha256(raw).hexdigest()
def write(path,raw):
    path.parent.mkdir(parents=True,exist_ok=True)
    if not path.exists() or path.read_bytes()!=raw:path.write_bytes(raw)
def encoded(obj):return (json.dumps(obj,ensure_ascii=False,sort_keys=True,indent=2)+'\n').encode('utf-8')
def matrix(value):return np.array([[getattr(value,f'e{r}{c}') for c in range(4)] for r in range(4)])

def build(map_id=11):
    if map_id not in (11, 12):raise ValueError('Only maps 11 and 12 are supported')
    OUTPUT=ROOT/f'unity/Assets/Official/World/Map/{map_id}'
    REL=f'Resources/Map{map_id}/BaseGround'
    BUNDLE=f'defaultpackage_assets_partition_mapground_{map_id}_mapground_{map_id}_bare.bundle'
    apk=Apk.current() if map_id==12 else Apk()
    code_root=ROOT/f'.tools/source-packages/current/{apk.version}/official-code' if map_id==12 else ROOT/'.tools/source-packages/official-code'
    apk.load(BUNDLE)
    sources=[o for o in apk.env.objects if o.type.name=='Tilemap' and o.read().m_GameObject.read().m_Name=='Ground']
    if len(sources)!=1:raise ValueError('Expected one native base Ground Tilemap')
    source=sources[0];tilemap=source.read();go=tilemap.m_GameObject.read()
    renderer=next(c.component.deref() for c in go.m_Component if c.component.type.name=='TilemapRenderer')
    rd=renderer.read();mat=rd.m_Materials[0].deref();shader=mat.read().m_Shader.deref()
    if shader.read().m_ParsedForm.m_Name!='Universal Render Pipeline/2D/Sprite-Lit-Default':raise ValueError('Unexpected ground shader')
    tr=next(c.component.read() for c in go.m_Component if c.component.type.name=='Transform')
    transforms=[]
    while tr:
        if [getattr(tr.m_LocalPosition,k) for k in 'xyz']!=[0,0,0] or [getattr(tr.m_LocalScale,k) for k in 'xyz']!=[1,1,1] or [getattr(tr.m_LocalRotation,k) for k in 'xyzw']!=[0,0,0,1]:
            raise ValueError('Native ground hierarchy changed; explicitly migrate its transform')
        transforms.append(apk.provenance(tr.object_reader))
        tr=tr.m_Father.read() if tr.m_Father.m_PathID else None
    orientation=matrix(tilemap.m_TileOrientationMatrix)
    sprites=[];resources=[]
    for entry in tilemap.m_TileSpriteArray:
        obj=entry.m_Data.deref();sprite=obj.read()
        h=MeshHandler(sprite.m_RD,version=obj.version);h.process()
        vertices=np.array(h.m_Vertices)
        stream=io.BytesIO();image=sprite.image;image.save(stream,format='PNG');raw=stream.getvalue()
        name='BaseGround-'+sha(raw)[:16]+'.png';path=OUTPUT/REL/name
        write(path,raw)
        metadata=Path(str(path)+'.meta')
        if map_id!=12 or not metadata.exists():write(metadata,texture_meta(REL+'/'+name,map_id))
        ppu=sprite.m_PixelsToUnits;rect=sprite.m_Rect;pivot=sprite.m_Pivot
        uv=[[float(v[0]*ppu/rect.width+pivot.x),float(v[1]*ppu/rect.height+pivot.y)] for v in vertices]
        indices=[i for sub in h.get_triangles() for triangle in sub for i in triangle]
        sprites.append((vertices,uv,indices,name))
        resources.append({'path':REL+'/'+name,'sha256':sha(raw),'source':apk.provenance(obj),'vertices':len(vertices),'indices':len(indices)})
    batches={}
    for pos,tile in tilemap.m_Tiles:
        v,uv,indices,name=sprites[tile.m_TileSpriteIndex]
        batch=batches.setdefault(name,{'positions':[],'uvs':[],'indices':[],'colors':[]})
        first=len(batch['positions'])//2
        points=np.concatenate((v,np.ones((len(v),1))),axis=1)
        points=(orientation@matrix(tilemap.m_TileMatrixArray[tile.m_TileMatrixIndex].m_Data)@points.T).T
        color=tilemap.m_TileColorArray[tile.m_TileColorIndex].m_Data
        tint=[getattr(color,k)*getattr(tilemap.m_Color,k) for k in 'rgba']
        for point in points:
            batch['positions'].extend([float(point[0]+pos.x+tilemap.m_TileAnchor.x),float(point[1]+pos.y+tilemap.m_TileAnchor.y)])
            batch['colors'].extend(tint)
        batch['uvs'].extend(x for pair in uv for x in pair)
        batch['indices'].extend(first+i for i in indices)
    output=bytearray(b'ZJMAP001'+struct.pack('<II',len(batches),0))
    for index,(name,b) in enumerate(sorted(batches.items())):
        xy=np.array(b['positions']).reshape(-1,2);lo=xy.min(axis=0);hi=xy.max(axis=0)
        meta={'texture':'res://assets/official/sprites/'+name,'layer':rd.m_SortingLayer,'order':rd.m_SortingOrder,
              'depth':0.0,'bounds':[float(lo[0]),float(lo[1]),float(hi[0]-lo[0]),float(hi[1]-lo[1])],
              'shader':shader.read().m_ParsedForm.m_Name}
        raw=json.dumps(meta,separators=(',',':')).encode('utf-8')
        if index:output.extend(struct.pack('<I',index))
        else:struct.pack_into('<I',output,12,len(raw))
        output.extend(raw);output.extend(struct.pack('<I',len(b['positions'])//2))
        for key,fmt in [('positions','f'),('uvs','f'),('colors','f')]:output.extend(struct.pack('<'+str(len(b[key]))+fmt,*b[key]))
        output.extend(struct.pack('<I',len(b['indices'])));output.extend(struct.pack('<'+str(len(b['indices']))+'I',*b['indices']))
    path=OUTPUT/REL/'geometry.bytes';write(path,bytes(output));write(Path(str(path)+'.meta'),text_meta(REL+'/geometry.bytes',map_id))
    receipt={'manifest':apk.version,'mapClassId':map_id,'tilemap':apk.provenance(source),'renderer':apk.provenance(renderer),
             'material':apk.provenance(mat),'shader':apk.provenance(shader),'hierarchy':transforms,
             'tileCount':len(tilemap.m_Tiles),'batchCount':len(batches),'sprites':resources,
             'geometry':{'path':REL+'/geometry.bytes','sha256':sha(output)},
             'operation':'Read native Sprite polygon vertices; apply original tile matrix, orientation, anchor and colors in unchanged Unity world coordinates.',
             'limitations':['Existing region renderer material adaptation does not yet restore original URP lighting. This geometry receipt is not a complete rendering claim.']}
    path=OUTPUT/'base-ground-source-manifest.json';write(path,encoded(receipt));write(Path(str(path)+'.meta'),text_meta('base-ground-source-manifest.json',map_id))
    player=apk.asset('defaultpackage_assets_prefabs_ecentity_mapworld_worldmapcharacter.bundle','/3001.prefab')
    def child(root,name):
        tr=next(c.component.read() for c in root.read().m_Component if c.component.type.name=='Transform')
        return next(ref.read().m_GameObject.deref() for ref in tr.m_Children if ref.read().m_GameObject.read().m_Name==name)
    model=player
    for name in ('3001_view','Model','Offset','Player_Model_Map'):model=child(model,name)
    renderer=next(c.component.deref() for c in model.read().m_Component if c.component.type.name=='MeshRenderer')
    transform=next(c.component.deref() for c in model.read().m_Component if c.component.type.name=='Transform')
    offset=transform.read().m_Father.deref()
    view={'modelScale':{k:getattr(transform.read().m_LocalScale,k) for k in 'xyz'},
          'modelOffset':{k:getattr(offset.read().m_LocalPosition,k) for k in 'xyz'},
          'sortingOrder':renderer.read().m_SortingLayer*1000+renderer.read().m_SortingOrder}
    camera_root=apk.asset('defaultpackage_assets_prefabs_map_common_entityui.bundle','/WorldCamera.prefab')
    controller=child(camera_root,'controller')
    adaptor=next(c.component.deref() for c in controller.read().m_Component if c.component.type.name=='MonoBehaviour'
                 and c.component.deref().read(check_read=False).m_Script.read().m_ClassName=='OrthographicCameraResolutionAdaptor')
    camera=next(c.component.deref() for c in child(camera_root,'Main Camera').read().m_Component if c.component.type.name=='Camera')
    adapter_fields=ScriptSchemas(code_root=code_root).read(adaptor)
    view.update(cameraSafeExtents=adapter_fields['mySafeArea']['m_Extent'],
                cameraAdaptPriority=adapter_fields['mAdapterPriority'],
                cameraDefaultSize=camera.read().orthographic_size,
                cameraNear=camera.read().near_clip_plane,cameraFar=camera.read().far_clip_plane)
    path=OUTPUT/REL/'player-view.json';raw=encoded(view);write(path,raw);write(Path(str(path)+'.meta'),text_meta(REL+'/player-view.json',map_id))
    receipt['playerView']={'root':apk.provenance(player),'model':apk.provenance(model),'renderer':apk.provenance(renderer),
                          'transform':apk.provenance(transform),'offset':apk.provenance(offset),'sha256':sha(raw)}
    receipt['cameraView']={'root':apk.provenance(camera_root),'camera':apk.provenance(camera),'resolutionAdaptor':apk.provenance(adaptor),
                          'code':[]}
    for name in ('WorldCameraBaseController','OrthographicCameraResolutionAdaptor'):
        p=code_root/f'client-source/{name}.cs'
        receipt['cameraView']['code'].append({'path':p.relative_to(ROOT).as_posix(),'sha256':sha(p.read_bytes())})
    path=OUTPUT/'base-ground-source-manifest.json';write(path,encoded(receipt))
    write(Path(str(OUTPUT/REL)+'.meta'),folder_meta(REL,map_id))
    print(f'Native Map{map_id} base ground: {len(tilemap.m_Tiles)} tiles, {len(batches)} batches, {len(sprites)} source sprites; {sha(output)}')
if __name__=='__main__':build()
