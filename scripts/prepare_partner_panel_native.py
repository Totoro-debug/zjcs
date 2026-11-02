"""Restore the complete selected NpcPanel with its original dependency graph."""
import argparse
import hashlib
import json
import re
from pathlib import Path
import yaml

from official_apk import ROOT
from prepare_partner_card_native import guid, sha, script_id, Dumper
from prepare_partner_star_god_native import PartnerStarGodBinding
from prepare_current_partner_native import documents, identity
from prepare_partner_input_dialog import metadata
from validation_workspace import validation_directory

NAME = 'partner-panel-native-source-manifest.json'


class PartnerPanelBinding(PartnerStarGodBinding):
    def load(self, source_guid):
        pointer = super().load(source_guid)
        obj = self.objects.get(source_guid)
        if obj is not None and obj.type.name == 'AnimatorOverrideController':
            pointer = dict(pointer, type=2)  # Native Unity asset, not an imported file.
            self.redirects[source_guid] = pointer
        return pointer

    def controller(self, source_guid, obj):
        native = self.native_path(source_guid, obj)
        raw = native.read_bytes()
        receipt_key = native.relative_to(self.cache / 'native-export-all').as_posix()
        meta = Path(str(native) + '.meta')
        if sha(raw) != self.checked[receipt_key] or sha(meta.read_bytes()) != self.checked[receipt_key + '.meta']:
            raise ValueError('Native panel animator differs from selected cache')
        for path in (native, meta): self.inputs[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
        docs = documents(raw.decode('utf8'))
        root, = [key for key, value in docs.items() if 'AnimatorController' in value]
        from partner_panel_animator_binding import verify_controller
        visited = verify_controller(self, obj, docs, root)
        target = Path('NativeControllers') / (sha(identity(obj).encode())[:16] + '.controller')
        target_guid = hashlib.md5(('zjcs:partner-original:' + identity(obj)).encode()).hexdigest()
        reference = dict(fileID=root, guid=target_guid, type=2)
        self.redirects[source_guid] = reference
        self.queue.append((target, target_guid, docs))
        self.proof[identity(obj)] = dict(source=self.recovery.apk.provenance(obj), original_sha256=sha(obj.get_raw_data()),
            output=target.as_posix(), guid=target_guid, verified_runtime_subassets=visited)
        return reference

    def replace(self, value):
        if value == dict(**{'class': 'StateAnimationConfig', 'ns': '', 'asm': 'Framework'}):
            return dict(**{'class': 'StateAnimationConfig', 'ns': 'Zjcs.Official.PartnersUI', 'asm': 'Zjcs.Official.PartnersUI'})
        if value == dict(**{'class': 'StateAnimationConfig', 'ns': 'Zjcs.Official.PartnersUI', 'asm': 'Zjcs.Official.PartnersUI'}):
            return value
        return super().replace(value)

    def register_scripts(self):
        super().register_scripts()
        official = ROOT / 'unity/Assets/Game/Runtime/Official'
        paths = {name: official / 'ProfessionUI' / (name + '.cs') for name in
            ('AppearanceUIPartHandler', 'CloseWindowBtn', 'OffScreenUISpinePlayer', 'PanelTitle',
             'PlayerIconMe', 'PlayerInfoUI', 'TopBarHandler')}
        paths.update({name: official / 'ProfessionUI' / (name.rsplit('.', 1)[-1] + '.cs') for name in
            ('AppearanceSystem.PlayerAnimationAppearance', 'AppearanceSystem.Runtime.AppearanceCharacter',
             'AppearanceSystem.Runtime.AppearanceSkinClip', 'AppearanceSystem.Runtime.AppearanceUI')})
        paths.update({name: official / 'TeamUI' / ('Team' + name + '.cs') for name in
            ('HeadCharacterPortrait', 'PlayerIcon', 'PlayerIconMeHpSliderNormal', 'PlayerIconOther')})
        paths.update({name: official / 'WorldUI' / file for name, file in
            [('PreviewTool', 'WorldPreviewTool.cs'), ('SafeArea', 'WorldSafeArea.cs'),
             ('SlicedFilledImage', 'SlicedFilledImage.cs'), ('UGUIJszx.HorizontalLayoutGroupJSZX', 'HorizontalLayoutGroupJSZX.cs'),
             ('UnityEngine.UI.Ext.TabAnimatorTransition', 'WorldTabAnimatorTransition.cs')]})
        paths['NpcPanel'] = official / 'PartnersUI/Roster/NpcPanel.cs'
        paths['Spine.Unity.SkeletonGraphic'] = ROOT / 'unity/Assets/Game/Runtime/Offline/ProfessionUI/ProfessionNativeUiGraphic.cs'
        for name, path in paths.items():
            meta = metadata(path)
            pointer = dict(fileID=11500000, guid=guid(meta), type=3)
            self.scripts[name] = pointer
            self.script_sources[name] = dict(path=path.relative_to(ROOT).as_posix(), pointer=pointer,
                sha256=sha(path.read_bytes()), meta_sha256=sha(meta.read_bytes()))
            namespace, _, leaf = name.rpartition('.')
            self.class_ids[script_id(namespace, leaf or name)] = pointer
            self.script_guids.add(pointer['guid'])

    def friendship_templates(self):
        self.recovery.recover('NpcPanel', self.work / 'partner-native')
        self.recovery.finalize()
        native, _, panel, record = self.recovery.outputs[-1]
        self.restore(panel)
        self.template_selection = dict(native_root=panel['root'], mapped_objects=len(panel['documents']),
            original_source=record['originalSource'], adaptation='Complete original NpcPanel; preserve hierarchy and component references.')
        for path in (native, Path(str(native) + '.meta')):
            self.inputs[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
        cache = self.recovery.proof['component_cache']
        self.inputs[cache['path']] = cache['sha256']
        self.star_god_images = self.static_images()
        return [('NpcPanel', panel)]

    def build(self):
        from prepare_partner_panel_support import prepare
        support_inputs, support_outputs = prepare()
        super().build()
        receipt = json.loads((self.work / 'partner-star-god-native-source-manifest.json').read_bytes())
        path = ROOT / 'scripts/prepare_partner_star_god_native.py'
        receipt['inputs'][path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
        receipt['generator_sha256'] = sha(Path(__file__).read_bytes())
        for path in support_inputs + [ROOT / 'scripts/prepare_partner_panel_support.py', ROOT / 'scripts/partner_panel_animator_binding.py']:
            receipt['inputs'][path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
        receipt['support_output_sha256'] = {path.relative_to(ROOT).as_posix(): sha(path.read_bytes()) for path in support_outputs}
        from partner_panel_language import panel_language
        texts, language_inputs = panel_language(self.work / 'gift-assets/NpcPanel.prefab')
        receipt['panel_texts'] = texts
        receipt['inputs'].update(language_inputs)
        # The staged prefab is already checked in outputs; it is not a durable
        # raw input after the owned validation workspace is removed.
        receipt['inputs'].pop((self.work / 'gift-assets/NpcPanel.prefab').relative_to(ROOT).as_posix())
        helper = ROOT / 'scripts/partner_panel_language.py'
        receipt['inputs'][helper.relative_to(ROOT).as_posix()] = sha(helper.read_bytes())
        receipt['scope'] = 'Complete original NpcPanel and external templates. Application services and actual interaction validation are recorded separately.'
        (self.work / NAME).write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')


def install(work):
    receipt = json.loads((work / NAME).read_bytes())
    if receipt['generator_sha256'] != sha(Path(__file__).read_bytes()): raise ValueError('Rebuild original panel')
    for path, expected in receipt['support_output_sha256'].items():
        if sha((ROOT / path).read_bytes()) != expected: raise ValueError('Panel support changed: ' + path)
    for path, expected in receipt['inputs'].items():
        if sha((ROOT / path).read_bytes()) != expected: raise ValueError('Input changed: ' + path)
    for row in receipt['script_bindings'].values():
        path = ROOT / row['path']
        if sha(path.read_bytes()) != row['sha256'] or sha(Path(str(path) + '.meta').read_bytes()) != row['meta_sha256']:
            raise ValueError('Script binding changed: ' + row['path'])
    base = ROOT / 'unity/Assets/Official/Partners'
    existing = {}
    for meta in base.rglob('*.meta'):
        path = Path(str(meta)[:-5])
        if path.is_file():
            key = guid(meta)
            if key in existing: raise ValueError('Duplicate partner GUID: ' + key)
            existing[key] = path
    # Earlier roster installation assigned different GUIDs to three identical
    # source clips. Reuse those exact source identities and bytes, and explicitly
    # record the reference remap instead of overwriting a shared controller.
    aliases, alias_proof, canonical_meta = {}, {}, {}
    prior_path = ROOT / 'assets/reference/partner-roster-native-source-manifest.json'
    prior = json.loads(prior_path.read_bytes())
    for key, row in receipt['objects'].items():
        old = prior['objects'].get(key)
        if row.get('type') != 'AnimationClip' or not old or row['guid'] == old['guid']: continue
        if row['source'] != old['source'] or row['original_sha256'] != old['original_sha256']:
            raise ValueError('Canonical animation source differs: ' + key)
        target = existing[old['guid']]
        source = work / 'gift-assets' / row['output']
        expected = prior['installed_outputs'][target.relative_to(ROOT).as_posix()]
        if sha(target.read_bytes()) != expected or source.read_bytes() != target.read_bytes():
            raise ValueError('Canonical animation bytes differ: ' + key)
        meta_path = Path(str(target) + '.meta')
        if sha(meta_path.read_bytes()) != prior['installed_outputs'][meta_path.relative_to(ROOT).as_posix()]:
            raise ValueError('Canonical animation metadata differs: ' + key)
        aliases[row['guid']] = old['guid']
        canonical_meta[row['guid']] = meta_path.read_bytes()
        alias_proof[key] = dict(staged_guid=row['guid'], installed_guid=old['guid'],
            prior_receipt=prior_path.relative_to(ROOT).as_posix(), prior_receipt_sha256=sha(prior_path.read_bytes()),
            installed_clip=target.relative_to(ROOT).as_posix(), clip_sha256=expected)
    def remap(raw):
        if not raw.startswith(b'%YAML'): return raw
        return re.sub(rb'(?<=guid: )[0-9a-f]{32}', lambda m: aliases.get(m[0].decode(), m[0].decode()).encode(), raw)
    destination = base / 'UI/Panel'
    previous_path = ROOT / 'assets/reference' / NAME
    previous = json.loads(previous_path.read_bytes()) if previous_path.exists() else None
    override_guids = {row['guid'] for row in receipt['objects'].values() if row.get('type') == 'AnimatorOverrideController'}
    def fix_override_references(raw):
        for key in override_guids:
            raw = raw.replace(('guid: ' + key + ', type: 3').encode(), ('guid: ' + key + ', type: 2').encode())
        return raw
    planned, installed, reused = [], {}, []
    for name, expected in receipt['outputs'].items():
        source = work / 'gift-assets' / name
        if sha(source.read_bytes()) != expected: raise ValueError('Staged panel dependency changed: ' + name)
        if name.endswith('.meta'): continue
        source_key = guid(Path(str(source) + '.meta'))
        key = aliases.get(source_key, source_key)
        target = existing.get(key, destination / name)
        raw = remap(source.read_bytes())
        meta_raw = canonical_meta.get(source_key, Path(str(source) + '.meta').read_bytes())
        if target.exists():
            old_raw = target.read_bytes()
            if raw != old_raw:
                old_hash = previous and previous['installed_output_sha256'].get(target.relative_to(ROOT).as_posix())
                if sha(old_raw) != old_hash or fix_override_references(old_raw) != raw:
                    raise ValueError('Preserve differing asset: ' + str(target))
                planned.append((raw, target))
            existing_meta = Path(str(target) + '.meta').read_bytes()
            if meta_raw != existing_meta:
                if yaml.safe_load(meta_raw) != yaml.safe_load(existing_meta):
                    raise ValueError('Preserve differing metadata: ' + str(target))
                meta_raw = existing_meta  # Preserve equivalent existing line endings.
            reused.append(target.relative_to(ROOT).as_posix())
        else: planned.extend(((raw, target), (meta_raw, Path(str(target) + '.meta'))))
        for content, right in ((raw, target), (meta_raw, Path(str(target) + '.meta'))):
            installed[right.relative_to(ROOT).as_posix()] = sha(content)
    for raw, target in planned:
        target.parent.mkdir(parents=True, exist_ok=True); target.write_bytes(raw)
    for folder in [destination, *[path for path in destination.rglob('*') if path.is_dir()]]:
        meta = metadata(folder, 'DefaultImporter', True); installed[meta.relative_to(ROOT).as_posix()] = sha(meta.read_bytes())
    script = ROOT / 'unity/Assets/Game/Runtime/Offline/PartnersUI/OfficialPartnerPanelAssetCatalog.cs'
    script_meta = metadata(script)
    catalog = ROOT / 'unity/Assets/Game/Resources/Partners/OfficialPartnerPanelAssetCatalog.asset'
    body = dict(m_ObjectHideFlags=0, m_CorrespondingSourceObject=dict(fileID=0), m_PrefabInstance=dict(fileID=0),
        m_PrefabAsset=dict(fileID=0), m_GameObject=dict(fileID=0), m_Enabled=1, m_EditorHideFlags=0,
        m_Script=dict(fileID=11500000, guid=guid(script_meta), type=3), m_Name='OfficialPartnerPanelAssetCatalog',
        m_EditorClassIdentifier='', panelPrefab=receipt['roots']['NpcPanel'], texts=receipt['panel_texts'])
    catalog.write_text('%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\n' +
        yaml.dump(dict(MonoBehaviour=body), Dumper=Dumper, sort_keys=False, allow_unicode=True, width=1000000), 'utf8', newline='\n')
    for path in (catalog, metadata(catalog, 'NativeFormatImporter'), script, script_meta):
        installed[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
    receipt.update(installed_output_sha256=installed, reused_assets=reused, canonical_animation_references=alias_proof)
    (ROOT / 'assets/reference' / NAME).write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    print('Installed complete original panel:', len(installed), 'paths; reused', len(reused), 'assets', flush=True)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--install', action='store_true')
    work = validation_directory()
    if parser.parse_args().install: install(work)
    else: PartnerPanelBinding(work).build()
