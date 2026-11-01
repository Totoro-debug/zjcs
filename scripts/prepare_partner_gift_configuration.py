"""Bind selected gift/level/tag parsers and the original CN custom-content whitelist."""
import hashlib
import json
from pathlib import Path
import re

from official_apk import ROOT
from official_manifest import read_asset_manifest
from prepare_current_partner_native import PartnerNativeRecovery, identity
from validation_workspace import validation_directory

DEST = Path('unity/Assets/Game/Runtime/Offline/PartnersUI')


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def method(text, signature):
    found = re.findall(r'\t' + re.escape(signature) + r'\n\t\{.*?\n\t\}', text, re.S)
    if len(found) != 1:
        raise ValueError('Review original method: ' + signature)
    return found[0]


def without_logging(text, expected):
    text, count = re.subn(r'\t+(?:GLoggerErrorInterpolatedStringHandler )?message = new GLoggerErrorInterpolatedStringHandler\([^\n]*\);\n.*?\n\t+Log.Error\([^\n]*\);\n', '', text, flags=re.S)
    text = text.replace('\t\tGLoggerErrorInterpolatedStringHandler message;\n', '').replace('\t\tbool enabled;\n', '')
    if count != expected or any(value in text for value in ('GLogger', 'Log.Error', 'AppendLiteral', 'enabled')):
        raise ValueError('Unadapted logging boundary')
    return text


