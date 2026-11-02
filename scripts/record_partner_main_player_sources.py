"""Record the selected official PlayerInfoData(Player) and local projection inputs."""
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def hashes(paths):
    return {path: hashlib.sha256((ROOT / path).read_bytes()).hexdigest() for path in sorted(paths)}


def main():
    previous = json.loads((ROOT / 'assets/reference/partner-player-info-source-manifest.json').read_bytes())
    official = {key: value for key, value in previous['official_source_sha256'].items()
                if not key.endswith(('PlayerInfoUI.cs', 'NpcTargetData.cs'))}
    for path, expected in official.items():
        if hashes([path])[path] != expected:
            raise ValueError('Selected official input changed: ' + path)
    base = '.tools/source-packages/current/884_156692/official-code/'
    official.update(hashes([base + 'client-source/PlayerInfoData.cs',
        base + 'common-source/Common/PlayerTargetData.cs', base + 'common-source/Common/PlayerPetExt.cs',
        base + 'common-source/Common/Player.cs']))
    data = dict(manifest='884_156692', official_source_sha256=official,
        binding_source_sha256=hashes([
            'unity/Assets/Game/Runtime/Offline/PartnersUI/OfflinePartnerPlayerInfoSource.cs',
            'unity/Assets/Game/Runtime/Offline/PartnersUI/OfflinePartnerPlayerInfoSource.cs.meta']),
        validation_source_sha256=hashes(['scripts/prepare_partner_main_player_validation.py',
            'scripts/fixtures/partner_main_player_validation.cs', 'scripts/record_partner_main_player_sources.py']),
        dependency_source_sha256=hashes([
            'unity/Assets/Game/Runtime/Offline/Progression/OfflineMainlineProgressSession.cs',
            'unity/Assets/Game/Runtime/Offline/Progression/OfflineLocalActorIdentities.cs',
            'unity/Assets/Game/Runtime/Offline/Progression/OfflineCharacterProgressionService.cs',
            'unity/Assets/Game/Runtime/Offline/Progression/OfflineCharacterProgressionService.Partners.cs',
            'unity/Assets/Game/Runtime/Offline/Progression/OfflinePetProgress.cs',
            'unity/Assets/Game/Runtime/Offline/ProfessionUI/OfflineProfessionAppearanceBindings.cs',
            'unity/Assets/Game/Runtime/Official/ProfessionUI/PlayerInfoData.cs',
            'unity/Assets/Game/Runtime/Official/ProfessionUI/ProfessionPlayerInfoBindings.cs',
            'unity/Assets/Game/Runtime/Official/ProfessionUI/SafeAction.cs',
            'unity/Assets/Game/Runtime/Official/World/NativeLabelUI/Framework/CultureTextArg.cs']),
        operation=[
            'Port the selected PlayerInfoData(Player) field projection onto the actual selected local profile, configured property calculator and original appearance protocol adapter.',
            'Player target preserves inherited ServerId=-1; figure and assistance calls require explicit host bindings.',
            'EngagePetClassId reads FollowPetIndex, independently of first skill-plan pet used by PlayerBriefInfo.',
            'Local committed profile replacement publishes a complete new info generation through the original SafeAction manager boundary; no guessed server field-event order.',
            'This receipt covers data projection; full original UI appearance, dialogue host and Windows delivery remain separate unfinished work.'])
    folder = 'unity/Assets/Official/Combat/SceneCharacterAppearance/Resources/Combat/SceneAppearance/'
    data['dependency_source_sha256'].update(hashes([folder + name for name in (
        'AppearanceConfig.asset', 'AppearanceAnimNameCombineRule.asset', 'appearance-tables.json', 'skin-defaults.json')]))
    path = ROOT / 'assets/reference/partner-main-player-source-manifest.json'
    path.write_text(json.dumps(data, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    print('Recorded selected official and main-player projection sources:', path)


if __name__ == '__main__':
    main()
