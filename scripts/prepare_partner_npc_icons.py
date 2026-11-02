"""Bind selected NPC/profession icon Sprites to verified native Windows assets."""
import hashlib
import json
from pathlib import Path
import re
import sys
import yaml
from UnityPy.enums import ClassIDType

from official_apk import ROOT
from official_manifest import read_asset_manifest
from official_tables import Constructors
from prepare_partner_card_native import PartnerCardBinding, Dumper, guid, sha
from prepare_current_partner_native import documents, identity
from validation_workspace import validation_directory


def metadata(key, importer):
    return ('fileFormatVersion: 2\nguid: ' + key + '\n' + importer + ':\n  externalObjects: {}\n').encode()


def binding_sources(receipt):
    original = ROOT / '.tools/source-packages/current/884_156692/official-code'
    files = [original / 'client-source' / (name + '.cs') for name in
        ('NpcPanelDialogueListNpcItem', 'PlayerInfoData', 'PlayerInfoUI', 'PlayerIcon', 'UIUtils', 'EUIInstantiateFlag')]
    files += [original / 'common-source/Common' / (name + '.cs') for name in
        ('NpcTargetData', 'PlayerLiteInfo', 'ProfessionBaseInfoParser')]
    files += [original / 'framework-firstpass-source/ResourceLoaderExtensions.cs']
    receipt['official_consumer_sha256'] = {path.relative_to(ROOT).as_posix(): sha(path.read_bytes()) for path in files}
    files = [ROOT / 'unity/Assets/Game/Runtime/Offline/PartnersUI' / name for name in
        ('OfflinePartnerNpcIconBindings.cs', 'OfflinePartnerNpcIconBindings.cs.meta', 'Zjcs.Offline.PartnersUI.asmdef')]
    files += [ROOT / 'scripts' / name for name in ('prepare_partner_npc_icon_validation.py', 'fixtures/partner_npc_icon_validation.cs')]
    files += [ROOT / 'scripts/fixtures/partner_panel_host_validation.cs',
        ROOT / 'unity/Assets/Game/Resources/Partners/OfficialPartnerDialoguePlaybackAssetCatalog.asset',
        ROOT / 'assets/reference/partner-dialogue-playback-native-source-manifest.json']
    receipt['binding_source_sha256'] = {path.relative_to(ROOT).as_posix(): sha(path.read_bytes()) for path in files}
    receipt['generator_sha256'] = sha(Path(__file__).read_bytes())


