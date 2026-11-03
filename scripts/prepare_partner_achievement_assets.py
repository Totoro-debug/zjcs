"""Install the original achievement region, reward sprites and explicit catalog."""
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
    native = json.loads((work / 'partner-achievement-native-source-manifest.json').read_bytes())
    if native['generator_sha256'] != sha((ROOT / 'scripts/prepare_partner_achievement_native.py').read_bytes()):
        raise ValueError('Rebuild achievement native input')
    for relative, digest in native['inputs'].items():
        if sha((ROOT / relative).read_bytes()) != digest: raise ValueError('Achievement input changed: ' + relative)
    binding = PartnerCardBinding(work)
    apk = binding.recovery.apk
    rules_path = ROOT / 'unity/Assets/Official/Partners/partner-rules.json'
    rules = json.loads(rules_path.read_bytes())
    if rules['manifest'] != apk.version: raise ValueError('Selected achievement version differs')
    binding.inputs[rules_path.relative_to(ROOT).as_posix()] = sha(rules_path.read_bytes())
    achievements = [row for row in rules['tables']['achievement'] if row['AchievementType'] in (27, 77)]
    rewards = {int(key) for row in achievements for key in row['Reward']}
    items = {row['ClassId']: row for row in rules['tables']['item'] if row['ClassId'] in rewards}
    if set(items) != rewards: raise ValueError('Original achievement reward item absent')
    records = read_asset_manifest(apk.source.manifest_bytes('DefaultPackage', apk.archive))
    sprites = []
    for item in items.values():
        address = 'Assets/Image/' + item['RawIcon']
        record, = [row for row in records if row['address'] == address or row['path'] == address + '.png']
        obj, = [ref.deref() for name, ref in apk.container(record['bundle'])
            if name.casefold() == record['path'].casefold() and ref.deref().type.name == 'Sprite']
        key, file_id = binding.recovery.fields(obj)['m_RenderDataKey']
        render_guid = ''.join(format(key['data[' + str(i) + ']'], '08x')[::-1] for i in range(4))
        if render_guid != record['guid']: raise ValueError('Achievement reward render key differs')
        name = binding.name_key(obj.read(check_read=False).m_Name)
        candidates = []
        for candidate_key in [name] + [name + str(i) for i in range(100)]:
            for candidate in binding.by_name.get(candidate_key, []):
                if candidate.suffix == '.asset':
                    docs = documents(candidate.read_text('utf8'))
                    if len(docs) == 1 and next(iter(docs.values())).get('Sprite', {}).get('m_RenderDataKey') == {render_guid: file_id}:
                        candidates.append(candidate)
        candidate, = candidates
        source_guid = guid(Path(str(candidate) + '.meta')); binding.objects[source_guid] = obj
        sprites.append(dict(item_id=item['ClassId'], address=address, pointer=binding.load(source_guid), manifest_asset=record,
            original=identity(obj), original_sha256=sha(obj.get_raw_data()), source=apk.provenance(obj)))
    for _, _, data in binding.queue:
        if isinstance(data, dict):
            resolved = binding.replace(data); data.clear(); data.update(resolved)
    destination = ROOT / 'unity/Assets/Official/Partners/UI/Achievements'
    proof_path = ROOT / 'assets/reference/partner-achievement-ui-source-manifest.json'
    previous = json.loads(proof_path.read_bytes()).get('outputs', {}) if proof_path.is_file() else {}
    existing = {guid(meta): Path(str(meta)[:-5]) for meta in (ROOT / 'unity/Assets/Official/Partners').rglob('*.meta') if Path(str(meta)[:-5]).is_file()}
    installed, reused, canonical = {}, [], []

    def write(path, raw):
        relative = path.relative_to(ROOT).as_posix()
        if path.is_file() and path.read_bytes() != raw and sha(path.read_bytes()) != previous.get(relative):
            raise ValueError('Preserve unrecorded achievement asset: ' + relative)
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

    plan = []
    for relative, expected in native['outputs'].items():
        if relative.endswith('.meta') or relative == 'partner-gift-texts.json': continue
        path = work / 'gift-assets' / relative
        if sha(path.read_bytes()) != expected: raise ValueError('Staged achievement changed: ' + relative)
        key = guid(Path(str(path) + '.meta'))
        plan.append((relative, key, path.read_bytes(), Path(str(path) + '.meta').read_bytes()))
    for target, key, data in binding.queue:
        raw = emit(data) if isinstance(data, dict) else data
        plan.append((target, key, raw, ('fileFormatVersion: 2\nguid: ' + key + '\nNativeFormatImporter:\n  externalObjects: {}\n').encode()))
    for target, key, raw, meta in plan:
        path = existing.get(key, destination / target)
        if path.is_file():
            old = path.read_bytes()
            if old != raw:
                # Earlier imports serialized the original unnamed Animator
                # transitions as YAML null; keep the shared controller intact.
                if path.suffix != '.controller' or old.replace(b'  m_Name: null\n', b"  m_Name: ''\n") != raw:
                    raise ValueError('Existing achievement dependency differs: ' + str(path))
                canonical.append(dict(path=path.relative_to(ROOT).as_posix(), staged_sha256=sha(raw), installed_sha256=sha(old),
                    reason='Preserve existing original unnamed Animator transition serialization.'))
            reused.append(path.relative_to(ROOT).as_posix())
        else:
            write(path, raw); write(Path(str(path) + '.meta'), meta)
        for entry in (path, Path(str(path) + '.meta')): installed[entry.relative_to(ROOT).as_posix()] = sha(entry.read_bytes())
        existing[key] = path
    prefab = destination / 'NpcDetailAchievementContent.prefab'
    keys = {yaml.safe_load(value) for value in re.findall(r'(?m)^  myKey: (.+)$', prefab.read_text('utf8'))} - {'', None}
    keys.update(('ui_achievement_title_NpcFriendshipLevel', 'ui_achievement_content_NpcFriendshipLevel',
        'ui_achievement_title_StarGodNpcFriendshipLevel', 'ui_achievement_content_StarGodNpcFriendshipLevel',
        'AchievementPanel.CanNotGetReward', 'AchievementPanel.AlreadyGotReward', 'ui_ItemIcon_count_key'))
    npc_ids = {int(re.search(r'NpcClassId\s*:\s*(\d+)', row['GroupParams'])[1]) for row in achievements}
    keys.update(row['NameKey'] for row in rules['tables']['npc'] if row['Id'] in npc_ids)
    texts, sources = language(keys)
    literal = keys - texts.keys()
    if literal != {'{0}', '{0}/{1}'}: raise ValueError('Original achievement language absent: ' + repr(literal))
    fallback = ROOT / '.tools/source-packages/current' / apk.version / 'official-code/framework-firstpass-source/Framework/LocalizationTextService.cs'
    from prepare_partner_gift_configuration import method
    fallback_body = method(fallback.read_text('utf-8-sig'), 'public static string GetMissingKeyText(string key, bool isShowMissingKeyText)')
    if 'return key;' not in fallback_body: raise ValueError('Original literal language fallback changed')
    binding.inputs[fallback.relative_to(ROOT).as_posix()] = sha(fallback.read_bytes())
    language_records = {row['name']: row for row in apk.source.manifest('WwiseBnk', apk.archive)['bundles']}
    for relative, expected in sources.items():
        raw = (ROOT / relative).read_bytes(); row = language_records[Path(relative).name]
        if sha(raw) != expected or len(raw) != row['size'] or hashlib.md5(raw).hexdigest() != row['hash']:
            raise ValueError('Original achievement language differs: ' + relative)
        binding.inputs[relative] = expected
    text_path = destination / 'partner-achievement-texts.json'
    write(text_path, (json.dumps(dict(manifest=apk.version, texts=[dict(key=k, text=v) for k, v in sorted(texts.items())]), ensure_ascii=False, indent=2) + '\n').encode())
    write(Path(str(text_path) + '.meta'), ('fileFormatVersion: 2\nguid: ' + hashlib.md5(b'zjcs:partner-achievement-texts').hexdigest() + '\nTextScriptImporter:\n  externalObjects: {}\n').encode())

    def body(name, script):
        return dict(m_ObjectHideFlags=0, m_CorrespondingSourceObject=dict(fileID=0), m_PrefabInstance=dict(fileID=0),
            m_PrefabAsset=dict(fileID=0), m_GameObject=dict(fileID=0), m_Enabled=1, m_EditorHideFlags=0,
            m_Script=dict(fileID=11500000, guid=guid(metadata(script)), type=3), m_Name=name, m_EditorClassIdentifier='')

    images = ROOT / 'unity/Assets/Game/Resources/Partners/OfficialPartnerAchievementImages.asset'
    content = body('OfficialPartnerAchievementImages', ROOT / 'unity/Assets/Game/Runtime/Offline/ProfessionUI/OfflineProfessionImageCatalog.cs')
    content['Entries'] = [dict(Path=row['address'], AssetGuid=row['manifest_asset']['guid'], Asset=row['pointer']) for row in sprites]
    write(images, emit({11400000: dict(MonoBehaviour=content)}))
    image_meta = metadata(images, 'NativeFormatImporter'); installed[image_meta.relative_to(ROOT).as_posix()] = sha(image_meta.read_bytes())
    gift_path = ROOT / 'assets/reference/partner-gift-controller-source-manifest.json'; gift = json.loads(gift_path.read_bytes())
    binding.inputs[gift_path.relative_to(ROOT).as_posix()] = sha(gift_path.read_bytes())
    script = ROOT / 'unity/Assets/Game/Runtime/Offline/PartnersUI/OfficialPartnerAchievementAssetCatalog.cs'
    common = ROOT / 'unity/Assets/Game/Resources/Partners/OfficialPartnerGiftPageAssetCatalog.asset'
    content = body('OfficialPartnerAchievementAssetCatalog', script)
    content.update(panelPrefab=native['roots']['NpcDetailAchievementContent'], rewardIconPrefab=gift['roots']['ItemIconDefault'],
        texts=dict(fileID=4900000, guid=guid(Path(str(text_path) + '.meta')), type=3),
        images=dict(fileID=11400000, guid=guid(image_meta), type=2), common=dict(fileID=11400000, guid=guid(Path(str(common) + '.meta')), type=2))
    catalog = ROOT / 'unity/Assets/Game/Resources/Partners/OfficialPartnerAchievementAssetCatalog.asset'
    write(catalog, emit({11400000: dict(MonoBehaviour=content)}))
    catalog_meta = metadata(catalog, 'NativeFormatImporter'); installed[catalog_meta.relative_to(ROOT).as_posix()] = sha(catalog_meta.read_bytes())
    code_folder = ROOT / 'unity/Assets/Game/Runtime/Official/PartnersUI/Roster/Achievements'
    for path in [*code_folder.glob('*.cs'), ROOT / 'unity/Assets/Game/Runtime/Offline/PartnersUI/OfflinePartnerAchievementUiServices.cs', script]:
        for entry in (path, metadata(path)): installed[entry.relative_to(ROOT).as_posix()] = sha(entry.read_bytes())
    for folder in [destination, code_folder, *[path for path in destination.rglob('*') if path.is_dir()]]:
        meta = metadata(folder, 'DefaultImporter', True); installed[meta.relative_to(ROOT).as_posix()] = sha(meta.read_bytes())
    # Unity can create these four new metadata files during the first import.
    # Canonicalize only this module's new files; preserve all shared metadata.
    for path in (Path(str(code_folder) + '.meta'), code_folder / 'IPartnerAchievementUiServices.cs.meta',
            ROOT / 'unity/Assets/Game/Runtime/Offline/PartnersUI/OfflinePartnerAchievementUiServices.cs.meta',
            Path(str(script) + '.meta')):
        raw = ('\n'.join(line.rstrip() for line in path.read_text('utf8').splitlines()) + '\n').encode()
        if path.read_bytes() != raw: path.write_bytes(raw)
        installed[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
    proof = dict(manifest=apk.version, generator_sha256=sha(Path(__file__).read_bytes()), inputs=binding.inputs,
        outputs=installed, reused=reused, canonical=canonical, sprites=sprites, objects=binding.proof, language_keys=sorted(keys), literal_keys=sorted(literal),
        native=native, controllers=json.loads((work / 'partner-achievement-ui-source-manifest.json').read_bytes()),
        scope='Original detail achievement region, original reward sprites and local claim UI. Formal detail-window host and Windows Player remain pending.')
    proof['validation_inputs'] = {relative: sha((ROOT / relative).read_bytes()) for relative in (
        'scripts/prepare_partner_achievement_ui_validation.py', 'scripts/fixtures/partner_achievement_ui_validation.cs')}
    proof_path.write_text(json.dumps(proof, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    print('Installed exact achievement region and original reward sprites:', len(installed), 'paths,', len(sprites), 'reward images')


if __name__ == '__main__': main()
