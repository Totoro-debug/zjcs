"""Record original auto-partner data and the confirmed offline trigger bindings."""
import json
from pathlib import Path

from prepare_partner_gift_configuration import method, sha

ROOT = Path(__file__).resolve().parents[1]


def main():
    version = json.loads((ROOT / 'official-source.json').read_bytes())['active']
    if version != '884_156692':
        raise ValueError('Review automatic partner initialization when changing version')
    inputs, methods = {}, {}
    for name, signatures in {
        'common-source/Common/PlayerNpcDataWrap.cs': ['public static PlayerNpcDataWrap CreateNpcDataByInfo(long playerId, NpcInfo info, int? initialLevel = null)'],
        'common-source/MsgPlayerNpc.cs': ['protected override void OnMessage(NetMessage data)'],
        'client-source/NPCHandler.cs': ['private void MainPlayer_OnAddNewNpc(int classId, NpcMeetType type)'],
    }.items():
        source = ROOT / '.tools/source-packages/official-code' / name
        raw = source.read_bytes()
        if raw != (ROOT / '.tools/source-packages/current' / version / 'official-code' / name).read_bytes():
            raise ValueError('Selected original source differs: ' + name)
        inputs[source.relative_to(ROOT).as_posix()] = sha(raw)
        text = raw.decode('utf-8-sig').replace('\r\n', '\n')
        methods[name] = {signature: sha(method(text, signature).encode()) for signature in signatures}
    rules_path = 'unity/Assets/Official/Partners/partner-rules.json'
    rules = json.loads((ROOT / rules_path).read_bytes())['tables']
    settings = rules['game_settings']
    level = settings['NpcDefaultFriendshipLevel']
    automatic = [row for row in rules['npc'] if row['IsAutoMeet']]
    if [row['Id'] for row in automatic] != [8402]:
        raise ValueError('Review changed automatic partner roster')
    inputs[rules_path] = sha((ROOT / rules_path).read_bytes())
    condition_proof = 'assets/reference/partner-condition-source-manifest.json'
    inputs[condition_proof] = sha((ROOT / condition_proof).read_bytes())
    bindings = [
        'unity/Assets/Game/Runtime/Offline/Progression/OfflineCharacterProgressionService.cs',
        'unity/Assets/Game/Runtime/Offline/Progression/OfflineCharacterProgressionService.Partners.cs',
        'unity/Assets/Game/Runtime/Offline/Progression/OfflineCharacterExperience.cs',
        'unity/Assets/Game/Runtime/Offline/CharacterCreation/CharacterCreationOfflineService.cs',
        'unity/Assets/Game/Runtime/Presentation/CharacterCreation/CharacterCreationFlowController.cs',
    ]
    validation = [
        'scripts/fixtures/partner_condition_validation.cs',
        'unity/Assets/Game/Editor/CharacterCreation/CharacterCreationPlayModeAcceptance.cs',
    ]
    proof = dict(manifest=version, generator_sha256=sha(Path(__file__).read_bytes()),
        source_sha256=inputs, methods=methods,
        automatic_partners=[dict(id=row['Id'], condition=row['CanMeetCondition'], initial_level=level,
            initial_experience=next(value['FriendshipExp'] for value in rules['npc_friendship_level']
                if value['Id'] == row['Id'] and value['Level'] == level)) for row in automatic],
        binding_sha256={name: sha((ROOT / name).read_bytes()) for name in bindings},
        validation_source_sha256={name: sha((ROOT / name).read_bytes()) for name in validation},
        adaptations=[
            'User-confirmed offline trigger: character creation and entry into an existing profile.',
            'Original current CanMeet condition and configured initial friendship; current local activation time.',
            'Creation and automatic meeting share the character-creation transaction.',
            'Profile-card browsing and portrait inspection remain read-only; existing meetings are never recreated.',
            'Auto initialization preserves saved levels; no rank-limit row means no known cap in the local partner base-level projection.',
            'Original client message and notification handlers are evidence of consumption, not a recovered server trigger.',
        ])
    (ROOT / 'assets/reference/partner-automatic-meeting-source-manifest.json').write_text(
        json.dumps(proof, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    print('Recorded selected automatic partner initialization and confirmed offline trigger.')


if __name__ == '__main__':
    main()
