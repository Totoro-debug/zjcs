"""Bind original pet portraits by evolution phase and types by PetSettings."""
import hashlib
import json
import struct
import argparse
import urllib.request
from pathlib import Path

from prepare_official_city import ART, REFERENCE, ROOT, Exporter
from official_resource_manifest import write_resources


def recheck_portrait():
    """Inspect original and current official icon inputs, keeping versions apart."""
    from official_manifest import read_manifest, read_asset_manifest
    from fetch_official_cloud_assets import CONFIG_URL
    import UnityPy
    e=Exporter();target='pet_1619'
    original=[]
    for name in e.apk.archive.namelist():
        if 'PackageManifest_' not in name or not name.endswith('.v3.bytes'):continue
        raw=e.apk.archive.read(name);rows=read_asset_manifest(raw)
        original.append(dict(manifest=name,sha256=hashlib.sha256(raw).hexdigest(),
                             matches=[r for r in rows if target in r['path'].lower()]))
    with urllib.request.urlopen(CONFIG_URL,timeout=30) as response:config=json.load(response)['default']
    root=config['CdnUrl'].rstrip('/')+'/'+str(config['ResVersion'])+'/Android/DefaultPackage/'
    with urllib.request.urlopen(root+'PackageManifest_DefaultPackage.version',timeout=30) as response:version=response.read().decode().strip()
    url=root+'PackageManifest_DefaultPackage_'+version+'.v3.bytes'
    with urllib.request.urlopen(url,timeout=30) as response:raw=response.read()
    table=read_manifest(raw);rows=read_asset_manifest(raw)
    if table['version']!=version:raise ValueError('Advertised pet manifest differs')
    cache=ROOT/'.tools/source-packages/cloud-research'/('pet-'+version);cache.mkdir(exist_ok=True)
    (cache/'manifest.v3.bytes').write_bytes(raw)
    bundles=[]
    for bundle in table['bundles']:
        if not any(k in bundle['name'] for k in ['image_itemicon_pet.','image_ui_pet.']):continue
        file=cache/bundle['file'];bundle_url=root+bundle['file']
        if not file.exists():
            with urllib.request.urlopen(bundle_url,timeout=30) as response:file.write_bytes(response.read())
        data=file.read_bytes()
        if len(data)!=bundle['size'] or hashlib.md5(data).hexdigest()!=bundle['hash']:
            raise ValueError('Official pet icon bundle checksum differs: '+bundle['name'])
        env=UnityPy.load(data)
        sprites=[dict(path_id=str(o.path_id),name=o.read().m_Name) for o in env.objects if o.type.name in ['Sprite','Texture2D']]
        bundles.append(dict(name=bundle['name'],url=bundle_url,md5=bundle['hash'],bytes=len(data),
                            sha256=hashlib.sha256(data).hexdigest(),objects=sprites,
                            matches=[o for o in sprites if target in o['name'].lower()]))
    reference=ROOT/'assets/reference/pet-ui/source-manifest.json'
    source=json.loads(reference.read_text('utf8'))
    source['portrait_sources']=dict(target='ItemIcon/Pet/'+target,original_manifests=original,
        latest_manifest=dict(version=version,url=url,sha256=hashlib.sha256(raw).hexdigest(),
                             matches=[r for r in rows if target in r['path'].lower()]),bundles=bundles)
    reference.write_text(json.dumps(source,ensure_ascii=False,indent=2),encoding='utf8')
    print('Pet portrait source manifests:',len(original),'original +',version,';',len(bundles),'current icon bundles;',
          sum(len(b['matches']) for b in bundles),'matching objects')


def build():
    exporter = Exporter()
    rules = json.loads((ART / 'rules.json').read_text(encoding='utf8'))
    portraits = {path.rsplit('/', 1)[-1].rsplit('.', 1)[0]: ptr
                 for path, ptr in exporter.apk.container('image_itemicon_pet')
                 if ptr.type.name == 'Sprite'}
    additions, phases, missing = {}, [], []
    for cfg in rules['pet_evolution']:
        key = f"pet_portrait:{cfg['ClassId']}:{cfg['EvolutionPhase']}"
        icon = cfg['PetIcon']
        ptr = portraits.get(icon.rsplit('/', 1)[-1])
        if ptr is None:
            missing.append({'key': key, 'path': icon, 'status': 'Not present in the original pet icon bundle'})
            continue
        additions[key] = exporter.sprite(ptr)
        phases.append({'key': key, 'path': icon, 'sprite': additions[key]})

    # PetSettings starts with the quality/color list, then type/path pairs.
    config = exporter.apk.asset('scriptableconfig_modules_pet', '/PetSettings.asset')
    raw = config.get_raw_data()
    offset = (32 + struct.unpack_from('<i', raw, 28)[0] + 3) & ~3
    quality_count = struct.unpack_from('<i', raw, offset)[0]
    if quality_count != 3:
        raise ValueError('Unexpected PetSettings quality list')
    offset += 4 + quality_count * 20
    count = struct.unpack_from('<i', raw, offset)[0]
    offset += 4
    types = {}
    type_sprites = {path.rsplit('/', 1)[-1].rsplit('.', 1)[0]: ptr
                    for path, ptr in exporter.apk.container('image_ui_pet')
                    if ptr.type.name == 'Sprite'}
    for _ in range(count):
        ident, length = struct.unpack_from('<ii', raw, offset)
        path = raw[offset + 8:offset + 8 + length].decode()
        offset = (offset + 8 + length + 3) & ~3
        additions[f'pet_type:{ident}'] = exporter.sprite(type_sprites[path.rsplit('/', 1)[-1].rsplit('.', 1)[0]])
        types[str(ident)] = {'path': path, 'sprite': additions[f'pet_type:{ident}']}

    # Other extraction tasks share these catalogs; merge into their latest state.
    icons = json.loads((ART / 'icons.json').read_text(encoding='utf8'))
    icons.update(additions)
    exporter.save('icons.json', icons)
    manifest = json.loads((REFERENCE / 'source-manifest.json').read_text(encoding='utf8'))
    manifest['resources'].update(exporter.records)
    manifest['data_sha256']['icons.json'] = hashlib.sha256((ART / 'icons.json').read_bytes()).hexdigest()
    manifest.setdefault('unsupported', {})['pet_portraits'] = missing
    (REFERENCE / 'source-manifest.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding='utf8')
    reference = ROOT / 'assets/reference/pet-ui'
    reference.mkdir(parents=True, exist_ok=True)
    evidence = {'version': '854_156383', 'resources': exporter.records,
                'pet_settings': exporter.apk.provenance(config), 'types': types,
                'portraits': phases, 'missing': missing,
                'logic': {'portrait': 'PetInfoContent.Init: replace mPetIcon using the current evolution phase',
                          'path': 'PetSettings.GetPetIconPath: classId + 100 * (phase - 1), matching PetEvolutionInfo.PetIcon',
                          'type': 'PetInfoContent.Init: mTypeIcon uses PetSettings.GetPetTypeIconPath(PetInfo.Type)',
                          'quality': 'PetInfoContent.Init: mQualityStateSwitcher uses PetInfo.ItemInfo.Quality'}}
    (reference / 'source-manifest.json').write_text(json.dumps(evidence, ensure_ascii=False, indent=2), encoding='utf8')
    write_resources(manifest)
    print(f'Pet UI: {len(phases)} phase portraits, {count} original type icons; {len(missing)} source icon unavailable')


if __name__ == '__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--recheck-portrait',action='store_true');args=parser.parse_args()
    if args.recheck_portrait:recheck_portrait()
    else:build()
