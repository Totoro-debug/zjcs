#!/usr/bin/env python3
"""Recover the complete selected official NpcDetailPanel and bind local scripts."""
from pathlib import Path
import hashlib
import json
import argparse
import re
import yaml

from official_apk import ROOT
from prepare_partner_card_native import guid, sha, script_id, Dumper
from prepare_partner_input_dialog import metadata
from prepare_partner_gift_native import PartnerGiftBinding
from prepare_partner_panel_native import PartnerPanelBinding
from validation_workspace import validation_directory


class PartnerDetailWindowBinding(PartnerPanelBinding):
    def load(self, source_guid):
        try:
            return super().load(source_guid)
        except ValueError:
            obj = self.objects.get(source_guid)
            if obj is not None and obj.type.name == 'AnimationClip':
                for _, target_guid, docs in self.queue:
                    if target_guid == self.redirects.get(source_guid, {}).get('guid'):
                        (self.work / 'detail-animation-binding-diagnostic.json').write_text(json.dumps(dict(
                            source=self.recovery.fields(obj), converted=docs), ensure_ascii=False, indent=2), 'utf8')
            raise

    def replace(self, value):
        if value == {'class': 'StateMultiLanguageConfig', 'ns': '', 'asm': 'Framework'}:
            return dict(**{'class': 'StateMultiLanguageConfig', 'ns': 'Zjcs.Official.PartnersUI', 'asm': 'Zjcs.Official.PartnerDetailUI'})
        return super().replace(value)

    def register_scripts(self):
        super().register_scripts()
        official = ROOT / 'unity/Assets/Game/Runtime/Official'
        paths = {name: official / 'PartnersUI/Detail' / (name + '.cs') for name in (
            'NpcAdventureConditionContent', 'NpcDetailPanelDetailContent', 'NpcDetailLiveController',
            'NpcDetailLiveItem', 'NpcTreasureIcon', 'NpcDetailPanelPropContent', 'NpcTalkUI')}
        paths['NpcDetailPanel'] = official / 'PartnersUI/Roster/NpcDetailPanel.cs'
        paths['ItemIconNpcGift'] = official / 'PartnersUI/Gifts/ItemIconNpcGift.cs'
        paths.update({name: official / 'PartnersUI/GiftController' / (name + '.cs') for name in (
            'NpcDetailPanelGiftContent', 'NpcFriendshipLevelContent', 'NpcGiftBlueDotItem')})
        paths.update({name: official / 'PartnersUI/Roster/Profile' / (name + '.cs') for name in (
            'NpcDetailPanelProfileContent', 'NpcDetailPanelProfileContentTabItem')})
        paths.update({name: official / 'PartnersUI/Roster/Achievements' / ('Partner' + name + '.cs') for name in (
            'AchievementPanel', 'AchievementPanelBase', 'AchievementPanelItem')})
        paths['ClampingScrollRect'] = official / 'WorldUI/WorldClampingScrollRect.cs'
        paths['TMP_TypeWriterEffect'] = official / 'StoryBubbles/TMP_TypeWriterEffect.cs'
        paths['UIBloomGraphicElement'] = official / 'ProfessionUI/UIBloomGraphicElement.cs'
        paths['AppearanceSystem.SkeletonGraphicAnimSequence'] = official / 'PartnersUI/Detail/SkeletonGraphicAnimSequence.cs'
        for name, path in paths.items():
            metadata = Path(str(path) + '.meta')
            reference = dict(fileID=11500000, guid=guid(metadata), type=3)
            self.scripts[name] = reference
            self.script_sources[name] = dict(path=path.relative_to(ROOT).as_posix(), pointer=reference,
                sha256=sha(path.read_bytes()), meta_sha256=sha(metadata.read_bytes()))
            namespace, _, leaf = name.rpartition('.')
            self.class_ids[script_id(namespace, leaf or name)] = reference
            self.script_guids.add(reference['guid'])

    def friendship_templates(self):
        for name in ('NpcDetailPanel', 'NpcFriendshipPropItemGroup'):
            self.recovery.recover(name, self.work / 'partner-native')
        self.recovery.finalize()
        templates = []
        for native, _, data, record in self.recovery.outputs:
            self.restore(data)
            templates.append((record['name'], data))
            for path in (native, Path(str(native) + '.meta')):
                self.inputs[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
        self.template_selection = dict(
            adaptation='Complete original NpcDetailPanel; all original children, component order and serialized bindings retained.',
            roots=[dict(name=name, native_root=data['root'], objects=len(data['documents'])) for name, data in templates])
        cache = self.recovery.proof['component_cache']
        self.inputs[cache['path']] = cache['sha256']
        return templates


NAME = 'partner-detail-window-native-source-manifest.json'


def install(work):
    receipt = json.loads((work / NAME).read_bytes())
    if receipt['generator_sha256'] != sha(Path(__file__).read_bytes()):
        raise ValueError('Rebuild the detail window after generator changes')
    for name, expected in receipt['inputs'].items():
        if sha((ROOT / name).read_bytes()) != expected:
            raise ValueError('Detail input changed: ' + name)
    for row in receipt['script_bindings'].values():
        path = ROOT / row['path']
        if sha(path.read_bytes()) != row['sha256'] or sha(Path(str(path) + '.meta').read_bytes()) != row['meta_sha256']:
            raise ValueError('Detail script changed: ' + row['path'])
    base = ROOT / 'unity/Assets/Official/Partners'
    existing = {}
    for meta in base.rglob('*.meta'):
        path = Path(str(meta)[:-5])
        if not path.is_file(): continue
        key = guid(meta)
        if key in existing: raise ValueError('Duplicate partner asset GUID: ' + key)
        existing[key] = path
    staged = {}
    for name, expected in receipt['outputs'].items():
        source = work / 'gift-assets' / name
        if sha(source.read_bytes()) != expected: raise ValueError('Staged detail asset changed: ' + name)
        if name.endswith('.meta'): continue
        meta = Path(str(source) + '.meta')
        key = guid(meta)
        if key in staged: raise ValueError('Duplicate staged detail GUID: ' + key)
        staged[key] = (name, source.read_bytes(), meta.read_bytes())

    # Reuse byte-identical assets. Different serialization (including restored
    # legacy root curves) gets its own deterministic identity, never overwrites
    # another module's asset. Propagate that identity through the dependency graph.
    previous_path = ROOT / 'assets/reference' / NAME
    previous = json.loads(previous_path.read_bytes()) if previous_path.exists() else {}
    aliases = dict(previous.get('isolated_serialization_guids', {}))
    for key, value in aliases.items():
        expected = hashlib.md5(('zjcs:partner-detail-window:' + key).encode()).hexdigest()
        if key not in staged or value != expected:
            raise ValueError('Previous isolated detail identity differs')
    def remap(raw):
        if not raw.startswith(b'%YAML'): return raw
        return re.sub(rb'(?<=guid: )[0-9a-f]{32}', lambda match:
            aliases.get(match[0].decode(), match[0].decode()).encode(), raw)
    while True:
        changed = False
        for key, (_, raw, _) in staged.items():
            if key in aliases or key not in existing: continue
            if remap(raw) != existing[key].read_bytes():
                aliases[key] = hashlib.md5(('zjcs:partner-detail-window:' + key).encode()).hexdigest()
                changed = True
        if not changed: break
    destination = base / 'UI/DetailWindow'
    installed, reused, planned = {}, [], []
    for key, (name, raw, meta_raw) in staged.items():
        target_key = aliases.get(key, key)
        target = existing.get(target_key, destination / name)
        raw = remap(raw)
        meta_raw = re.sub(rb'(?m)^guid: [0-9a-f]{32}$', ('guid: ' + target_key).encode(), meta_raw)
        if target.exists():
            if target.read_bytes() != raw: raise ValueError('Preserve different existing detail asset: ' + str(target))
            previous_meta = Path(str(target) + '.meta').read_bytes()
            if yaml.safe_load(previous_meta) != yaml.safe_load(meta_raw):
                raise ValueError('Preserve different metadata: ' + str(target))
            meta_raw = previous_meta
            reused.append(target.relative_to(ROOT).as_posix())
        else: planned.extend(((target, raw), (Path(str(target) + '.meta'), meta_raw)))
        for path, content in ((target, raw), (Path(str(target) + '.meta'), meta_raw)):
            installed[path.relative_to(ROOT).as_posix()] = sha(content)
    for target, raw in planned:
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(raw)
    for folder in [destination, *[path for path in destination.rglob('*') if path.is_dir()]]:
        meta = metadata(folder, 'DefaultImporter', True)
        installed[meta.relative_to(ROOT).as_posix()] = sha(meta.read_bytes())
    script = ROOT / 'unity/Assets/Game/Runtime/Offline/PartnersUI/OfficialPartnerDetailWindowAssetCatalog.cs'
    script_meta = metadata(script)
    catalog = ROOT / 'unity/Assets/Game/Resources/Partners/OfficialPartnerDetailWindowAssetCatalog.asset'
    pointer = dict(receipt['roots']['NpcDetailPanel'])
    pointer['guid'] = aliases.get(pointer['guid'], pointer['guid'])
    body = dict(m_ObjectHideFlags=0, m_CorrespondingSourceObject=dict(fileID=0), m_PrefabInstance=dict(fileID=0),
        m_PrefabAsset=dict(fileID=0), m_GameObject=dict(fileID=0), m_Enabled=1, m_EditorHideFlags=0,
        m_Script=dict(fileID=11500000, guid=guid(script_meta), type=3), m_Name='OfficialPartnerDetailWindowAssetCatalog',
        m_EditorClassIdentifier='', panelPrefab=pointer, texts=receipt['detail_texts'])
    catalog.write_text('%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\n' +
        yaml.dump(dict(MonoBehaviour=body), Dumper=Dumper, sort_keys=False, allow_unicode=True, width=1000000), 'utf8', newline='\n')
    for path in (catalog, metadata(catalog, 'NativeFormatImporter'), script, script_meta):
        installed[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
    receipt.update(installed_output_sha256=installed, reused_assets=reused,
        isolated_serialization_guids=aliases, installed_root=pointer)
    (ROOT / 'assets/reference' / NAME).write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    print('Installed detail window:', len(installed), 'paths; reused', len(reused), 'assets', flush=True)


def main():
    work = validation_directory()
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--install', action='store_true')
    if parser.parse_args().install:
        install(work)
        return
    binding = PartnerDetailWindowBinding(work)
    try:
        PartnerGiftBinding.build(binding)
        receipt = json.loads((work / 'partner-gift-native-source-manifest.json').read_bytes())
        from partner_panel_language import panel_language
        texts, inputs = panel_language(work / 'gift-assets/NpcDetailPanel.prefab')
        inputs.pop((work / 'gift-assets/NpcDetailPanel.prefab').relative_to(ROOT).as_posix())
        receipt['inputs'].update(inputs)
        receipt['detail_texts'] = texts
        receipt['animation_root_path_repairs'] = binding.animation_root_paths
        for name in ('prepare_partner_roster_native.py', 'prepare_partner_star_god_native.py', 'prepare_partner_panel_native.py', 'partner_panel_language.py'):
            path = ROOT / 'scripts' / name
            receipt['inputs'][path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
        receipt['generator_sha256'] = sha(Path(__file__).read_bytes())
        receipt['scope'] = 'Complete original detail prefab staged. Native animated actors, production host and interaction validation pending.'
        (work / NAME).write_text(
            json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    finally:
        binding.recovery.apk.archive.close()


if __name__ == '__main__':
    main()
