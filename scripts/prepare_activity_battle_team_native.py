"""Bind the exact official activity invitation subtree and its Windows resource closure."""
import copy
import hashlib
import json
from pathlib import Path

from official_apk import ROOT
from official_manifest import read_asset_manifest
from npc_travel_language import language
from prepare_activity_battle_selector_native import SelectorBinding, encode
from prepare_partner_card_native import guid, sha, script_id
from prepare_partner_input_dialog import metadata
from validation_workspace import validation_directory


class TeamBinding(SelectorBinding):
    def register_scripts(self):
        super().register_scripts()
        for name, relative in (
            ('ActivityDomainBossTeamButton', 'ActivityBattleUI/ActivityDomainBossTeamButton.cs'),
            ('CountRedDotImage', 'WorldUI/WorldCountRedDotImage.cs'),
        ):
            path = ROOT / 'unity/Assets/Game/Runtime/Official' / relative
            pointer = dict(fileID=11500000, guid=guid(Path(str(path) + '.meta')), type=3)
            self.scripts[name] = pointer
            self.class_ids[script_id('', name)] = pointer
            self.script_guids.add(pointer['guid'])
            self.script_sources[name] = dict(path=path.relative_to(ROOT).as_posix(), pointer=pointer,
                sha256=sha(path.read_bytes()), meta_sha256=sha(Path(str(path) + '.meta').read_bytes()))


def extract(panel, consumer_type='ActivityDomainBossPanelBattleContent', field='mTeamBtn', *, parent_levels=0):
    consumer, = [row for row in panel['scripts'] if row['type'] == consumer_type]
    component = panel['documents'][consumer['nativeFileID']]['MonoBehaviour'][field]['fileID']
    root = panel['documents'][component]['MonoBehaviour']['m_GameObject']['fileID']
    for _ in range(parent_levels):
        transform_id = panel['documents'][root]['GameObject']['m_Component'][0]['component']['fileID']
        transform = next(iter(panel['documents'][transform_id].values()))
        parent = transform['m_Father']['fileID']
        if not parent: raise ValueError('Original referenced subtree has no requested parent')
        root = next(iter(panel['documents'][parent].values()))['m_GameObject']['fileID']
    selected = set()
    def visit(go):
        selected.add(go)
        components = panel['documents'][go]['GameObject']['m_Component']
        selected.update(row['component']['fileID'] for row in components)
        transform = next(iter(panel['documents'][components[0]['component']['fileID']].values()))
        for child in transform['m_Children']:
            visit(next(iter(panel['documents'][child['fileID']].values()))['m_GameObject']['fileID'])
    visit(root)
    result = copy.deepcopy(panel)
    result['documents'] = {key: value for key, value in result['documents'].items() if key in selected}
    result['scripts'] = [row for row in result['scripts'] if row['nativeFileID'] in selected]
    result['objectMap'] = {key: value for key, value in result['objectMap'].items() if value in selected}
    result['root'] = root
    transform_id = result['documents'][root]['GameObject']['m_Component'][0]['component']['fileID']
    transform = next(iter(result['documents'][transform_id].values()))
    original_parent = transform['m_Father']
    transform['m_Father'] = dict(fileID=0)
    def check(value):
        if isinstance(value, dict):
            if set(value) == {'fileID'} and value['fileID'] and value['fileID'] not in selected:
                raise ValueError('Activity invitation references outside the original subtree: ' + repr(value))
            for child in value.values(): check(child)
        elif isinstance(value, list):
            for child in value: check(child)
    check(result['documents'])
    return result, dict(consumer=consumer['original'], field=field, native_root=root,
        original_parent=original_parent, objects=len(selected),
        adaptation='Exact original referenced subtree; detach root parent only. Preserve original inactive state.')


