"""Record original profile migration and the confirmed offline claim boundary."""
import hashlib
import json
from pathlib import Path

from official_apk import ROOT
from prepare_partner_input_dialog import metadata


def sha(path): return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    version = json.loads((ROOT / 'official-source.json').read_bytes())['active']
    if version != '884_156692': raise ValueError('Review selected profile source version')
    inputs = ['official-source.json', 'assets/reference/partner-profile-native-source-manifest.json',
        'assets/reference/partner-profile-assets-source-manifest.json',
        'unity/Assets/Official/Partners/partner-rules.json',
        'unity/Assets/Game/Runtime/Offline/Progression/OfflineMainlineProgressSession.cs',
        'unity/Assets/Game/Runtime/Offline/Progression/OfflineInventoryOperations.cs',
        'unity/Assets/Game/Runtime/Offline/State/LocalJsonSaveFile.cs']
    inputs += ['.tools/source-packages/current/' + version + '/official-code/' + relative for relative in (
        'client-source/NpcDetailPanelProfileContent.cs', 'common-source/MsgPlayerNpc.cs',
        'common-source/Common/PlayerNpcDataWrap.cs', 'common-source/Common/PlayerNpcReceivedProfileRewardsWrap.cs')]
    outputs = {}
    for relative in ('Progression/OfflinePartnerProfileService.cs', 'Progression/OfflineCharacterPropertyProgress.cs',
            'PartnersUI/OfflinePartnerProfileUiServices.cs', 'PartnersUI/OfficialPartnerProfileAssetCatalog.cs',
            'PartnersUI/OfflinePartnerGiftRuntimeBindings.cs'):
        source = ROOT / 'unity/Assets/Game/Runtime/Offline' / relative
        meta = metadata(source)
        if source.name not in ('OfflineCharacterPropertyProgress.cs', 'OfflinePartnerGiftRuntimeBindings.cs'):
            meta.write_text('\n'.join(line.rstrip() for line in meta.read_text('utf8').splitlines()) + '\n', 'utf8', newline='\n')
        for path in (source, meta): outputs[path.relative_to(ROOT).as_posix()] = sha(path)
    helpers = ['scripts/' + name for name in ('prepare_partner_profile_ui.py', 'prepare_partner_profile_native.py',
        'prepare_partner_profile_assets.py', 'prepare_partner_profile_validation.py',
        'fixtures/partner_profile_transaction_validation.cs', 'fixtures/partner_profile_ui_validation.cs')]
    proof = dict(manifest=version, inputs={relative: sha(ROOT / relative) for relative in inputs}, outputs=outputs,
        recorder_sha256=sha(Path(__file__)), validation_helpers={relative: sha(ROOT / relative) for relative in helpers},
        confirmed_offline_decision='On unlock click reload the current save, require a met NPC and original chapter friendship condition, grant configured rewards and chapter receipt atomically; failed save stays locked, retries and revisits never grant twice.',
        operation=['Preserve original chapter order, three/five-tab choice, eligible-tab preference, one-frame scroll and all lock/content/reward states.',
            'Use the selected original native profile subtree, reward ItemIcon prefab, language and configured reward sprites; preserve original geometry and animation.',
            'Project receipts and friendship from the current Save.Player after every transaction or reload; return detached receipt reads.',
            'Missing legacy receipt fields normalize to the original empty collection; validate positive unique chapter IDs.',
            'Perform checked inventory grants on the transaction clone and publish success only after the atomic save; catch disk/access failures at the profile boundary.',
            'Closed collaboration NPCs remain unavailable. Formal Home detail host, production tip graph and Windows Player remain pending.'])
    target = ROOT / 'assets/reference/partner-profile-source-manifest.json'
    target.write_text(json.dumps(proof, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    print('Recorded original profile migration and confirmed atomic claim boundary')


if __name__ == '__main__': main()
