"""Convert official concert glow sticks onto the supported player's weapon bone."""
import hashlib,json,subprocess
from prepare_map_regions import Regions,ART,REF,ROOT,save

def build():
    e=Regions();cache=ROOT/'.tools/source-packages/character-male'
    bundle='defaultpackage_assets_appearanceassets_character_pweapon.bundle'
    for name in ['primaryWeapon.skel','primaryWeapon.atlas']:
        obj=next(o for o in e.apk.objects(bundle) if o.type.name=='TextAsset' and o.read().m_Name==name)
        e.input(obj);(cache/(name+'.bytes')).write_bytes(obj.read().m_Script.encode('utf8','surrogateescape'))
    hud=json.loads((ART/'map-activity-hud.json').read_text(encoding='utf8'))
    job=json.loads((cache/'map-actions-job.json').read_text(encoding='utf8'))
    job['weapon_skeleton']=str(cache/'primaryWeapon.skel.bytes');job['weapon_atlas']=str(cache/'primaryWeapon.atlas.bytes')
    job['weapons']={id:hud['skins'][str(row['SkinDict']['1'])]['SkinName'] for id,row in hud['appearances'].items()}
    prefab_bundle='defaultpackage_assets_prefabs_appearance_playerpart_standalone_pweapon_ui.bundle'
    scales=[]
    for obj in e.apk.objects(prefab_bundle):
        if obj.type.name!='RectTransform':continue
        transform=obj.read()
        if transform.m_GameObject.read().m_Name!='Spine':continue
        e.input(obj);scales.append([transform.m_LocalScale.x,transform.m_LocalScale.y])
    if not scales or any(scale!=scales[0] for scale in scales):raise ValueError('Original weapon prefab scales differ')
    job['weapon_scale']=scales[0]
    job['animations']=['map/idle_01','map/run_01']+[r['animation'] for r in hud['supporter']]
    save(cache/'music-weapons-job.json',job)
    target=ART/'map-music-weapons.json'
    subprocess.run(['node',str(ROOT/'scripts/extract_music_weapons.mjs'),str(cache/'music-weapons-job.json'),str(target)],check=True)
    output=json.loads(target.read_text(encoding='utf8'));names=output['textures'];textures={}
    atlas_bundle='defaultpackage_assets_appearanceassets_character_pweapon_atlas_default.bundle'
    for obj in e.apk.objects(atlas_bundle):
        if obj.type.name!='Texture2D' or obj.read().m_Name not in names:continue
        from UnityPy.classes.PPtr import PPtr
        textures[obj.read().m_Name]=e.texture(PPtr(m_FileID=0,m_PathID=obj.path_id,assetsfile=obj.assets_file))
    if set(names)!=set(textures):raise ValueError('Original glow-stick atlas missing: '+str(set(names)-set(textures)))
    output['textures']=textures;save(target,output)
    save(REF/'music-weapons-source-manifest.json',dict(manifest='854_156383',inputs=e.source_records,resources=e.records,
        outputs={'map-music-weapons.json':hashlib.sha256(target.read_bytes()).hexdigest()},
        operation='Six original primaryWeapon skins and their atlas regions, using the shared standalone UI prefab Spine scale, sampled on the original male pWeapon_R2 bone at 24 fps. Local bone attachment adapter retains the supported player appearance; no bitmap painting.'))
    print('Original concert weapon skins',len(job['weapons']),flush=True)

if __name__=='__main__':build()