def build(work):
    recovery = PartnerNativeRecovery()
    apk = recovery.apk
    inputs, outputs, methods = {}, {}, {}
    stage = work / 'gift-configuration'
    stage.mkdir(exist_ok=True)

    def source(relative):
        path = ROOT / '.tools/source-packages/official-code' / relative
        raw = path.read_bytes()
        if raw != (ROOT / '.tools/source-packages/current' / apk.version / 'official-code' / relative).read_bytes():
            raise ValueError('Current gift parser source differs: ' + relative)
        inputs[path.relative_to(ROOT).as_posix()] = sha(raw)
        return raw.decode('utf-8-sig').replace('\r\n', '\n')

    def selected(relative, signatures):
        text = source(relative)
        bodies = [method(text, signature) for signature in signatures]
        methods[relative] = {signature: sha(body.encode()) for signature, body in zip(signatures, bodies)}
        return '\n\n'.join(bodies)

    template = ROOT / 'scripts/templates/partner_gift_controller/OfflinePartnerGiftConfiguration.cs'
    inputs[template.relative_to(ROOT).as_posix()] = sha(template.read_bytes())
    text = template.read_text('utf8')
    levels = selected('common-source/Common/NpcFriendshipLevelInfoParser.cs', [
        'public override void PostParse()', 'public int GetMaxLevel(int id)',
        'public int GetMaxSubRankLevel(int id, SubRank subRank)',
        'public List<int> GetSubRankLevelList(int id, SubRank subRank)',
        'public int GetFriendshipLevelByExp(int id, long friendshipExp, SubRank subRank)'])
    levels = without_logging(levels, 3).replace('((ParserBase)this).PostParse();', '').replace('public override void', 'public void')
    levels = levels.replace('Parsers.Instance.NpcInfoParser.Get(id)', 'npcs.Get(id)')
    levels = levels.replace('NpcFriendshipLevelInfo', 'LevelRecord').replace('NpcInfo npcInfo', 'NpcRecord npcInfo')
    levels = re.sub(r'\bSubRank\b', 'int', levels.replace('SubRank.None', '0'))
    text = text.replace('/*LEVEL_METHODS*/', levels)

    gift = selected('common-source/Common/NpcGiftInfoParser.cs', ['public int GetAllExpByDict(Dictionary<int, long> giftDict, Dictionary<int, double> giftPreferenceDict)'])
    gift = without_logging(gift, 1).replace('Parsers.GameSettings.NpcGiftDefaultPreferenceFactor', 'defaultPreference')
    gift = gift.replace('ItemInfoParser itemInfoParser = Parsers.Instance.ItemInfoParser;', 'var itemInfoParser = items;')
    gift = gift.replace('NpcUnlockItemInfoParser npcUnlockItemInfoParser = Parsers.Instance.NpcUnlockItemInfoParser;', 'var npcUnlockItemInfoParser = unlocks;')
    gift = gift.replace('ItemInfo itemInfo', 'ItemRecord itemInfo').replace('NpcUnlockItemInfo npcUnlockItemInfo', 'UnlockRecord npcUnlockItemInfo')
    gift = gift.replace('NpcGiftInfo npcGiftInfo', 'PartnerGiftInfo npcGiftInfo').replace('ItemType.NpcUnlockItem', '433')
    text = text.replace('/*GIFT_METHOD*/', gift)

    tags = selected('common-source/Common/NpcFriendshipTagInfoParser.cs', [
        'protected override void PostParse(Dictionary<int, NpcFriendshipTagInfo> configs)',
        'private List<NpcFriendshipTagInfo> GetTagInfoByRoleType(NpcRoleType roleType)',
        'public NpcFriendshipTagInfo GetTagInfoByRoleTypeAndLevel(NpcRoleType roleType, int level)'])
    tags = tags.replace('base.PostParse(configs);', '').replace('protected override void', 'private void')
    tags = tags.replace('NpcFriendshipTagInfo', 'TagRecord')
    tags = re.sub(r'\bNpcRoleType\b', 'int', tags)
    text = text.replace('/*TAG_METHODS*/', tags)
    text = text.replace('/*TAG_RANGE*/', selected('common-source/Common/NpcFriendshipTagInfo.cs', ['public bool InRangeLevel(int level)']))
    characters = selected('client-source/FontCharacterValidChecker.cs', ['public bool IsAllCharacterValid(string content)'])
    characters = characters.replace('GetAndTryRefreshCharactersSet()', 'validCharacters')
    text = text.replace('/*CHARACTER_CHECK*/', characters)
    legality = selected('common-source/Common/FormulaHandler.cs', ['public int CheckNpcFriendshipTagLegal(string name)'])
    legality = legality.replace('Parsers.GameSettings.NpcFriendshipTagSettings.CustomTagMaxLength', 'customTagMaxLength')
    text = text.replace('/*TAG_CHECK*/', legality)
    lengths = selected('common-source/Common/FormulaHandler.cs', ['public bool IsOverCharacterCountLimit(string str, int limitCount)', 'public int GetCharacterCount(string str)'])
    # selected() records each source once; merge the name-check receipt as well.
    methods['common-source/Common/FormulaHandler.cs']['public int CheckNpcFriendshipTagLegal(string name)'] = sha(method(source('common-source/Common/FormulaHandler.cs'), 'public int CheckNpcFriendshipTagLegal(string name)').encode())
    text = text.replace('/*CHARACTER_LENGTH*/', lengths.replace('CJK.GetByteCount', 'Common.CJK.GetByteCount'))
    cjk = source('common-source/Common/CJK.cs')
    installed = ROOT / 'unity/Assets/Game/Runtime/Official/SkillRules/CJK.cs'
    existing = installed.read_text('utf-8-sig').replace('\r\n', '\n')
    if existing.replace('namespace Common\n{\n', 'namespace Common;\n').rstrip().removesuffix('}').rstrip() != cjk.rstrip():
        raise ValueError('Existing CJK implementation is not the selected original')
    inputs[installed.relative_to(ROOT).as_posix()] = sha(installed.read_bytes())
    if '/*' in text or 'Parsers.' in text or 'GLogger' in text:
        raise ValueError('Unbound gift configuration template')
    output = normalize('// Official parser bodies; see partner-gift-configuration-source-manifest.json.\n' + text)
    (stage / 'OfflinePartnerGiftConfiguration.cs').write_text(output, 'utf8', newline='\n')

    source('common-source/CustomContentSettings.cs')
    item_types = source('common-source/Common/ItemType.cs')
    npc_types = source('common-source/Common/NpcRoleType.cs')
    if not all(re.search(pattern, value) for pattern, value in ((r'NpcGift = 328,', item_types),
            (r'NpcUnlockItem = 433,', item_types), (r'StarGod = 2,', npc_types))):
        raise ValueError('Review selected NPC/item enum identities')
    source('common-source/Common/NpcInfo.cs')
    language = source('framework-firstpass-source/Framework/LanguageType.cs')
    if not re.search(r'public enum LanguageType\n\{\n\tzh_CN,', language):
        raise ValueError('Review official CN language enum identity')
    manifest_bytes = apk.source.manifest_bytes('DefaultPackage', apk.archive)
    records = read_asset_manifest(manifest_bytes)
    settings_row, = [row for row in records if row['path'].endswith('/Modules/Text/CustomContentSettings.asset')]
    obj, = [pointer.deref() for name, pointer in apk.container(settings_row['bundle']) if name.casefold() == settings_row['path'].casefold()]
    fields = recovery.fields(obj)
    matching = [row for row in fields['mLanguageCharactersAssetList'] if row['LanguageType'] == 0]
    character_guid = matching[0]['CharactersAsset']['mAssetGuid'] if matching else fields['mDefaultCharactersAsset']['mAssetGuid']
    character_row, = [row for row in records if row['guid'] == character_guid]
    character_obj, = [pointer.deref() for name, pointer in apk.container(character_row['bundle']) if name.casefold() == character_row['path'].casefold()]
    if character_obj.type.name != 'TextAsset':
        raise ValueError('Custom-content whitelist is not a TextAsset')
    raw = character_obj.read().m_Script.encode('utf8', 'surrogateescape')
    (stage / 'partner-custom-characters.txt').write_bytes(raw)
    for relative in ('scripts/official_apk.py', 'scripts/official_manifest.py', 'scripts/prepare_current_partner_native.py',
            'scripts/prepare_partner_ui_helpers.py', 'unity/Assets/Official/Partners/partner-rules.json',
            'unity/Assets/Official/Partners/partner-properties.json', 'unity/Assets/Game/Resources/Partners/OfficialPartnerAssetCatalog.asset'):
        inputs[relative] = sha((ROOT / relative).read_bytes())
    receipt = dict(manifest=apk.version, generator_sha256=sha(Path(__file__).read_bytes()), source_sha256=inputs, methods=methods,
        asset_manifest_sha256=sha(manifest_bytes), settings=dict(record=settings_row, source=apk.provenance(obj), identity=identity(obj),
            original_sha256=sha(obj.get_raw_data()), fields=fields),
        characters=dict(language='zh_CN', language_type=0, guid=character_guid, record=character_row, source=apk.provenance(character_obj),
            identity=identity(character_obj), original_sha256=sha(character_obj.get_raw_data()), sha256=sha(raw), bytes=len(raw)),
        validation_source_sha256={name: sha((ROOT / name).read_bytes()) for name in (
            'scripts/prepare_partner_gift_configuration_validation.py', 'scripts/fixtures/partner_gift_configuration_validation.cs')},
        adaptations=['Typed projections and indexed table bindings; original iteration order retained.',
            'Original diagnostic logging omitted at absent-configuration branches; return values retained.',
            'CN original CustomContentSettings whitelist is explicitly packaged; no remote font lookup.',
            'Reuse byte-equivalent selected CJK implementation in Official.SkillRules.'], outputs=outputs)
    (work / 'partner-gift-configuration-source-manifest.json').write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    return receipt


