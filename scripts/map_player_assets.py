"""Resolve pinned player split Spine animation assets for either sex."""
import hashlib
from official_manifest import read_asset_manifest
from prepare_map_regions import ROOT


def identity(obj):
    return obj.assets_file.name + ':' + str(obj.path_id)


class PlayerAssets:
    def __init__(self, exporter, body='male', cache_directory=None):
        self.e = exporter
        self.definitions = {}
        self.guids = None
        self.body = body
        self.cache_directory = cache_directory

    def split_definition(self, asset):
        key = identity(asset)
        if key in self.definitions:
            return self.definitions[key]
        expected = asset.read(check_read=False).m_Name + '_SplitBinarySO.asset'
        for name, pointer in asset.assets_file.container.items():
            if name.endswith('/' + expected):
                obj = pointer.deref()
                self.e.input(obj)
                self.definitions[key] = self.e.schemas.read(obj)
                return self.definitions[key]
        raise FileNotFoundError('Original player split animation definition: ' + expected)

    def prepare_split_animation(self, key, job):
        definition = self.definitions[key.split(':@', 1)[0]]
        entry = next((row for row in definition['ListAnimationContent']
                      if row['ContentName'] == job['animation']), None)
        if entry is None:
            # Base clips live in the main skeleton; only external clips have rows.
            return
        if self.guids is None:
            self.guids = {row['guid']: row for name in self.e.apk.archive.namelist()
                          if name.startswith('assets/yoo/') and name.endswith('_854_156383.v3.bytes')
                          for row in read_asset_manifest(self.e.apk.archive.read(name)) if row['guid']}
        row = self.guids[entry['TextAsset']['mAssetGuid']]
        obj = self.e.apk.asset(row['bundle'], '/' + row['path'].rsplit('/', 1)[-1])
        self.e.input(obj)
        folder = self.cache_directory or ROOT / ('.tools/source-packages/character-' + self.body)
        target = folder / (hashlib.sha256(identity(obj).encode()).hexdigest()[:24] + '.bytes')
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(obj.read().m_Script.encode('utf8', 'surrogateescape'))
        job['split_animations'] = {job['animation']: str(target)}
        job['string_table'] = definition['StringArray']
