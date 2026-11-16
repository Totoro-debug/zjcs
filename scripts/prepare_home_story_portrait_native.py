"""Recover the selected original dialogue portrait prefabs and Sprite proxies."""
import hashlib
import json
import re
from pathlib import Path

from home_native_assets import write_staged_assets
from official_apk import ROOT
from prepare_current_partner_native import documents, identity
from prepare_partner_card_native import guid, script_id, sha
from prepare_partner_gift_native import PartnerGiftBinding
from validation_workspace import validation_directory


class StoryPortraitBinding(PartnerGiftBinding):
    def register_scripts(self):
        super().register_scripts()
        path = self.work / 'home-story-portrait-code/PortraitFace.cs'
        meta = Path(str(path) + '.meta')
        pointer = dict(fileID=11500000, guid=guid(meta), type=3)
        self.scripts['PortraitFace'] = pointer
        self.class_ids[script_id('', 'PortraitFace')] = pointer
        self.script_guids.add(pointer['guid'])
        self.script_sources['PortraitFace'] = dict(path=path.relative_to(ROOT).as_posix(), pointer=pointer,
            sha256=sha(path.read_bytes()), meta_sha256=sha(meta.read_bytes()))

    def build(self):
        bundle = 'defaultpackage_assets_prefabs_ui_portrait.bundle'
        prefabs = []
        for number in (8402, 8411):
            name = 'Npc_Portrait_' + str(number)
            relative = 'Prefabs/UI/Portrait/' + name + '.prefab'
            native = self.native / relative
            root = self.recovery.apk.asset(bundle, name + '.prefab')
            native_docs = documents(native.read_text('utf8'))
            native_root, = [key for key, value in native_docs.items()
                            if value.get('GameObject', {}).get('m_Name') == name]
            self.recovery.recover(name, self.work / 'home-story-portrait-recovery', bundle=bundle,
                native_relative=relative, source_root=root, native_root_id=native_root)
            self.inputs[native.relative_to(ROOT).as_posix()] = sha(native.read_bytes())
            meta = Path(str(native) + '.meta')
            self.inputs[meta.relative_to(ROOT).as_posix()] = sha(meta.read_bytes())
        self.recovery.finalize()
        roots, proxy_ids = {}, set()
        for _, _, panel, _ in self.recovery.outputs:
            if panel['unresolved']:
                raise ValueError('Resolve original portrait dependencies: ' + repr(panel['unresolved']))
            for key, pointer in panel['external'].items():
                self.objects[pointer['guid']] = self.original(key)
            for key, file_id in panel['objectMap'].items():
                owner = self.original(key)
                self.learn(self.recovery.fields(owner), next(iter(panel['documents'][file_id].values())), owner)
            for script in panel['scripts']:
                full = '.'.join(filter(None, (script['namespace'], script['type'])))
                if full not in self.scripts:
                    raise ValueError('Bind original portrait component: ' + full)
                fields = panel['documents'][script['nativeFileID']]['MonoBehaviour']
                fields['m_Script'] = self.scripts[full]
                if script['type'] == 'PortraitFace':
                    def collect(value):
                        if isinstance(value, dict):
                            if value.get('mAssetGuid'): proxy_ids.add(value['mAssetGuid'])
                            for item in value.values(): collect(item)
                        elif isinstance(value, list):
                            for item in value: collect(item)
                    collect(fields)
            name = next(value['GameObject']['m_Name'] for value in panel['documents'].values()
                        if value.get('GameObject', {}).get('m_Name', '').startswith('Npc_Portrait_'))
            key = hashlib.md5(('zjcs:home-story-portrait:' + name).encode()).hexdigest()
            prefabs.append((Path(name + '.prefab'), key, self.replace(panel['documents'])))
            roots[name] = dict(fileID=panel['root'], guid=key, type=3)

        package = self.recovery.apk.source.manifest_bytes('DefaultPackage', self.recovery.apk.archive)
        paths = {match[2].decode(): match[1].decode() for match in re.finditer(
            rb'(Assets/[^\x00-\x1f]+?)\x20\x00([a-f0-9]{32})', package)}
        pointers = {}
        for key in sorted(proxy_ids):
            path = paths[key]
            asset_key = 'defaultpackage_' + path.replace('/', '_').lower()
            bundles = sorted((name for name in self.recovery.apk.index
                if asset_key.startswith(name.removesuffix('.bundle'))), key=len, reverse=True)
            matches = [(asset, pointer.deref()) for candidate in bundles
                for asset, pointer in self.recovery.apk.container(candidate)
                if asset.lower() == path.lower() and pointer.type.name == 'Sprite']
            if len(matches) != 1:
                raise ValueError('Resolve original portrait Sprite: ' + key + '/' + path)
            native = self.native / Path(path).relative_to('Assets').with_suffix('.asset')
            native_guid = guid(Path(str(native) + '.meta'))
            self.objects[native_guid] = matches[0][1]
            pointers[key] = self.load(native_guid)
            self.inputs[native.relative_to(ROOT).as_posix()] = sha(native.read_bytes())
        self.queue.extend(prefabs)
        for _, _, data in self.queue:
            if isinstance(data, dict):
                result = self.replace(data)
                data.clear()
                data.update(result)
        remap = {key: hashlib.md5(('zjcs:home-story-portrait-asset:' + key).encode()).hexdigest()
            for path, key, _ in self.queue if path.suffix in ('.asset', '.mat', '.controller', '.anim')}
        def remap_guids(value):
            if isinstance(value, dict):
                return {key: remap.get(item, item) if key == 'guid' and isinstance(item, str)
                        else remap_guids(item) for key, item in value.items()}
            if isinstance(value, list): return [remap_guids(item) for item in value]
            return value
        self.queue = [(path, remap.get(key, key), remap_guids(data)) for path, key, data in self.queue]
        pointers = remap_guids(pointers)
        outputs = write_staged_assets(self.queue, self.work / 'home-story-portrait-assets')
        for path in (Path(__file__), ROOT / 'scripts/prepare_home_story_portrait_code.py'):
            self.inputs[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
        receipt = dict(manifest=self.recovery.apk.version, inputs=self.inputs, package_manifest_sha256=sha(package),
            roots=roots, sprite_proxy_bindings=pointers, objects=self.proof, recovery=self.recovery.proof,
            script_bindings=self.script_sources, native_guid_remap=remap, outputs=outputs,
            scope='Original portrait prefabs and Sprite proxy resources; dialogue panel remains separate.')
        (self.work / 'home-story-portrait-native-source-manifest.json').write_text(
            json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
        print('Staged original portrait prefabs:', len(roots), 'Sprite proxies:', len(pointers), flush=True)


if __name__ == '__main__':
    StoryPortraitBinding(validation_directory()).build()
