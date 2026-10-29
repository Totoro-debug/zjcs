"""Original art for UI whose complete prefab is absent in the pinned base APK."""
import hashlib
import json
from prepare_official_city import Exporter, ART, REFERENCE, ROOT

def build():
    exporter=Exporter()
    icons=json.loads((ART/'icons.json').read_text(encoding='utf8'))
    manifest=json.loads((REFERENCE/'source-manifest.json').read_text(encoding='utf8'))
    for bundle,namespace in [('image_ui_chestgacha_chestelementgacha_atlas','chest_ui'),('image_ui_treasure','treasure_ui')]:
        for path,ptr in exporter.apk.container(bundle):
            if ptr.type.name!='Sprite':continue
            sprite=exporter.sprite(ptr)
            icons[namespace+':'+sprite['name']]=sprite
            print(namespace,sprite['name'],sprite['width'],sprite['height'])
    qualities=['White','Blue','Purple','Orange','Gold','Red','Multicolour']
    for path,ptr in exporter.apk.container('image_ui_common'):
        if ptr.type.name!='Sprite':continue
        name=path.rsplit('/',1)[-1].removesuffix('.png')
        if name not in ['ImgTreasureQuality_'+q for q in qualities]:continue
        sprite=exporter.sprite(ptr)
        icons['treasure_quality:'+str(qualities.index(sprite['name'].removeprefix('ImgTreasureQuality_'))+1)]=sprite
    exporter.save('icons.json',icons)
    manifest['resources'].update(exporter.records)
    manifest['data_sha256']['icons.json']=hashlib.sha256((ART/'icons.json').read_bytes()).hexdigest()
    manifest['unsupported']['chest_gacha_interface']={
        'prefab':'defaultpackage_assets_prefabs_ui_chestgacha.bundle',
        'status':'Not packaged; native controls use available original chest atlas, not an extracted complete layout',
        'backgrounds':'defaultpackage_assets_image_ui_chestgacha_chestgachabg_*.bundle: not packaged'}
    pending={str(item):{'icon':f'ItemIcon/Materials/item_{item}',
                       'bundle':f'defaultpackage_assets_image_itemicon_materials_map{area}_noatlas.bundle'}
             for item,area in zip(range(8051,8058),[12,13,14,17,19,21,23]) if str(item) not in icons}
    if pending:
        manifest['unsupported']['shop_ingredient_icons']={
            'status':'Not yet converted; check the pinned APK and verified CDN cache', 'items':pending}
    else:
        manifest['unsupported']['shop_ingredient_icons']={
            'status':'All seven extracted from verified 854_156383 CDN cache; see shop-ui/source-manifest.json'}
    (REFERENCE/'source-manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf8')
    __import__("official_resource_manifest").write_resources(manifest)

if __name__=='__main__':build()