def build(binding, *, consumer_type='ActivityDomainBossPanelBattleContent', field='mTeamBtn',
          prefab_name='ActivityDomainBossTeamButton', group='Team', extra_text_keys=(),
          parent_levels=0, decorate_selection=None, literal_text_keys=(), complete_template=None):
    template = complete_template or dict(name='ActivityDomainBossPanel',
        bundle='defaultpackage_assets_prefabs_ui_activitydomainboss.bundle',
        native_relative='Prefabs/UI/ActivityDomainBoss/ActivityDomainBossPanel.prefab')
    binding.recovery.recover(template['name'], binding.work / 'activity-team-native',
        bundle=template['bundle'], native_relative=template['native_relative'])
    binding.recovery.finalize()
    native, _, panel, record = binding.recovery.outputs[-1]
    for path in (native, Path(str(native) + '.meta')):
        expected = binding.checked[path.relative_to(binding.cache / 'native-export-all').as_posix()]
        if sha(path.read_bytes()) != expected: raise ValueError('Native invitation source changed')
        binding.inputs[path.relative_to(ROOT).as_posix()] = expected
    if complete_template is None:
        team, selection = extract(panel, consumer_type, field, parent_levels=parent_levels)
    else:
        team = copy.deepcopy(panel)
        selection = dict(native_root=panel['root'], objects=len(panel['documents']),
            adaptation='Complete original standalone prefab; preserve every node, field and component.')
    for key, pointer in team['external'].items(): binding.objects[pointer['guid']] = binding.original(key)
    for key, file_id in team['objectMap'].items():
        owner = binding.original(key)
        binding.learn(binding.recovery.fields(owner), next(iter(team['documents'][file_id].values())), owner)
    text_keys = set()
    for row in team['scripts']:
        fields = team['documents'][row['nativeFileID']]['MonoBehaviour']
        full_name = '.'.join(filter(None, [row['namespace'], row['type']]))
        if full_name not in binding.scripts: raise ValueError('Original invitation script pending: ' + full_name)
        fields['m_Script'] = binding.scripts[full_name]
        if row['type'] == 'ButtonSound':
            owner = binding.original(row['original'])
            fields['ClickEvent'] = binding.sound(binding.recovery.fields(owner)['ClickEvent'], owner)
        if row['type'] == 'MultiLanguageText' and fields['myKey']: text_keys.add(fields['myKey'])
    text_keys.update(extra_text_keys)
    if decorate_selection is not None: decorate_selection(binding, panel, team, selection)
    resolved = binding.replace(team['documents'])
    texts, sources = language(text_keys)
    # The original localization service returns the key itself when absent.
    # Allow only explicitly verified literal preview keys; other unknown keys fail.
    literal_keys = ({'{0}'} | set(literal_text_keys)) & text_keys - set(texts)
    if literal_keys:
        texts.update({key: key for key in literal_keys})
        for name in ('LocalizationBinaryTextService', 'LocalizationTextService'):
            path = ROOT / '.tools/source-packages/current' / binding.recovery.apk.version / 'official-code/framework-firstpass-source/Framework' / (name + '.cs')
            binding.inputs[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
    if set(texts) != text_keys: raise ValueError('Original invitation text missing')
    binding.inputs.update(sources)
    records = read_asset_manifest(binding.recovery.apk.source.manifest_bytes('DefaultPackage', binding.recovery.apk.archive))
    font = binding.chinese_font(records)
    font_data, = [data for _, value, data in binding.queue if value == font['font_asset']['guid']]
    fields = font_data[font['font_asset']['fileID']]['MonoBehaviour']
    for key in ('m_SourceFontFile', 'm_SourceFontFile_EditorRef'): fields[key] = font['source_font']
    fields['m_SourceFontFileGUID'] = font['source_font']['guid']
    # MultiLanguageText.RefreshFont replaces the prefab's preview font with the
    # region font at runtime. Resolve that same CN binding explicitly here;
    # the Japanese preview font deliberately has no CN source font configured.
    font['localized_texts'] = []
    language_objects = {team['documents'][row['nativeFileID']]['MonoBehaviour']['m_GameObject']['fileID']
        for row in team['scripts'] if row['type'] == 'MultiLanguageText'}
    for row in team['scripts']:
        if row['type'] != 'TextMeshProUGUI': continue
        text_fields = resolved[row['nativeFileID']]['MonoBehaviour']
        if text_fields['m_GameObject']['fileID'] not in language_objects: continue
        font['localized_texts'].append(dict(component=row['original'], preview_font=text_fields['m_fontAsset'],
            preview_material=text_fields['m_sharedMaterial']))
        text_fields['m_fontAsset'] = font['font_asset']
        text_fields['m_sharedMaterial'] = fields['material']
    binding.queue.extend((path, value, raw) for path, (value, raw) in binding.audio_outputs.items())
    for _, _, data in binding.queue:
        if isinstance(data, dict):
            result = binding.replace(data); data.clear(); data.update(result)
    prefix = 'zjcs:activity-' + group.lower() + ':'
    root_guid = hashlib.md5((prefix + prefab_name).encode()).hexdigest()
    binding.queue.append((Path('Resources/DailyChallenges') / group / (prefab_name + '.prefab'), root_guid, resolved))
    binding.queue.append((Path('Resources/DailyChallenges') / group / 'texts.json', hashlib.md5((prefix + 'texts').encode()).hexdigest(),
        (json.dumps(dict(texts=[dict(key=k, text=texts[k]) for k in sorted(texts)]), ensure_ascii=False, indent=2) + '\n').encode()))
    return record, selection, font


def install(binding, record, selection, font, *, group='Team', receipt_name='activity-team-native-source-manifest.json',
            extra_helpers=()):
    # Own each serialized asset identity, including the dynamic font and atlas.
    # No other module's resources or importer settings are overwritten.
    aliases = {value: hashlib.md5(('zjcs:activity-' + group.lower() + '-resource:' + value).encode()).hexdigest()
        for _, value, _ in binding.queue}
    # Different official shader objects can convert to the exact same native
    # payload/path. Preserve both source records while sharing that output's GUID.
    unique = {}
    for relative, value, data in binding.queue:
        raw = encode(data) if isinstance(data, dict) else data
        if relative in unique:
            prior_value, prior_data, prior_raw = unique[relative]
            if raw != prior_raw: raise ValueError('Conflicting activity resource outputs: ' + str(relative))
            aliases[value] = aliases[prior_value]
        else: unique[relative] = (value, data, raw)
    def remap(value):
        if isinstance(value, dict):
            return {key: aliases.get(child, child) if key == 'guid' else remap(child) for key, child in value.items()}
        if isinstance(value, list): return [remap(child) for child in value]
        if isinstance(value, str): return aliases.get(value, value)
        return value
    destination = ROOT / 'unity/Assets/Official/DailyChallenges/UI' / group
    receipt_path = ROOT / 'assets/reference/daily-challenges' / receipt_name
    previous = json.loads(receipt_path.read_bytes()).get('installed', {}) if receipt_path.exists() else {}
    planned = []
    for relative, (value, data, _) in unique.items():
        target = destination / relative
        data = remap(data)
        raw = encode(data) if isinstance(data, dict) else data
        if target.exists() and target.read_bytes() != raw and sha(target.read_bytes()) != previous.get(target.relative_to(ROOT).as_posix()):
            raise ValueError('Preserve changed invitation output: ' + str(target))
        meta = Path(str(target) + '.meta')
        if meta.exists() and guid(meta) != aliases[value]:
            raise ValueError('Activity control metadata identity changed: ' + str(meta))
        planned.append((target, aliases[value], raw, data))
    installed = {}
    for path, value, raw, data in planned:
        path.parent.mkdir(parents=True, exist_ok=True); path.write_bytes(raw)
        meta = Path(str(path) + '.meta')
        if path.suffix == '.prefab': importer = 'PrefabImporter:\n  externalObjects: {}\n'
        elif isinstance(data, dict): importer = 'NativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: ' + str(next(iter(data))) + '\n'
        else: importer = ''
        expected_meta = ('fileFormatVersion: 2\nguid: ' + value + '\n' + importer).encode()
        if meta.exists() and guid(meta) != value: raise ValueError('Activity control metadata identity changed: ' + str(meta))
        if not meta.exists(): meta.write_bytes(expected_meta)
        for output in (path, meta): installed[output.relative_to(ROOT).as_posix()] = sha(output.read_bytes())
        parent = path.parent
        while parent.is_relative_to(destination):
            folder_meta = metadata(parent, 'DefaultImporter', True)
            installed[folder_meta.relative_to(ROOT).as_posix()] = sha(folder_meta.read_bytes())
            parent = parent.parent
    used = set()
    def scripts(value):
        if isinstance(value, dict):
            if isinstance(value.get('m_Script'), dict): used.add(value['m_Script'].get('guid'))
            for child in value.values(): scripts(child)
        elif isinstance(value, list):
            for child in value: scripts(child)
    for _, _, data in binding.queue: scripts(data)
    for helper in ('prepare_activity_battle_team_native.py', 'prepare_activity_battle_selector_native.py',
                   'prepare_activity_battle_team_ui.py', 'prepare_partner_card_native.py',
                   'prepare_partner_gift_native.py', 'prepare_partner_roster_native.py',
                   'prepare_partner_panel_native.py', 'prepare_partner_input_dialog.py',
                   'prepare_unity_map13_native_sprite_yaml.py', 'npc_travel_language.py') + tuple(extra_helpers):
        path = ROOT / 'scripts' / helper; binding.inputs[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
    receipt = dict(manifest=binding.recovery.apk.version, prefab=record, selection=selection, inputs=binding.inputs,
        scripts={name: row for name, row in binding.script_sources.items() if row['pointer']['guid'] in used},
        resources=binding.proof, audio=binding.audio_proof, font=remap(font), installed=installed,
        isolated_resource_guids=aliases,
        stage='Original ' + group.lower() + ' control bound. Full activity page and production host pending.')
    receipt_path.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    print('Installed original activity ' + group.lower() + ':', selection['objects'], 'objects,', len(installed), 'paths', flush=True)


def main():
    binding = TeamBinding(validation_directory())
    try: install(binding, *build(binding))
    finally: binding.recovery.apk.archive.close()


if __name__ == '__main__': main()
