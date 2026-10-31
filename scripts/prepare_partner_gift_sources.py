"""Record the active official source inputs for the migrated partner gift rules."""
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SOURCE_FILES = (
    'common-source/NpcGiftItemCompare.cs', 'common-source/NpcHelper.cs',
    'common-source/Common/NpcGiftInfoParser.cs', 'common-source/Common/NpcFriendshipLevelInfoParser.cs',
    'common-source/Common/NpcInfoParser.cs', 'client-source/NpcDetailPanelGiftContent.cs',
)

def main():
    selection = json.loads((ROOT / 'official-source.json').read_text('utf8'))
    version = selection['active']
    inputs = {}
    for name in SOURCE_FILES:
        path = ROOT / '.tools/source-packages/official-code' / name
        archived = ROOT / '.tools/source-packages/current' / version / 'official-code' / name
        if path.read_bytes() != archived.read_bytes():
            raise ValueError('Working official source does not match active source: ' + name)
        inputs[path.relative_to(ROOT).as_posix()] = hashlib.sha256(path.read_bytes()).hexdigest()
    target = ROOT / 'assets/reference/partner-gift-rules-source-manifest.json'
    data_paths = [ROOT / '.tools/source-packages/official-data' / (name + '.g.bytes')
                  for name in ('npc', 'npc_gift', 'npc_unlock_item', 'npc_friendship_level', 'game_settings')]
    data_paths += [ROOT / 'unity/Assets/Official/Character/character-progression.json',
                   ROOT / 'unity/Assets/Game/Resources/Official/Progression/mainline-tasks.json']
    target.write_text(json.dumps(dict(format=1, official_source=version,
        apk=selection['profiles'][version]['apk'], source_sha256=inputs,
        data_sha256={path.relative_to(ROOT).as_posix(): hashlib.sha256(path.read_bytes()).hexdigest()
                     for path in data_paths},
        migration={
            'NpcGiftRules.Compare': 'NpcGiftItemCompare.CompareShowGift / CompareSelectGift',
            'NpcGiftRules.Experience': 'NpcGiftInfoParser.GetAllExpByDict / NpcDetailPanelGiftContent.GetGiftFriendship',
            'NpcGiftRules.AutoSelect': 'NpcDetailPanelGiftContent.DoAutoSelectGift',
            'OfflineCharacterProgressionService.CanSendNpcGift': 'NpcHelper.CanSendGiftToNpc',
            'OfflineCharacterProgressionService.GiveNpcGifts': 'NpcDetailPanelGiftContent.OnClickGiveGiftBtn / IsFriendshipLevelLimit / IsFriendshipOverflow'
        }), ensure_ascii=False, indent=2) + '\n', encoding='utf8')
    print(target)

if __name__ == '__main__':
    main()
