"""Record the selected local friendship/property dialog data bindings."""
import json
import hashlib
import re
from pathlib import Path

from prepare_partner_gift_configuration import method, sha

ROOT = Path(__file__).resolve().parents[1]


def install_catalog():
    proof_path = ROOT / 'assets/reference/partner-friendship-services-source-manifest.json'
    previous = json.loads(proof_path.read_bytes()).get('outputs', {}) if proof_path.is_file() else {}
    script = 'unity/Assets/Game/Runtime/Offline/PartnersUI/OfficialPartnerFriendshipAssetCatalog.cs'
    references = {
        'propertyConfiguration': ('unity/Assets/Official/Partners/partner-property-ui.json', 4900000, 3),
        'propertyTexts': ('unity/Assets/Official/Partners/partner-property-texts.json', 4900000, 3),
        'dialogTexts': ('unity/Assets/Official/Partners/UI/FriendshipDialog/partner-friendship-dialog-texts.json', 4900000, 3),
        'dialogPrefab': ('unity/Assets/Official/Partners/UI/FriendshipDialog/NpcFriendshipDialog.prefab', 1709254077376921, 3),
    }

    def guid(relative):
        return re.search(r'^guid: ([a-f0-9]{32})$', (ROOT / (relative + '.meta')).read_text(), re.M)[1]

    asset = 'unity/Assets/Game/Resources/Partners/OfficialPartnerFriendshipAssetCatalog.asset'
    text = ('%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\nMonoBehaviour:\n'
        '  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n'
        '  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: 0}\n'
        '  m_Enabled: 1\n  m_EditorHideFlags: 0\n'
        f'  m_Script: {{fileID: 11500000, guid: {guid(script)}, type: 3}}\n'
        '  m_Name: OfficialPartnerFriendshipAssetCatalog\n  m_EditorClassIdentifier:\n')
    for field, (relative, file_id, kind) in references.items():
        text += f'  {field}: {{fileID: {file_id}, guid: {guid(relative)}, type: {kind}}}\n'
    asset_guid = hashlib.md5(b'zjcs:partner-friendship-resources-catalog').hexdigest()
    outputs = {asset: text.encode(), asset + '.meta': (
        f'fileFormatVersion: 2\nguid: {asset_guid}\nNativeFormatImporter:\n'
        '  externalObjects: {}\n  mainObjectFileID: 11400000\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n').encode()}
    for relative, payload in outputs.items():
        path = ROOT / relative
        if path.is_file() and path.read_bytes() != payload and sha(path.read_bytes()) != previous.get(relative):
            raise ValueError('Refusing to replace unrecorded friendship catalog: ' + relative)
    for relative, payload in outputs.items():
        (ROOT / relative).write_bytes(payload)
    return [asset, asset + '.meta'], [value[0] for value in references.values()]


def main():
    version = json.loads((ROOT / 'official-source.json').read_bytes())['active']
    if version != '884_156692':
        raise ValueError('Review local friendship services after changing the selected version')
    catalog_outputs, catalog_inputs = install_catalog()
    inputs, methods = {}, {}
    for relative in catalog_inputs:
        for suffix in ('', '.meta'):
            inputs[relative + suffix] = sha((ROOT / (relative + suffix)).read_bytes())
    declarations = {
        'client-source/NpcFriendshipDialog.cs': ['private void Refresh()'],
        'common-source/Common/NpcInfoParser.cs': ['public Dictionary<PropType, long> GetFriendshipLevelPropDiff(NpcInfo npcInfo, int fromLevel, int toLevel)'],
        'common-source/Common/NpcFriendshipLevelCondition.cs': [],
        'common-source/Common/PlayerTaskDataUtils.cs': [],
    }
    for relative, signatures in declarations.items():
        path = ROOT / '.tools/source-packages/official-code' / relative
        raw = path.read_bytes()
        if raw != (ROOT / '.tools/source-packages/current' / version / 'official-code' / relative).read_bytes():
            raise ValueError('Selected friendship source differs: ' + relative)
        inputs[path.relative_to(ROOT).as_posix()] = sha(raw)
        text = raw.decode('utf-8-sig').replace('\r\n', '\n')
        methods[relative] = {signature: sha(method(text, signature).encode()) for signature in signatures}
    for relative in ('assets/reference/partner-properties-source-manifest.json',
            'assets/reference/partner-property-ui-source-manifest.json',
            'assets/reference/partner-reward-comparer-source-manifest.json',
            'unity/Assets/Official/Partners/partner-rules.json',
            'unity/Assets/Game/Runtime/Offline/Progression/OfficialPartnerAssetCatalog.cs',
            'unity/Assets/Game/Runtime/Offline/PartnersUI/OfflinePartnerGiftConfiguration.cs',
            'unity/Assets/Game/Runtime/Offline/ProfessionUI/OfflineProfessionPropertyBindings.cs'):
        inputs[relative] = sha((ROOT / relative).read_bytes())
    outputs = {}
    for name in ('OfflinePartnerFriendshipProperties', 'OfflinePartnerFriendshipDialogServices', 'OfficialPartnerFriendshipAssetCatalog'):
        for suffix in ('.cs', '.cs.meta'):
            path = ROOT / 'unity/Assets/Game/Runtime/Offline/PartnersUI' / (name + suffix)
            outputs[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
    assembly = 'unity/Assets/Game/Runtime/Offline/PartnersUI/Zjcs.Offline.PartnersUI.asmdef'
    outputs[assembly] = sha((ROOT / assembly).read_bytes())
    outputs.update({relative: sha((ROOT / relative).read_bytes()) for relative in catalog_outputs})
    validations = ('scripts/prepare_partner_friendship_current_validation.py',
        'scripts/fixtures/partner_friendship_dialog_validation.cs',
        'scripts/fixtures/partner_gift_preferences_validation.cs')
    proof = dict(manifest=version, generator_sha256=sha(Path(__file__).read_bytes()), source_sha256=inputs,
        methods=methods, outputs=outputs,
        validation_source_sha256={relative: sha((ROOT / relative).read_bytes()) for relative in validations},
        adaptations=['Read-only typed current-table projections and live player rank/travel function ports.',
            'Existing selected LevelPropParser/NpcInfoParser math supplies all property rows and differences.',
            'The original CanMeetCondition friendship branch controls the assist hint, rather than AssistCondition.',
            'The original RewardPreviewItemComparer instance supplies ItemComparerBase.RewardItemComparer.',
            'Explicit existing property/resource/localization bindings and native icon-pool delegates; no invented values.'],
        scope='Local provider and native dialog integration; production detail-window routing and popup contents require separate validation.')
    (ROOT / 'assets/reference/partner-friendship-services-source-manifest.json').write_text(
        json.dumps(proof, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    print('Recorded selected friendship/property dialog service bindings.')


if __name__ == '__main__':
    main()
