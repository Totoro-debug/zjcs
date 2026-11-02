"""Record selected official health formulas and the explicit offline binding sources."""
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def main():
    official = ROOT / '.tools/source-packages/current/884_156692/official-code'
    inputs = [official / 'common-source/Common' / (name + '.cs') for name in
              ('Player', 'PlayerDataWrap', 'Formula', 'FormulaHandler', 'TimeManager', 'BattleFormulaHandler')]
    inputs.append(official / 'client-source/PlayerIconMeHpSliderNormal.cs')
    runtime = ROOT / 'unity/Assets/Game/Runtime'
    outputs = [runtime / name for name in (
        'Official/PlayerHealth/PlayerHealthRules.cs',
        'Official/PlayerHealth/Zjcs.Official.PlayerHealth.asmdef',
        'Offline/Progression/OfflinePlayerHealth.cs',
        'Offline/Progression/OfflineMainlineProgressSession.cs',
        'Offline/Progression/Zjcs.Offline.Progression.asmdef',
        'Offline/PartnersUI/OfflinePartnerPlayerHpBindings.cs')]
    for path in list(outputs):
        if path.name not in ('OfflineMainlineProgressSession.cs', 'Zjcs.Offline.Progression.asmdef'):
            outputs.append(Path(str(path) + '.meta'))
    outputs.extend((runtime / 'Official/PlayerHealth.meta',))
    editor = ROOT / 'unity/Assets/Game/Editor/PlayerHealth'
    outputs.extend(path for path in editor.iterdir() if path.is_file())
    outputs.append(Path(str(editor) + '.meta'))
    validation = [Path(__file__), ROOT / 'scripts/prepare_partner_gift_validation.py',
                  ROOT / 'scripts/prepare_partner_player_health_validation.py']
    dependencies = [runtime / 'Offline/Progression/CharacterPropCalculator.cs',
        runtime / 'Offline/Progression/OfficialPropCatalog.cs',
        runtime / 'Offline/Progression/OfflineMainlineProgressSession.cs.meta',
        runtime / 'Offline/Progression/Zjcs.Offline.Progression.asmdef.meta',
        runtime / 'Official/TeamUI/TeamPlayerUIBase.cs',
        runtime / 'Official/TeamUI/TeamPlayerIconMeHpSliderNormal.cs',
        ROOT / 'unity/Assets/Official/Partners/UI/Panel/NpcPanel.prefab']

    def hashes(paths):
        return {path.relative_to(ROOT).as_posix(): hashlib.sha256(path.read_bytes()).hexdigest()
                for path in sorted(paths)}

    proof = dict(manifest='884_156692',
        official_source_sha256=hashes(inputs), binding_source_sha256=hashes(outputs),
        validation_source_sha256=hashes(validation), dependency_source_sha256=hashes(dependencies),
        adaptation=[
            'Original health getter, recovery-speed expression, nonnegative elapsed seconds and current-games branch; manager/config inputs are explicit.',
            'Current-games membership is Common.Player.IsInGame (CurrentGames.Count > 0); each supplied flag is the original StageInfo.UsePlayerHp.',
            'Property values use the existing original PropCfgInfo.Float fixed-point conversion; the character constructor supplies live NonBattleProps.',
            'Confirmed offline missing-field/new-character initialization is full HP; existing zero HP remains zero.',
            'Local value and UTC update ticks share the existing profile transaction; publish original HP event only after successful persistence.',
            'The isolated original prefab widget check does not establish full player-info, transcript, live map/battle or Windows Player integration.'
        ])
    target = ROOT / 'assets/reference/partner-player-health-source-manifest.json'
    target.write_text(json.dumps(proof, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    print('Recorded selected official player health formulas and local persistence/widget bindings')


if __name__ == '__main__':
    main()