def normalize(text):
    return '\n'.join(line.expandtabs(4).rstrip() for line in text.splitlines()) + '\n'


def install(work, receipt):
    from prepare_partner_ui_helpers import meta
    stage = work / 'gift-configuration'
    proof = ROOT / 'assets/reference/partner-gift-configuration-source-manifest.json'
    previous = json.loads(proof.read_bytes())['outputs'] if proof.is_file() else {}
    pairs = [(stage / 'OfflinePartnerGiftConfiguration.cs', ROOT / DEST / 'OfflinePartnerGiftConfiguration.cs'),
             (stage / 'partner-custom-characters.txt', ROOT / 'unity/Assets/Official/Partners/partner-custom-characters.txt')]
    for source, target in pairs:
        if target.exists() and target.read_bytes() != source.read_bytes() and previous.get(target.relative_to(ROOT).as_posix()) != sha(target.read_bytes()):
            raise ValueError('Review existing configuration output before replacing: ' + str(target))
        target.write_bytes(source.read_bytes())
        if target.suffix == '.txt':
            guid = hashlib.md5(('zjcs:partner-custom-characters:' + receipt['characters']['identity']).encode()).hexdigest()
            Path(str(target) + '.meta').write_text('fileFormatVersion: 2\nguid: ' + guid + '\nTextScriptImporter:\n  externalObjects: {}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n', 'utf8', newline='\n')
        else:
            meta(target)
    script = ROOT / DEST / 'OfficialPartnerGiftAssetCatalog.cs'
    meta(script)
    def guid(path):
        return re.search(rb'(?m)^guid: ([0-9a-f]{32})', Path(str(path) + '.meta').read_bytes())[1].decode()
    binding = ROOT / 'unity/Assets/Game/Resources/Partners/OfficialPartnerGiftAssetCatalog.asset'
    data = ('%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\nMonoBehaviour:\n'
        '  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n'
        '  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: 0}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n'
        '  m_Script: {fileID: 11500000, guid: ' + guid(script) + ', type: 3}\n'
        '  m_Name: OfficialPartnerGiftAssetCatalog\n  m_EditorClassIdentifier:\n'
        '  configuration: {fileID: 11400000, guid: ' + guid(ROOT / 'unity/Assets/Game/Resources/Partners/OfficialPartnerAssetCatalog.asset') + ', type: 2}\n'
        '  customCharacters: {fileID: 4900000, guid: ' + guid(pairs[1][1]) + ', type: 3}\n').encode()
    if binding.exists() and binding.read_bytes() != data:
        raise ValueError('Existing gift configuration binding differs')
    binding.write_bytes(data)
    Path(str(binding) + '.meta').write_text('fileFormatVersion: 2\nguid: ' + hashlib.md5(b'zjcs:partner-gift-configuration-binding').hexdigest() +
        '\nNativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 11400000\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n', 'utf8', newline='\n')
    installed = [target for _, target in pairs] + [script, binding]
    for target in installed:
        path = Path(str(target) + '.meta')
        path.write_bytes(path.read_bytes().replace(b'\r\n', b'\n'))
        for entry in (target, path):
            receipt['outputs'][entry.relative_to(ROOT).as_posix()] = sha(entry.read_bytes())
    assembly = ROOT / DEST / 'Zjcs.Offline.PartnersUI.asmdef'
    receipt['outputs'][assembly.relative_to(ROOT).as_posix()] = sha(assembly.read_bytes())
    proof.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')


if __name__ == '__main__':
    import argparse
    parser = argparse.ArgumentParser(__doc__)
    parser.add_argument('--install', action='store_true')
    args = parser.parse_args()
    work = validation_directory()
    receipt = build(work)
    if args.install:
        install(work, receipt)
    print('Selected original gift/level/tag methods and CN character whitelist recovered.')
