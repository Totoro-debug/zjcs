"""Bind selected original profile language, configured reward sprites and prefab."""
import hashlib
import json
from pathlib import Path
import re

import yaml
from UnityPy.enums import ClassIDType

from official_apk import ROOT
from official_manifest import read_asset_manifest
from npc_travel_language import language
from prepare_partner_card_native import PartnerCardBinding, Dumper, guid, identity, sha
from prepare_current_partner_native import documents
from prepare_partner_input_dialog import metadata
from validation_workspace import validation_directory


def main():
    work = validation_directory()
    binding = PartnerCardBinding(work)
    apk = binding.recovery.apk
    rules_path = ROOT / 'unity/Assets/Official/Partners/partner-rules.json'
    rules = json.loads(rules_path.read_bytes())
    if rules['manifest'] != apk.version: raise ValueError('Selected profile rules differ')
    binding.inputs[rules_path.relative_to(ROOT).as_posix()] = sha(rules_path.read_bytes())
    profiles = [row for row in rules['tables']['npc_profile'] if not 11015 <= row['Id'] <= 11022]
    rewards = {int(key) for row in profiles for key in row['Reward']}
    items = {row['ClassId']: row for row in rules['tables']['item'] if row['ClassId'] in rewards}
    if set(items) != rewards: raise ValueError('Configured profile reward item absent')
    records = read_asset_manifest(apk.source.manifest_bytes('DefaultPackage', apk.archive))
    sprites = []
    for item in items.values():
        address = 'Assets/Image/' + item['RawIcon']
        record, = [row for row in records if row['address'] == address or row['path'] == address + '.png']
        obj, = [ref.deref() for name, ref in apk.container(record['bundle'])
            if name.casefold() == record['path'].casefold() and ref.deref().type.name == 'Sprite']
        key, file_id = binding.recovery.fields(obj)['m_RenderDataKey']
        render_guid = ''.join(format(key['data[' + str(i) + ']'], '08x')[::-1] for i in range(4))
        if render_guid != record['guid']: raise ValueError('Profile reward render key differs')
        name = binding.name_key(obj.read(check_read=False).m_Name)
        candidates = []
        for candidate_key in [name] + [name + str(i) for i in range(100)]:
            for candidate in binding.by_name.get(candidate_key, []):
                if candidate.suffix == '.asset':
                    docs = documents(candidate.read_text('utf8'))
                    if len(docs) == 1 and next(iter(docs.values())).get('Sprite', {}).get('m_RenderDataKey') == {render_guid: file_id}:
                        candidates.append(candidate)
        candidate, = candidates
        source_guid = guid(Path(str(candidate) + '.meta'))
        binding.objects[source_guid] = obj
        sprites.append(dict(item_id=item['ClassId'], address=address, pointer=binding.load(source_guid), manifest_asset=record,
            original=identity(obj), original_sha256=sha(obj.get_raw_data()), source=apk.provenance(obj)))
    for target, target_guid, data in binding.queue:
        if isinstance(data, dict):
            resolved = binding.replace(data); data.clear(); data.update(resolved)
    native_path = ROOT / 'assets/reference/partner-profile-native-source-manifest.json'
    native = json.loads(native_path.read_bytes())
    binding.inputs[native_path.relative_to(ROOT).as_posix()] = sha(native_path.read_bytes())
    prefab = ROOT / 'unity/Assets/Official/Partners/UI/Profile/NpcDetailProfileContent.prefab'
    keys = {yaml.safe_load(value) for value in re.findall(r'(?m)^  myKey: (.+)$', prefab.read_text('utf8'))} - {'', None}
    keys.update('npc_profile_Content_' + str(row['Id']) + '_' + str(row['ProfileId']) for row in profiles)
    keys.update(('conditon_unlock_desc_NpcFriendshipLevel', 'ui_ItemIcon_count_key'))
    texts, sources = language(keys)
    # The chapter caption is an original literal format string. The selected
    # LocalizationTextService.GetMissingKeyText returns the key unchanged.
    literal_keys = keys - texts.keys()
    if literal_keys != {'{0}'}: raise ValueError('Original profile language absent: ' + repr(literal_keys))
    fallback = ROOT / '.tools/source-packages/current' / apk.version / 'official-code/framework-firstpass-source/Framework/LocalizationTextService.cs'
    from prepare_partner_gift_configuration import method
    fallback_body = method(fallback.read_text('utf-8-sig'), 'public static string GetMissingKeyText(string key, bool isShowMissingKeyText)')
    if 'return key;' not in fallback_body: raise ValueError('Original literal language fallback changed')
    binding.inputs[fallback.relative_to(ROOT).as_posix()] = sha(fallback.read_bytes())
    language_records = {row['name']: row for row in apk.source.manifest('WwiseBnk', apk.archive)['bundles']}
    for relative, expected in sources.items():
        raw = (ROOT / relative).read_bytes(); record = language_records[Path(relative).name]
        if sha(raw) != expected or len(raw) != record['size'] or hashlib.md5(raw).hexdigest() != record['hash']:
            raise ValueError('Original profile language differs: ' + relative)
        binding.inputs[relative] = expected
    destination = ROOT / 'unity/Assets/Official/Partners/UI/Profile'
    proof_path = ROOT / 'assets/reference/partner-profile-assets-source-manifest.json'
    previous = json.loads(proof_path.read_bytes()).get('outputs', {}) if proof_path.is_file() else {}
    existing = {guid(meta): Path(str(meta)[:-5]) for meta in (ROOT / 'unity/Assets/Official/Partners').rglob('*.meta') if Path(str(meta)[:-5]).is_file()}
    installed, reused = {}, []

    def write(path, raw):
        relative = path.relative_to(ROOT).as_posix()
        if path.is_file() and path.read_bytes() != raw and sha(path.read_bytes()) != previous.get(relative):
            raise ValueError('Preserve unrecorded profile asset: ' + relative)
        path.parent.mkdir(parents=True, exist_ok=True)
        if not path.is_file() or path.read_bytes() != raw: path.write_bytes(raw)
        installed[relative] = sha(path.read_bytes())

    def emit(data):
        result = '%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n'
        for file_id, value in data.items():
            kind, = value
            result += '--- !u!' + str(int(ClassIDType[kind])) + ' &' + str(file_id) + '\n'
            result += yaml.dump(value, Dumper=Dumper, sort_keys=False, allow_unicode=True, width=1000000)
        return result.encode()

    for target, target_guid, data in binding.queue:
        raw = emit(data) if isinstance(data, dict) else data
        path = existing.get(target_guid, destination / target)
        if path.is_file():
            if path.read_bytes() != raw: raise ValueError('Existing profile reward differs: ' + str(path))
            reused.append(path.relative_to(ROOT).as_posix())
        else:
            write(path, raw)
            write(Path(str(path) + '.meta'), ('fileFormatVersion: 2\nguid: ' + target_guid + '\nNativeFormatImporter:\n  externalObjects: {}\n').encode())
        for entry in (path, Path(str(path) + '.meta')): installed[entry.relative_to(ROOT).as_posix()] = sha(entry.read_bytes())
    text_path = destination / 'partner-profile-texts.json'
    write(text_path, (json.dumps(dict(manifest=apk.version, texts=[dict(key=k, text=v) for k, v in sorted(texts.items())]), ensure_ascii=False, indent=2) + '\n').encode())
    write(Path(str(text_path) + '.meta'), ('fileFormatVersion: 2\nguid: ' + hashlib.md5(b'zjcs:partner-profile-texts').hexdigest() + '\nTextScriptImporter:\n  externalObjects: {}\n').encode())
    image_script = ROOT / 'unity/Assets/Game/Runtime/Offline/ProfessionUI/OfflineProfessionImageCatalog.cs'
    images = ROOT / 'unity/Assets/Game/Resources/Partners/OfficialPartnerProfileImages.asset'

    def body(name, script):
        return dict(m_ObjectHideFlags=0, m_CorrespondingSourceObject=dict(fileID=0), m_PrefabInstance=dict(fileID=0),
            m_PrefabAsset=dict(fileID=0), m_GameObject=dict(fileID=0), m_Enabled=1, m_EditorHideFlags=0,
            m_Script=dict(fileID=11500000, guid=guid(Path(str(script) + '.meta')), type=3), m_Name=name, m_EditorClassIdentifier='')

    content = body('OfficialPartnerProfileImages', image_script)
    content['Entries'] = [dict(Path=row['address'], AssetGuid=row['manifest_asset']['guid'], Asset=row['pointer']) for row in sprites]
    attachment = native['template_selection']['attachment']
    content['Entries'].append(dict(Path=attachment['asset']['path'], AssetGuid=attachment['asset']['guid'], Asset=native['roots'][attachment['name']]))
    write(images, emit({11400000: dict(MonoBehaviour=content)}))
    image_meta = metadata(images, 'NativeFormatImporter'); installed[image_meta.relative_to(ROOT).as_posix()] = sha(image_meta.read_bytes())
    gift_path = ROOT / 'assets/reference/partner-gift-controller-source-manifest.json'
    gift = json.loads(gift_path.read_bytes())
    if gift['manifest'] != apk.version: raise ValueError('Reward icon prefab version differs')
    binding.inputs[gift_path.relative_to(ROOT).as_posix()] = sha(gift_path.read_bytes())
    reward_prefab = existing[gift['roots']['ItemIconDefault']['guid']]
    for path in (reward_prefab, Path(str(reward_prefab) + '.meta')):
        relative = path.relative_to(ROOT).as_posix()
        if sha(path.read_bytes()) != gift['installed_output_sha256'][relative]: raise ValueError('Original reward prefab differs')
        binding.inputs[relative] = sha(path.read_bytes())
    script = ROOT / 'unity/Assets/Game/Runtime/Offline/PartnersUI/OfficialPartnerProfileAssetCatalog.cs'
    metadata(script)
    common = ROOT / 'unity/Assets/Game/Resources/Partners/OfficialPartnerGiftPageAssetCatalog.asset'
    for path in (script, Path(str(script) + '.meta'), common, Path(str(common) + '.meta')):
        binding.inputs[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
    content = body('OfficialPartnerProfileAssetCatalog', script)
    content.update(profilePrefab=native['roots']['NpcDetailProfileContent'], rewardIconPrefab=gift['roots']['ItemIconDefault'],
        texts=dict(fileID=4900000, guid=guid(Path(str(text_path) + '.meta')), type=3),
        images=dict(fileID=11400000, guid=guid(image_meta), type=2), common=dict(fileID=11400000, guid=guid(Path(str(common) + '.meta')), type=2))
    catalog = ROOT / 'unity/Assets/Game/Resources/Partners/OfficialPartnerProfileAssetCatalog.asset'
    write(catalog, emit({11400000: dict(MonoBehaviour=content)}))
    catalog_meta = metadata(catalog, 'NativeFormatImporter'); installed[catalog_meta.relative_to(ROOT).as_posix()] = sha(catalog_meta.read_bytes())
    for folder in [destination, *[path for path in destination.rglob('*') if path.is_dir()]]:
        meta = metadata(folder, 'DefaultImporter', True); installed[meta.relative_to(ROOT).as_posix()] = sha(meta.read_bytes())
    for relative in list(installed):
        if relative.endswith('.meta') and (relative.startswith('unity/Assets/Official/Partners/UI/Profile') or
                relative.startswith('unity/Assets/Game/Resources/Partners/OfficialPartnerProfile')):
            meta = ROOT / relative
            cleaned = '\n'.join(line.rstrip() for line in meta.read_text('utf8').splitlines()) + '\n'
            if meta.read_bytes() != cleaned.encode(): meta.write_text(cleaned, 'utf8', newline='\n')
            installed[relative] = sha(meta.read_bytes())
    proof = dict(manifest=apk.version, generator_sha256=sha(Path(__file__).read_bytes()), inputs=binding.inputs,
        outputs=installed, reused=reused, sprites=sprites, objects=binding.proof, language_keys=sorted(keys), literal_keys=sorted(literal_keys),
        scope='Original profile language and reward sprites for the confirmed open NPC scope, exact native subtree and reward icon Resources binding; full host and Player pending.')
    proof_path.write_text(json.dumps(proof, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    print('Bound original profile texts:', len(texts), 'and reward sprites:', len(sprites))


if __name__ == '__main__': main()
