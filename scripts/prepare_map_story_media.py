"""Resolve native story asset GUIDs and decode original full-screen artwork."""
import hashlib
import json
import zipfile
from collections import Counter
from official_manifest import read_asset_manifest
from fetch_official_assets import APK
from prepare_map_regions import Regions, ART, REF, save
from UnityPy.classes.PPtr import PPtr


def guids(value):
    if isinstance(value, dict):
        if value.get('mAssetGuid'): yield value['mAssetGuid']
        for child in value.values(): yield from guids(child)
    elif isinstance(value, list):
        for child in value: yield from guids(child)


def build():
    archive = zipfile.ZipFile(APK)
    manifests = {name: archive.read(name) for name in archive.namelist() if name.startswith('assets/yoo/') and name.endswith('_854_156383.v3.bytes')}
    assets = [row for raw in manifests.values() for row in read_asset_manifest(raw)]
    by_guid = {r['guid']: r for r in assets if r['guid']}
    stories = json.loads((ART / 'adventure.json').read_text(encoding='utf8'))['stories']
    referenced = set(guids(stories))
    resolved = {g: by_guid[g] for g in sorted(referenced) if g in by_guid}
    e = Regions(); images = {}; missing = {}
    image_guids = {n['ImageAsset']['mAssetGuid'] for s in stories.values() for n in s['nodes'].values() if n['_type'] == 'OpenScreenImgNode'}
    for guid in sorted(image_guids - {''}):
        row = by_guid.get(guid)
        if row is None:
            missing[guid] = f'GUID absent from all {len(assets)} original AssetTable rows across five packages'
            continue
        try:
            obj = e.apk.asset(row['bundle'], '/' + row['path'].rsplit('/', 1)[-1])
            e.input(obj)
            ptr = PPtr(m_FileID=0, m_PathID=obj.path_id, assetsfile=obj.assets_file)
            images[guid] = e.sprite(ptr) if obj.type.name == 'Sprite' else {'file': e.texture(ptr)}
        except Exception as ex:
            missing[guid] = type(ex).__name__ + ': ' + str(ex)
    save(ART / 'map-story-media.json', dict(images=images, assets=resolved))
    save(REF / 'story-media-source-manifest.json', dict(manifest='854_156383', manifests={name: hashlib.sha256(raw).hexdigest() for name, raw in manifests.items()}, asset_rows=len(assets), referenced_guids=len(referenced), unresolved_guids=sorted(referenced - by_guid.keys()), images=len(images), missing=missing, inputs=e.source_records, resources=e.records, outputs={'map-story-media.json': hashlib.sha256((ART / 'map-story-media.json').read_bytes()).hexdigest()}))
    print('Story assets resolved', len(resolved), 'images', len(images), 'missing', missing, flush=True)


if __name__ == '__main__': build()