def main():
    work = validation_directory()
    receipt_path = ROOT / 'assets/reference/partner-npc-icons-source-manifest.json'
    if '--refresh-binding-sources' in sys.argv:
        receipt = json.loads(receipt_path.read_bytes())
        for name in ('inputs', 'installed_output_sha256', 'reused_output_sha256'):
            for relative, expected in receipt[name].items():
                if sha((ROOT / relative).read_bytes()) != expected: raise ValueError('Source or installed icon changed: ' + relative)
        binding_sources(receipt)
        receipt_path.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
        print('Verified native icon inputs/outputs and refreshed consumer binding sources', flush=True)
        return
    binding = PartnerCardBinding(work)
    apk = binding.recovery.apk
    if apk.version != '884_156692': raise ValueError('Review selected icon version')
    rules = ROOT / 'unity/Assets/Official/Partners/partner-rules.json'
    data = json.loads(rules.read_bytes())
    if data['manifest'] != apk.version: raise ValueError('NPC icon table version differs')
    binding.inputs[rules.relative_to(ROOT).as_posix()] = sha(rules.read_bytes())
    constructors = Constructors(ROOT / '.tools/source-packages/current/884_156692/official-code')
    table = ROOT / 'unity/Assets/Official/Partners/NpcRuntime/Tables/profession_base.g.bytes'
    professions = constructors.table(table.read_bytes(), 'TbProfessionBaseInfo')
    binding.inputs[table.relative_to(ROOT).as_posix()] = sha(table.read_bytes())
    combat_table = table.with_name('npc_level_detail.g.bytes')
    combat_classes = {row['ClassId'] for row in constructors.table(combat_table.read_bytes(), 'TbNpcLevelDetailInfo')}
    binding.inputs[combat_table.relative_to(ROOT).as_posix()] = sha(combat_table.read_bytes())
    if any(row['Dialogues'] and row['Id'] not in combat_classes for row in data['tables']['npc']):
        raise ValueError('Review noncombat NPC conversation icons')
    requests = {}
    for row in data['tables']['npc']:
        # The confirmed closed collaboration remains inaccessible.
        if row['Id'] not in combat_classes or 11015 <= row['Id'] <= 11022 or not row['Icon']: continue
        requests.setdefault(row['Icon'], []).append(dict(npc=row['Id'], field='Icon'))
    for row in professions:
        for field in ('Icon1', 'Icon3'):
            if row[field]: requests.setdefault(row[field], []).append(dict(profession=row['Profession'], field=field))
    records = read_asset_manifest(apk.source.manifest_bytes('DefaultPackage', apk.archive))
    sprites = []
    for address, consumers in sorted(requests.items()):
        full = 'Assets/Image/' + address
        matches = [r for r in records if r['address'] == full or r['path'] == full + '.png']
        if len(matches) != 1: raise ValueError('Locate exact original icon address: ' + address + ' / ' + repr(consumers))
        record, = matches
        obj, = [p.deref() for name, p in apk.container(record['bundle'])
                 if name.casefold() == record['path'].casefold() and p.deref().type.name == 'Sprite']
        key, source_id = binding.recovery.fields(obj)['m_RenderDataKey']
        render_guid = ''.join(format(key['data[' + str(i) + ']'], '08x')[::-1] for i in range(4))
        if render_guid != record['guid']: raise ValueError('Sprite render key differs: ' + address)
        name = binding.name_key(obj.read(check_read=False).m_Name)
        matches = []
        for candidate_key in [name] + [name + str(i) for i in range(100)]:
            for candidate in binding.by_name.get(candidate_key, []):
                if candidate.suffix != '.asset': continue
                docs = documents(candidate.read_text('utf8'))
                if len(docs) == 1 and next(iter(docs.values())).get('Sprite', {}).get('m_RenderDataKey') == {render_guid: source_id}:
                    matches.append(candidate)
        candidate, = matches
        source_guid = guid(Path(str(candidate) + '.meta'))
        binding.objects[source_guid] = obj
        pointer = binding.load(source_guid)
        sprites.append(dict(address=address, consumers=consumers, pointer=pointer, manifest_asset=record,
            original=identity(obj), original_sha256=sha(obj.get_raw_data()), source=apk.provenance(obj)))
    for _, _, payload in binding.queue:
        if isinstance(payload, dict):
            resolved = binding.replace(payload); payload.clear(); payload.update(resolved)
    # Reuse an existing native dependency only when both GUID and exact payload
    # match. Never install a duplicate GUID or replace another module's asset.
    existing = {}
    for meta in (ROOT / 'unity/Assets').rglob('*.meta'):
        match = re.search(rb'(?m)^guid: ([0-9a-f]{32})', meta.read_bytes())
        if match and Path(str(meta)[:-5]).is_file(): existing.setdefault(match[1].decode(), []).append(Path(str(meta)[:-5]))
    previous = json.loads(receipt_path.read_bytes()).get('installed_output_sha256', {}) if receipt_path.exists() else {}
    outputs, reused = {}, {}

    def install(path, raw):
        relative = path.relative_to(ROOT).as_posix()
        if path.exists() and path.read_bytes() != raw:
            if sha(path.read_bytes()) != previous.get(relative): raise ValueError('Preserve differing local icon asset: ' + relative)
        path.parent.mkdir(parents=True, exist_ok=True)
        if not path.exists() or path.read_bytes() != raw: path.write_bytes(raw)
        outputs[relative] = sha(raw)

    for target, key, payload in binding.queue:
        if isinstance(payload, dict):
            text = '%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n'
            for file_id, content in payload.items():
                kind, = content
                text += '--- !u!' + str(int(ClassIDType[kind])) + ' &' + str(file_id) + '\n'
                text += yaml.dump(content, Dumper=Dumper, sort_keys=False, allow_unicode=True, width=1000000)
            raw = text.encode()
        else: raw = payload
        path = ROOT / 'unity/Assets/Official/Partners/NpcIcons' / target
        candidates = existing.get(key, [])
        if candidates:
            candidate, = candidates
            if candidate.read_bytes().replace(b'\r\n', b'\n') != raw: raise ValueError('Existing icon dependency differs: ' + str(candidate))
            if candidate != path:
                reused[candidate.relative_to(ROOT).as_posix()] = sha(candidate.read_bytes())
                reused[str(candidate.relative_to(ROOT)).replace('\\', '/') + '.meta'] = sha(Path(str(candidate) + '.meta').read_bytes())
                continue
        install(path, raw)
        install(Path(str(path) + '.meta'), metadata(key, 'NativeFormatImporter'))
    catalog = ROOT / 'unity/Assets/Game/Resources/Partners/OfficialPartnerNpcIconImages.asset'
    script = ROOT / 'unity/Assets/Game/Runtime/Offline/ProfessionUI/OfflineProfessionImageCatalog.cs.meta'
    lines = ['%YAML 1.1', '%TAG !u! tag:unity3d.com,2011:', '--- !u!114 &11400000', 'MonoBehaviour:',
        '  m_ObjectHideFlags: 0', '  m_CorrespondingSourceObject: {fileID: 0}', '  m_PrefabInstance: {fileID: 0}',
        '  m_PrefabAsset: {fileID: 0}', '  m_GameObject: {fileID: 0}', '  m_Enabled: 1', '  m_EditorHideFlags: 0',
        '  m_Script: {fileID: 11500000, guid: ' + guid(script) + ', type: 3}', '  m_Name: OfficialPartnerNpcIconImages',
        "  m_EditorClassIdentifier: ''", '  Entries:']
    for row in sprites:
        p = row['pointer']
        lines += ['  - Path: ' + row['address'], '    AssetGuid: ' + p['guid'],
            '    Asset: {fileID: ' + str(p['fileID']) + ', guid: ' + p['guid'] + ', type: ' + str(p['type']) + '}']
    install(catalog, ('\n'.join(lines) + '\n').encode())
    install(Path(str(catalog) + '.meta'), metadata(hashlib.md5(b'zjcs:partner-npc-icon-images').hexdigest(), 'NativeFormatImporter'))
    for folder in (ROOT / 'unity/Assets/Official/Partners/NpcIcons').rglob('*'):
        if folder.is_dir():
            install(Path(str(folder) + '.meta'), ('fileFormatVersion: 2\nguid: ' + hashlib.md5(('zjcs:' + folder.relative_to(ROOT).as_posix()).encode()).hexdigest() + '\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n').encode())
    folder = ROOT / 'unity/Assets/Official/Partners/NpcIcons'
    install(Path(str(folder) + '.meta'), ('fileFormatVersion: 2\nguid: ' + hashlib.md5(b'zjcs:partner-npc-icons-folder').hexdigest() + '\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n').encode())
    receipt = dict(manifest=apk.version, generator_sha256=sha(Path(__file__).read_bytes()),
        inputs=binding.inputs, sprites=sprites, native_assets=binding.proof, reused_output_sha256=reused,
        installed_output_sha256=outputs)
    binding_sources(receipt)
    receipt_path.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    print('Installed', len(sprites), 'original NPC/profession icon addresses;', len(outputs), 'files;', len(reused), 'reused inputs', flush=True)


if __name__ == '__main__': main()
