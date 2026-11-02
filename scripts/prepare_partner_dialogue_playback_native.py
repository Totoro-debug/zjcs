"""Restore the exact selected NpcDialoguePanel and its external templates."""
import argparse
import json
import re
import yaml
from pathlib import Path

from official_apk import ROOT
from prepare_partner_panel_native import PartnerPanelBinding
from prepare_partner_gift_native import PartnerGiftBinding
from prepare_partner_card_native import guid, sha, script_id
from prepare_partner_input_dialog import metadata
from prepare_partner_dialogue_playback_ui import NAMES, SUPPORT
from validation_workspace import validation_directory

NAME = 'partner-dialogue-playback-native-source-manifest.json'


class PartnerDialoguePlaybackBinding(PartnerPanelBinding):
    def register_scripts(self):
        super().register_scripts()
        official = ROOT / 'unity/Assets/Game/Runtime/Official'
        paths = {name: official / 'PartnersUI/DialoguePlayback' / (name + '.cs') for name in NAMES + SUPPORT}
        paths['TMP_TypeWriterEffect'] = official / 'StoryBubbles/TMP_TypeWriterEffect.cs'
        for name, path in paths.items():
            meta = Path(str(path) + '.meta')
            pointer = dict(fileID=11500000, guid=guid(meta), type=3)
            self.scripts[name] = pointer
            self.script_sources[name] = dict(path=path.relative_to(ROOT).as_posix(), pointer=pointer,
                sha256=sha(path.read_bytes()), meta_sha256=sha(meta.read_bytes()))
            self.class_ids[script_id('', name)] = pointer
            self.script_guids.add(pointer['guid'])

    def friendship_templates(self):
        self.recovery.recover('NpcDialoguePanel', self.work / 'partner-native')
        self.recovery.finalize()
        native, _, panel, record = self.recovery.outputs[-1]
        self.restore(panel)
        self.template_selection = dict(native_root=panel['root'], mapped_objects=len(panel['documents']),
            original_source=record['originalSource'], adaptation='Complete original NpcDialoguePanel; preserve hierarchy, geometry and component references.')
        for path in (native, Path(str(native) + '.meta')):
            self.inputs[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
        cache = self.recovery.proof['component_cache']
        self.inputs[cache['path']] = cache['sha256']
        return [('NpcDialoguePanel', panel)]

    def build(self):
        PartnerGiftBinding.build(self)
        receipt = json.loads((self.work / 'partner-gift-native-source-manifest.json').read_bytes())
        receipt['generator_sha256'] = sha(Path(__file__).read_bytes())
        for name in ('prepare_partner_panel_native.py', 'prepare_partner_gift_native.py',
            'prepare_partner_roster_native.py', 'prepare_partner_input_dialog.py',
            'prepare_partner_dialogue_entry_native.py', 'prepare_partner_star_god_native.py',
            'partner_roster_animator_binding.py', 'partner_panel_animator_binding.py'):
            path = ROOT / 'scripts' / name
            receipt['inputs'][path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
        from partner_dialogue_playback_language import playback_language
        receipt['texts'], inputs = playback_language(self.work / 'gift-assets/NpcDialoguePanel.prefab')
        receipt['inputs'].update(inputs)
        helper = ROOT / 'scripts/partner_dialogue_playback_language.py'
        receipt['inputs'][helper.relative_to(ROOT).as_posix()] = sha(helper.read_bytes())
        receipt['scope'] = 'Original conversation playback prefab and Windows dependencies; application services and input/render validation recorded separately.'
        (self.work / NAME).write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
        print('Prepared complete original conversation panel', len(receipt['outputs']), 'files', flush=True)


def install(work):
    receipt = json.loads((work / NAME).read_bytes())
    if receipt['generator_sha256'] != sha(Path(__file__).read_bytes()): raise ValueError('Rebuild original playback assets')
    for path, expected in receipt['inputs'].items():
        if sha((ROOT / path).read_bytes()) != expected: raise ValueError('Changed original playback input: ' + path)
    for row in receipt['script_bindings'].values():
        path = ROOT / row['path']
        if sha(path.read_bytes()) != row['sha256'] or sha(Path(str(path) + '.meta').read_bytes()) != row['meta_sha256']:
            raise ValueError('Changed playback component: ' + row['path'])
    base = ROOT / 'unity/Assets/Official/Partners'
    existing = {}
    for meta in base.rglob('*.meta'):
        path = Path(str(meta)[:-5])
        if not path.is_file(): continue
        key = guid(meta)
        if key in existing: raise ValueError('Duplicate installed partner GUID: ' + key)
        existing[key] = path
    # Reuse the full panel's already verified canonical animation identities.
    prior_path = ROOT / 'assets/reference/partner-panel-native-source-manifest.json'
    prior = json.loads(prior_path.read_bytes())
    aliases, canonical_meta, proof = {}, {}, {}
    for key, row in receipt['objects'].items():
        if row.get('type') != 'AnimationClip' or key not in prior['objects']: continue
        previous = prior['objects'][key]
        if row['source'] != previous['source'] or row['original_sha256'] != previous['original_sha256']:
            raise ValueError('Shared animation source changed')
        chosen = prior.get('canonical_animation_references', {}).get(key, {}).get('installed_guid', previous['guid'])
        if chosen == row['guid']: continue
        target = existing[chosen]
        raw = (work / 'gift-assets' / row['output']).read_bytes()
        if raw != target.read_bytes() or sha(raw) != prior['installed_output_sha256'][target.relative_to(ROOT).as_posix()]:
            raise ValueError('Shared animation payload changed')
        meta = Path(str(target) + '.meta')
        if sha(meta.read_bytes()) != prior['installed_output_sha256'][meta.relative_to(ROOT).as_posix()]:
            raise ValueError('Shared animation metadata changed')
        aliases[row['guid']] = chosen; canonical_meta[row['guid']] = meta.read_bytes()
        proof[key] = dict(staged_guid=row['guid'], installed_guid=chosen, source_receipt=prior_path.relative_to(ROOT).as_posix(),
            source_receipt_sha256=sha(prior_path.read_bytes()), installed_path=target.relative_to(ROOT).as_posix())
    def remap(raw):
        if not raw.startswith(b'%YAML'): return raw
        return re.sub(rb'(?<=guid: )[0-9a-f]{32}', lambda m: aliases.get(m[0].decode(), m[0].decode()).encode(), raw)
    destination = base / 'UI/DialoguePlayback'
    planned, installed, reused = [], {}, []
    for name, expected in receipt['outputs'].items():
        source = work / 'gift-assets' / name
        if sha(source.read_bytes()) != expected: raise ValueError('Staged playback dependency changed: ' + name)
        if name.endswith('.meta'): continue
        source_key = guid(Path(str(source) + '.meta'))
        key = aliases.get(source_key, source_key)
        target = existing.get(key, destination / name)
        raw = remap(source.read_bytes())
        meta_raw = canonical_meta.get(source_key, Path(str(source) + '.meta').read_bytes())
        if target.exists():
            if raw != target.read_bytes(): raise ValueError('Preserve differing playback asset: ' + str(target))
            old_meta = Path(str(target) + '.meta').read_bytes()
            if yaml.safe_load(meta_raw) != yaml.safe_load(old_meta): raise ValueError('Preserve differing playback metadata')
            meta_raw = old_meta; reused.append(target.relative_to(ROOT).as_posix())
        else: planned.extend(((raw, target), (meta_raw, Path(str(target) + '.meta'))))
        for content, path in ((raw, target), (meta_raw, Path(str(target) + '.meta'))):
            installed[path.relative_to(ROOT).as_posix()] = sha(content)
    for raw, path in planned:
        path.parent.mkdir(parents=True, exist_ok=True); path.write_bytes(raw)
    for folder in [destination, *[path for path in destination.rglob('*') if path.is_dir()]]:
        path = metadata(folder, 'DefaultImporter', True); installed[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
    script = ROOT / 'unity/Assets/Game/Runtime/Offline/PartnersUI/OfficialPartnerDialoguePlaybackAssetCatalog.cs'
    pointer = receipt['roots']['NpcDialoguePanel']
    from prepare_partner_card_native import Dumper
    body = dict(m_ObjectHideFlags=0, m_CorrespondingSourceObject=dict(fileID=0), m_PrefabInstance=dict(fileID=0),
        m_PrefabAsset=dict(fileID=0), m_GameObject=dict(fileID=0), m_Enabled=1, m_EditorHideFlags=0,
        m_Script=dict(fileID=11500000, guid=guid(metadata(script)), type=3), m_Name='OfficialPartnerDialoguePlaybackAssetCatalog',
        m_EditorClassIdentifier='', panelPrefab=pointer, texts=receipt['texts'],
        sounds=[dict(eventId=row['event']['id'], clip=row['pointer']) for row in receipt.get('audio', {}).values()])
    catalog = ROOT / 'unity/Assets/Game/Resources/Partners/OfficialPartnerDialoguePlaybackAssetCatalog.asset'
    catalog.write_text('%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\n' +
        yaml.dump(dict(MonoBehaviour=body), Dumper=Dumper, sort_keys=False, allow_unicode=True, width=1000000), 'utf8', newline='\n')
    for path in (catalog, metadata(catalog, 'NativeFormatImporter'), script, metadata(script)):
        installed[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
    receipt.update(installed_output_sha256=installed, reused_assets=reused, canonical_animation_references=proof)
    (ROOT / 'assets/reference' / NAME).write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    print('Installed original playback:', len(installed), 'paths; reused', len(reused), 'assets', flush=True)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--install', action='store_true')
    work = validation_directory()
    if parser.parse_args().install: install(work)
    else: PartnerDialoguePlaybackBinding(work).build()
