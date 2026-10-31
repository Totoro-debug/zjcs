"""Pin the actual active client/common inputs used by partner roster migration."""
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
FILES = (
    'common-source/NpcHelper.cs', 'common-source/NpcComparer.cs',
    'common-source/Common/NpcMeetStatus.cs', 'common-source/Common/NpcInfo.cs',
    'common-source/Common/PlayerNpcData.cs', 'common-source/Common/PlayerNpcDataWrap.cs',
    'common-source/Common/Player.cs', 'common-source/Common/PlayerUtils.cs',
    'common-source/Common/FunctionLimitInfoParser.cs',
    'common-source/Common/MapUnlockCondition.cs', 'common-source/Common/MapRegionActivedCondition.cs',
    'common-source/Common/HasManualMeetNpcCondition.cs',
    'client-source/NpcPanelNpcList.cs', 'client-source/NpcPanelNpcItem.cs',
    'client-source/NpcDetailPanelPropContent.cs',
)

def main():
    selection = json.loads((ROOT / 'official-source.json').read_text('utf8'))
    version = selection['active']
    inputs = {}
    for name in FILES:
        source = ROOT / '.tools/source-packages/official-code' / name
        archived = ROOT / '.tools/source-packages/current' / version / 'official-code' / name
        if source.read_bytes() != archived.read_bytes():
            raise ValueError('Official working source differs from selected version: ' + name)
        inputs[source.relative_to(ROOT).as_posix()] = hashlib.sha256(source.read_bytes()).hexdigest()
    data_paths = [ROOT / '.tools/source-packages/official-data' / (name + '.g.bytes')
                  for name in ('npc', 'npc_friendship_level', 'function_limit', 'conditions')]
    data_paths += [ROOT / 'unity/Assets/Official/Character/character-progression.json']
    target = ROOT / 'assets/reference/partner-roster-rules-source-manifest.json'
    target.write_text(json.dumps(dict(format=1, official_source=version,
        apk=selection['profiles'][version]['apk'], source_sha256=inputs,
        data_sha256={path.relative_to(ROOT).as_posix(): hashlib.sha256(path.read_bytes()).hexdigest()
                     for path in data_paths},
        migration={
            'NpcRosterRules.MeetStatus': 'NpcHelper.GetNpcMeetStatus',
            'NpcRosterRules.IsVisible': 'NpcPanelNpcList.InitNpcList',
            'NpcRosterRules.Compare': 'NpcComparer priority lists / Comparer / GetScore',
            'OfflineCharacterProgressionService.NpcRoster': 'NpcPanelNpcList.InitNpcList / RefreshLoop / PlayerNpcDataWrap.CreateNpcDataByInfo',
            'OfficialCharacterCatalog.MapRegionActivedCondition': 'MapRegionActivedCondition.GetValue / Player.RegionIsActived',
            'OfflineCharacterProgressionService.CanPinNpc': 'FunctionLimitInfoParser.IsLimit / HasManualMeetNpcCondition.CheckMeet',
            'OfflineCharacterProgressionService.SetNpcCollected': 'NpcDetailPanelPropContent.OnClickCollectionBtn / PlayerNpcData.IsCollected / CollectedTime',
        }), ensure_ascii=False, indent=2) + '\n', encoding='utf8')
    print(target)

if __name__ == '__main__':
    main()
