"""Recover the complete selected bed window and its original dependency closure."""
import hashlib
import argparse
import json
from pathlib import Path
import re
import subprocess
import sys
import wave

from prepare_home_currency_windows_native import HomeCurrencyWindowsBinding
from prepare_home_repair_native import HomeRepairBinding
from prepare_partner_gift_native import PartnerGiftBinding
from prepare_partner_card_native import ROOT, guid, sha, script_id, PPtr
from prepare_current_partner_native import identity
from prepare_current_partner_native import documents
from prepare_partner_input_dialog import metadata
from npc_travel_language import language
from official_manifest import read_asset_manifest
from official_tables import Constructors
from validation_workspace import validation_directory
from deduplicate_unity_textures import reuse_staged_shared_textures

RECEIPT = ROOT/'assets/reference/home-hangup-windows-native-source-manifest.json'
DESTINATION = ROOT/'unity/Assets/Official/Home/UI/HangUpWindows'
CATALOG = ROOT/'unity/Assets/Game/Resources/Home/OfficialHomeHangUpWindowAssetCatalog.asset'


class HomeHangUpWindowsBinding(HomeCurrencyWindowsBinding):
    def sound(self, payload, owner):
        pointer = payload['WwiseObjectReference']
        if not pointer['m_PathID']: return super().sound(payload, owner)
        obj = PPtr(**pointer, assetsfile=owner.assets_file).deref()
        event = self.recovery.fields(obj)
        if event['id'] != 3729877424: return super().sound(payload, owner)
        assert not payload.get('idInternal') and not payload.get('valueGuidInternal')
        key = identity(obj)
        if key in self.audio_proof: return self.audio_proof[key]['pointer']
        manifest = self.cache/'raw-resources-source-manifest.json'
        media = json.loads(manifest.read_bytes())
        assert media['manifest'] == self.recovery.apk.version and media['completed']
        package, = [row for row in media['packages'] if str(event['id']) in row.get('control_objects', {})]
        control = package['control_objects'][str(event['id'])]
        assert control['type'] == 4
        bank, = [row for row in package['banks'] if row['id'] == control['bank_id'] and row['language'] == 0]
        original = self.cache/'raw-resources'/bank['path']; raw = original.read_bytes()
        assert sha(raw) == bank['sha256']
        self.inputs[manifest.relative_to(ROOT).as_posix()] = sha(manifest.read_bytes())
        self.inputs[original.relative_to(ROOT).as_posix()] = bank['sha256']
        stage = self.work/('sound-'+str(event['id']))
        banks, txtps = stage/'banks', stage/'txtp'
        banks.mkdir(parents=True, exist_ok=True); txtps.mkdir(exist_ok=True)
        (banks/(str(bank['id'])+'.bnk')).write_bytes(raw)
        wwiser, decoder = ROOT/'.tools/wwiser/wwiser.py', ROOT/'.tools/vgmstream/vgmstream-cli.exe'
        command = [sys.executable, str(wwiser), str(banks/(str(bank['id'])+'.bnk')),
            '-g', '-d', 'none', '-go', str(txtps), '-gw', '../banks/', '-gv', '0dB', '-gf', str(event['id'])]
        subprocess.run(command, cwd=stage, check=True, capture_output=True,
            creationflags=getattr(subprocess, 'CREATE_NO_WINDOW', 0))
        txtp, = [path for path in txtps.glob('*.txtp') if re.search(r'CAkEvent\[\d+\] '+str(event['id'])+r'\b', path.read_text('utf8'))]
        graph = txtp.read_text('utf8')
        leaves = [line.strip() for line in graph.splitlines() if line.strip() and not line.lstrip().startswith('#')]
        # Verify the entire selected fixed graph before rendering it. vgmstream
        # interprets the original TXTP layer, delay and gain operators directly.
        assert leaves == [
            '../banks/775127471.bnk #s38 #i  ##204256875.wem',
            '../banks/775127471.bnk #s38 #i #p 0.5  ##204256875.wem',
            '../banks/775127471.bnk #s38 #i #p 1.0  ##204256875.wem',
            '../banks/775127471.bnk #s76 #i #p 1.5  ##423609989.wem',
            '../banks/775127471.bnk #s161 #i  #v -6.0dB  ##1001275208.wem',
            'group = -L5 #@layer-v'], graph
        assert 'master volume: auto' not in graph and bank['id'] == 775127471
        wav = stage/(event['objectName']+'.wav')
        subprocess.run([str(decoder), '-i', '-o', str(wav), str(txtp)], check=True, capture_output=True,
            creationflags=getattr(subprocess, 'CREATE_NO_WINDOW', 0))
        data = wav.read_bytes()
        with wave.open(str(wav), 'rb') as audio:
            pcm = dict(channels=audio.getnchannels(), rate=audio.getframerate(), frames=audio.getnframes(), sample_width=audio.getsampwidth())
        target = Path('Audio')/(event['objectName']+'.wav')
        target_guid = hashlib.md5(('zjcs:partner-current-sound:'+key).encode()).hexdigest()
        result = dict(fileID=8300000, guid=target_guid, type=3)
        self.audio_outputs[target] = (target_guid, data); self.script_guids.add(target_guid)
        self.audio_proof[key] = dict(event=event, original_sha256=sha(obj.get_raw_data()),
            source=self.recovery.apk.provenance(obj), bank=bank, package=package['name'], txtp=graph,
            interpretation='Complete fixed five-layer original TXTP; delays 0/0.5/1/1.5 seconds and original -6dB layer retained.',
            pcm=pcm, output=target.as_posix(), output_sha256=sha(data), pointer=result,
            tools={str(path.relative_to(ROOT)): sha(path.read_bytes()) for path in (wwiser, decoder)})
        print('Resolved complete original layered bed sound:', event['objectName'], flush=True)
        return result

    def register_scripts(self):
        super().register_scripts()
        for name in ('FurnitureBedDialog', 'FurnitureOutputInfo', 'FurnitureHangUpQuickReceive',
                     'BlessingRuleBtn', 'OutputAdditionDetailButton', 'BlessingLevelRuleDialog', 'BlessingLevelRuleItem', 'RuleTipNumer'):
            path = ROOT/'unity/Assets/Game/Runtime/Official/HomeHangUpWindowsUI'/(name+'.cs')
            meta = Path(str(path)+'.meta')
            ref = dict(fileID=11500000, guid=guid(meta), type=3)
            self.scripts[name] = ref; self.class_ids[script_id('', name)] = ref
            self.script_guids.add(ref['guid'])
            self.script_sources[name] = dict(path=path.relative_to(ROOT).as_posix(), pointer=ref,
                sha256=sha(path.read_bytes()), meta_sha256=sha(meta.read_bytes()))
        for name in ('RuleDialog', 'TipInfo'):
            path = ROOT/'unity/Assets/Game/Runtime/Official/ProfessionUI'/(name+'.cs')
            ref = dict(fileID=11500000, guid=guid(Path(str(path)+'.meta')), type=3)
            self.scripts[name] = ref; self.class_ids[script_id('', name)] = ref
            self.script_guids.add(ref['guid'])
            self.script_sources[name] = dict(path=path.relative_to(ROOT).as_posix(), pointer=ref,
                sha256=sha(path.read_bytes()), meta_sha256=sha(Path(str(path)+'.meta').read_bytes()))

    def tip_address(self, obj, original_type):
        key = identity(obj)
        if key in self.original_refs: return self.original_refs[key]
        result = super().tip_address(obj, original_type)
        previous = result['guid']
        key_guid = hashlib.md5(('zjcs:home-hangup-window-tip-address:'+key).encode()).hexdigest()
        result['guid'] = key_guid; self.script_guids.add(key_guid)
        payload = hashlib.md5(b'zjcs:home-hangup-window-tip-source-objects').hexdigest()
        self.script_guids.add(payload)
        for index, (path, asset_guid, data) in enumerate(self.queue):
            if asset_guid == previous:
                self.queue[index] = (path, key_guid, data)
                for doc in data.values(): doc['MonoBehaviour']['sourceGraphData']['guid'] = payload
        self.proof[key]['guid'] = key_guid
        return result

    def friendship_templates(self):
        self.recovery.recover('BlessingLevelRuleDialog', self.work/'hangup-native',
            bundle='defaultpackage_assets_prefabs_ui_furniture.bundle',
            native_relative='Prefabs/UI/Furniture/BlessingLevelRuleDialog.prefab')
        self.recovery.recover('FurnitureBedDialog', self.work/'hangup-native',
            bundle='defaultpackage_assets_prefabs_ui_furniture.bundle',
            native_relative='Prefabs/UI/Furniture/FurnitureBedDialog.prefab')
        templates = HomeRepairBinding.friendship_templates(self)
        # The notification embeds tip selectors whose graph payload belongs to
        # this module. Preserve the earlier repair module's prefab identities.
        renames = {name: 'HomeHangUpWindows'+name for name in ('FurnitureRepairDialog', 'FurnitureRepairedNotifyDialog')}
        redirects = {hashlib.md5(('zjcs:partner-current:'+name+'.prefab').encode()).hexdigest():
                     hashlib.md5(('zjcs:partner-current:'+target+'.prefab').encode()).hexdigest()
                     for name, target in renames.items()}
        for pointer in self.home_template_pointers.values():
            if pointer['guid'] in redirects: pointer['guid'] = redirects[pointer['guid']]
        self.script_guids.update(redirects.values())
        templates = [(renames.get(name, name), panel) for name, panel in templates]
        for _, panel in templates:
            for script in panel['scripts']:
                if script['type'] == 'FurnitureBedDialog':
                    owner = self.original(script['original'])
                    self.sound(self.recovery.fields(owner)['FastHangUpSoundEvent'], owner)
        # The reward image is loaded by code, so it is outside prefab closure.
        # Resolve the selected HangUpInfo address through the actual manifest.
        info, = [row for row in self.hang_up_rows if row['Id'] == 1]
        path = 'Assets/Image/'+info['Icon']
        records = read_asset_manifest(self.recovery.apk.source.manifest_bytes('DefaultPackage', self.recovery.apk.archive))
        record, = [row for row in records if row.get('address', '').casefold() == path.casefold()
                   or row['path'].casefold() == (path+'.png').casefold()]
        self.bind_dynamic_sprite(record['guid'])
        self.reward_image = dict(path=info['Icon'], **self.dynamic_sprites_by_guid[record['guid']])
        missing = {'.'.join(filter(None, (s['namespace'], s['type'])))
                   for _, panel in templates for s in panel['scripts']} - self.scripts.keys()
        assert not missing, missing
        self.entry_text_keys = {doc['MonoBehaviour']['myKey'] for _, panel in templates
            for doc in panel['documents'].values() if doc.get('MonoBehaviour', {}).get('myKey')}
        self.entry_text_keys.update(('ui_common_world_level', 'hangup_output_add_loss_goddess_nums',
            'hangup_output_add_loss_goddess', 'fast_hang_up_no_award', 'home_bed_confirm1',
            'home_bed_confirm2', 'item_desc_bed', 'ui_sleep_text', 'ui_build_text', 'ui_fix_text',
            'ui_item_not_enough', 'hangup_output_add_feeder_explore', 'hangup_output_add_astrological',
            'UI_Pay_PeriodicCard', 'hangup_output_add_treasure', 'hangup_output_add_title',
            'hang_up_prop_add_sum_speed_and_percent', 'ui_common_percent'))
        prop_type = ROOT/'.tools/source-packages/current'/self.recovery.apk.version/'official-code/common-source/Common/PropType.cs'
        names = {int(value): name for name, value in re.findall(r'^\s*(\w+) = (\d+),?$', prop_type.read_text('utf-8-sig'), re.M)}
        self.entry_text_keys.update('PropType.'+names[info[field]]
            for field in ('EffectFixedPropType', 'EffectPercentPropType', 'MaxStorePropType', 'FinallyEffectPercentPropType'))
        self.inputs[prop_type.relative_to(ROOT).as_posix()] = sha(prop_type.read_bytes())
        self.entry_text_keys.update('item_'+str(row['ClassId'])+'_name' for row in self.furniture_rows)
        self.entry_text_keys.update(group['TypeNameKey'] for group in self.settings['fields']['mFurnitureClassifications'])
        for name in self.icons: self.entry_text_keys.update(('item_unrepair_'+name, 'item_unrepair_desc_'+name))
        code = ROOT/'.tools/source-packages/current'/self.recovery.apk.version/'official-code'
        constructors = Constructors(code)
        kind, = re.findall(r'new (Tb\w+)\(loader\("(?:[^"/]*/)?rule_tip"\)\)', constructors.source('Tables'))
        obj, = [obj for obj in self.recovery.apk.objects('defaultpackage_assets_config_luban.bundle')
            if obj.type.name == 'TextAsset' and obj.read().m_Name.rsplit('/', 1)[-1] == 'rule_tip.g']
        raw = obj.read().m_Script.encode('utf8', 'surrogateescape')
        rows = [row for row in constructors.table(raw, kind) if row['RuleKey'] == 'BlessingLevelRule']
        assert sum(row['Order'] == 0 for row in rows) == 1 and len(rows) > 1
        self.blessing_rule = dict(Key='BlessingLevelRule', TitleKey='rule_tip_BlessingLevelRule_0',
            TipKeys=['rule_tip_BlessingLevelRule_'+str(row['Order']) for row in sorted(rows, key=lambda row: row['Order']) if row['Order'] != 0])
        self.entry_text_keys.update([self.blessing_rule['TitleKey'], *self.blessing_rule['TipKeys'], 'ui_rule_hours'])
        for name in (*constructors.sources, 'RuleTipInfoParser'):
            item = code/'common-source/Common'/(name+'.cs')
            if not item.is_file(): item = code/'common-source/Common.LubanGen'/(name+'.cs')
            self.inputs[item.relative_to(ROOT).as_posix()] = sha(item.read_bytes())
        self.template_selection['blessing_rule'] = dict(source=self.recovery.apk.provenance(obj),
            object_sha256=sha(obj.get_raw_data()), payload_sha256=sha(raw), rows=rows, parsed=self.blessing_rule)
        self.template_selection['scope'] = 'Complete original bed and blessing-rule windows, repair/property templates and configured furniture icon dependencies.'
        return templates

    def build(self):
        PartnerGiftBinding.build(self)
        receipt = json.loads((self.work/'partner-gift-native-source-manifest.json').read_bytes())
        folder = self.work/'gift-assets'
        payload = dict(manifest=self.recovery.apk.version,
            scope='Complete original selector graph dependency payload; evaluated by an explicitly bound owner.',
            objects=getattr(self, 'tip_source_objects', {}))
        path = folder/'home-hangup-window-tip-source-objects.json'
        path.write_text(json.dumps(payload, ensure_ascii=False, indent=2)+'\n', 'utf8', newline='\n')
        meta = Path(str(path)+'.meta')
        meta.write_text('fileFormatVersion: 2\nguid: '+hashlib.md5(b'zjcs:home-hangup-window-tip-source-objects').hexdigest()+'\nTextScriptImporter:\n  externalObjects: {}\n', 'utf8', newline='\n')
        for item in (path, meta): receipt['outputs'][item.name] = sha(item.read_bytes())
        texts, sources = language(self.entry_text_keys)
        missing = self.entry_text_keys-texts.keys()
        assert not missing-{'{0}', '{0}/{1}', '+{0}%'}, missing
        receipt['inputs'].update(sources); receipt['texts'] = texts
        receipt['literal_format_keys'] = sorted(missing)
        path = folder/'home-hangup-window-texts.json'
        path.write_text(json.dumps(dict(manifest=self.recovery.apk.version,
            texts=[dict(key=key, text=text) for key, text in sorted(texts.items())],
            blessingRule=self.blessing_rule), ensure_ascii=False, indent=2)+'\n', 'utf8', newline='\n')
        meta = Path(str(path)+'.meta')
        meta.write_text('fileFormatVersion: 2\nguid: '+hashlib.md5(b'zjcs:home-hangup-window-texts').hexdigest()+'\nTextScriptImporter:\n  externalObjects: {}\n', 'utf8', newline='\n')
        for item in (path, meta): receipt['outputs'][item.name] = sha(item.read_bytes())
        for name in ('prepare_home_hangup_windows_native.py',
                     'prepare_home_currency_windows_native.py', 'prepare_home_repair_native.py'):
            path = ROOT/'scripts'/name; receipt['inputs'][path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
        used = set()
        for relative in receipt['outputs']:
            path = folder/relative
            if path.suffix != '.meta' and path.read_bytes().startswith(b'%YAML'):
                used.update(re.findall(r'm_Script: \{[^\n}]*guid: ([0-9a-f]{32})', path.read_text('utf8')))
        receipt['script_bindings'] = {name: row for name, row in receipt['script_bindings'].items() if row['pointer']['guid'] in used}
        receipt.update(generator_sha256=sha(Path(__file__).read_bytes()),
            reward_image=self.reward_image,
            scope='Original complete bed and blessing-rule windows, selected native scripts/layout/animations and Windows dependency closure. Other income windows and complete runtime/Player validation remain required.')
        helper = ROOT/'scripts/deduplicate_unity_textures.py'
        receipt['inputs'][helper.relative_to(ROOT).as_posix()] = sha(helper.read_bytes())
        reuse_staged_shared_textures(folder, receipt)
        (self.work/'home-hangup-windows-native-source-manifest.json').write_text(json.dumps(receipt, ensure_ascii=False, indent=2)+'\n','utf8',newline='\n')
        print('Original bed window:', len(receipt['outputs']), 'outputs;', len(payload['objects']), 'tip source objects', flush=True)


def install(work):
    receipt = json.loads((work/RECEIPT.name).read_bytes())
    if receipt['generator_sha256'] != sha(Path(__file__).read_bytes()):
        raise ValueError('Rebuild changed original income window generator')
    for relative, expected in receipt['inputs'].items():
        if sha((ROOT/relative).read_bytes()) != expected:
            raise ValueError('Original income window input changed: '+relative)
    for row in receipt['script_bindings'].values():
        path = ROOT/row['path']
        if sha(path.read_bytes()) != row['sha256'] or sha(Path(str(path)+'.meta').read_bytes()) != row['meta_sha256']:
            raise ValueError('Original income window script changed: '+row['path'])
    previous = json.loads(RECEIPT.read_bytes()).get('installed_output_sha256', {}) if RECEIPT.exists() else {}
    existing = {}
    for meta in (ROOT/'unity/Assets').rglob('*.meta'):
        path = Path(str(meta)[:-5])
        if path.is_file():
            key = guid(meta)
            if key in existing: raise ValueError('Existing Unity GUID collision: '+key)
            existing[key] = path

    def equivalent(left, right):
        if left == right: return True
        if left.replace(b'\r\n', b'\n') == right.replace(b'\r\n', b'\n'): return True
        if not left.startswith(b'%YAML') or not right.startswith(b'%YAML'): return False
        def normalize(value):
            if isinstance(value, dict):
                return {key: '' if key == 'm_Name' and child is None else normalize(child)
                        for key, child in value.items()}
            if isinstance(value, list): return [normalize(child) for child in value]
            return value
        return normalize(documents(left.decode('utf8'))) == normalize(documents(right.decode('utf8')))

    plan, installed, reused = [], {}, []
    for relative, expected in receipt['outputs'].items():
        source = work/'gift-assets'/relative
        if sha(source.read_bytes()) != expected: raise ValueError('Staged income window changed: '+relative)
        if relative.endswith('.meta'): continue
        key = guid(Path(str(source)+'.meta'))
        target = existing.get(key, DESTINATION/relative)
        same = target.exists() and equivalent(target.read_bytes(), source.read_bytes())
        if target.exists() and not same:
            if not target.is_relative_to(DESTINATION) or sha(target.read_bytes()) != previous.get(target.relative_to(ROOT).as_posix()):
                raise ValueError('Preserve different existing original resource: '+str(target))
        if same:
            reused.append(dict(path=target.relative_to(ROOT).as_posix(), guid=key, sha256=sha(target.read_bytes()),
                staged_sha256=expected, comparison='Exact bytes, or parsed YAML equality normalizing only null/empty object names.'))
        else:
            plan.extend(((source, target), (Path(str(source)+'.meta'), Path(str(target)+'.meta'))))
        if target.is_relative_to(DESTINATION):
            installed[target.relative_to(ROOT).as_posix()] = sha(target.read_bytes()) if same else expected
            installed[Path(str(target)+'.meta').relative_to(ROOT).as_posix()] = sha(Path(str(target)+'.meta').read_bytes()) if same else sha(Path(str(source)+'.meta').read_bytes())
        existing[key] = target
    # Every comparison succeeds before mutating any installed asset.
    for source, target in plan:
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(source.read_bytes())
    for folder in [DESTINATION, *[p for p in DESTINATION.rglob('*') if p.is_dir()]]:
        meta = metadata(folder, 'DefaultImporter', True)
        installed[meta.relative_to(ROOT).as_posix()] = sha(meta.read_bytes())
    script = ROOT/'unity/Assets/Game/Runtime/Official/HomeHangUpWindowsUI/OfficialHomeHangUpWindowAssetCatalog.cs'
    root = receipt['roots']['FurnitureBedDialog']
    raw = '%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\nMonoBehaviour:\n'
    raw += '  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n'
    raw += '  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: 0}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n'
    raw += '  m_Script: {fileID: 11500000, guid: '+guid(Path(str(script)+'.meta'))+', type: 3}\n'
    raw += "  m_Name: OfficialHomeHangUpWindowAssetCatalog\n  m_EditorClassIdentifier: ''\n"
    raw += '  bedPrefab: {fileID: '+str(root['fileID'])+', guid: '+root['guid']+', type: 3}\n'
    root = receipt['roots']['BlessingLevelRuleDialog']
    raw += '  blessingRulePrefab: {fileID: '+str(root['fileID'])+', guid: '+root['guid']+', type: 3}\n'
    raw += '  texts: {fileID: 4900000, guid: '+hashlib.md5(b'zjcs:home-hangup-window-texts').hexdigest()+', type: 3}\n'
    image = receipt['reward_image']
    raw += '  bedRewardImagePath: '+image['path']+'\n'
    raw += '  bedRewardImage: {fileID: '+str(image['pointer']['fileID'])+', guid: '+image['pointer']['guid']+', type: '+str(image['pointer']['type'])+'}\n'
    if CATALOG.exists() and CATALOG.read_text('utf8') != raw and sha(CATALOG.read_bytes()) != previous.get(CATALOG.relative_to(ROOT).as_posix()):
        raise ValueError('Preserve changed income window catalog')
    CATALOG.write_text(raw, 'utf8', newline='\n')
    for item in (CATALOG, metadata(CATALOG, 'NativeFormatImporter')):
        installed[item.relative_to(ROOT).as_posix()] = sha(item.read_bytes())
    receipt.update(installed_output_sha256=installed, reused=reused,
        runtime_code={p.relative_to(ROOT).as_posix(): sha(p.read_bytes()) for p in script.parent.iterdir() if p.is_file()})
    RECEIPT.write_text(json.dumps(receipt, ensure_ascii=False, indent=2)+'\n', 'utf8', newline='\n')
    print('Installed original income window:', len(installed), 'files; reused', len(reused), 'assets', flush=True)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--install', action='store_true')
    args = parser.parse_args()
    work = validation_directory()
    if args.install: install(work)
    else: HomeHangUpWindowsBinding(work).build()
