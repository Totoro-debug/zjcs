"""Convert pinned mountain terrain materials, shoreline mask and battle-stele UI."""
import json
import hashlib
from copy import deepcopy
from prepare_map11_rendering import Map11Regions,terrain_renderers,bake_mask,digest,model_view
from prepare_map_regions import ART,REF,ROOT,save
from official_shader_programs import gles_programs

GROUND='Assets/Partition/MapGround_12/MapGround_12_bare.prefab'


class Map12Regions(Map11Regions):
    def __init__(self):
        super().__init__();self.sprite_textures={};self.lava_programs={}

    def sprite(self,ptr):
        result=super().sprite(ptr)
        if result:
            sprite=ptr.read();rd=sprite.m_RD
            if sprite.m_SpriteAtlas.path_id:rd=dict(sprite.m_SpriteAtlas.read().m_RenderDataMap)[sprite.m_RenderDataKey]
            self.sprite_textures[result['file']]=rd.texture
        return result

    def material(self,ptr):
        result=super().material(ptr)
        if result['shader']=='Sprites/Lava':
            obj=ptr.deref();self.input(obj);shader=obj.read().m_Shader.deref();self.input(shader)
            key=shader.assets_file.name+':'+str(shader.path_id)
            if key not in self.lava_programs:
                self.lava_programs[key]={'source':self.apk.provenance(shader),'raw_sha256':hashlib.sha256(shader.get_raw_data()).hexdigest(),'programs':gles_programs(shader.read())}
            result['textures']={name:self.texture(env.m_Texture) for name,env in obj.read().m_SavedProperties.m_TexEnvs if env.m_Texture.path_id}
        return result

    def bind_tiles(self,tiles,settings):
        super().bind_tiles(tiles,settings)
        for tile in tiles:
            if tile['materials'] and tile['materials'][0]['shader']=='Sprites/Lava':
                tile['lava_texture']=self.texture(self.sprite_textures[tile['sprite']['file']])


def build():
    destination=ROOT/'output/map12-material-build'
    destination.mkdir(parents=True,exist_ok=True)
    exporter=Map12Regions()
    previous={f'assets/official/regions/12.{suffix}':digest(ART/f'regions/12.{suffix}') for suffix in ['geom','json']}
    result=exporter.region(12,{'ground_path':GROUND},destination)
    if result['skeleton_instances']:raise ValueError('Convert original mountain base skeletons before installation')
    renderers=terrain_renderers(exporter,GROUND)
    water_renderers=[r for r in renderers if r['active'] and any(m['shader']=='Universal Render Pipeline/2D/Sprite-Lit-CustomWater2' for m in r['materials'])]
    water=deepcopy(json.loads((ART/'map-water.json').read_text(encoding='utf8'))['12'])
    if len(water_renderers)!=len(water['renderers']):raise ValueError('Original water shader/renderer count differs from the converted water meshes')
    water['native_materials']={'shader':'Universal Render Pipeline/2D/Sprite-Lit-CustomWater2','mask':bake_mask(exporter,12,GROUND)}
    view=model_view(exporter)
    save(ART/'map12-rendering.json',{'manifest':'854_156383','map':12,'water':water,'model_views':{view['model']:view}})
    for suffix in ['geom','json']:
        target=ART/f'regions/12.{suffix}';content=(destination/target.name).read_bytes()
        if target.read_bytes()!=content:target.write_bytes(content)
    outputs=['assets/official/regions/12.geom','assets/official/regions/12.json','assets/official/map12-rendering.json','assets/official/map-water-masks/12.png']
    receipt=REF/'materials-12-source-manifest.json'
    if receipt.exists():previous=json.loads(receipt.read_text(encoding='utf8'))['previous_outputs']
    save(receipt,{'manifest':'854_156383','map':12,'scope':'Original mountain base tile renderer materials, 57 Water2 meshes, original WaterMask draws',
                  'terrain_source':exporter.apk.provenance(exporter.locate(GROUND)[1]),'original_renderers':renderers,
                  'inputs':exporter.source_records,'resources':exporter.records,'schemas':exporter.schemas.records,
                  'lava_shaders':exporter.lava_programs,
                  'previous_outputs':previous,'outputs':{p:digest(ROOT/p) for p in outputs},'counts':result['counts'],'geometry':result['geometry'],
                  'limitations':['Original WaterMask maximum blend rasterized at 64 samples/world unit; this does not claim identical Unity framebuffer pixels.','Lava uses the original unlit GLES branch, original full Sprite texture and noise, world coordinates and material parameters; Unity URP shape-light accumulation is not reproduced.'],
                  'history_preserved':['regions-source-manifest.json','water-source-manifest.json']})
    from prepare_map12_dialogs import build as mountain_dialogs
    mountain_dialogs(ROOT)
    print('Converted original mountain materials and seven battle-stele dialogs',flush=True)


if __name__=='__main__':build()
