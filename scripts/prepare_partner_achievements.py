"""Port the selected original friendship tasks, records and claim calculation."""
import hashlib
import json
import re
from pathlib import Path
from official_apk import ROOT
from prepare_partner_gift_configuration import method
from prepare_partner_input_dialog import metadata


def sha(path): return hashlib.sha256(path.read_bytes()).hexdigest()
def write(path, value):
    data = value.encode('utf8')
    if not path.exists() or path.read_bytes() != data: path.write_bytes(data)


def main():
    version = json.loads((ROOT / 'official-source.json').read_bytes())['active']
    if version != '884_156692': raise ValueError('Review selected friendship achievement source')
    inputs, methods = {}, {}
    def source(name):
        p = ROOT / '.tools/source-packages/current' / version / 'official-code/common-source/Common' / (name + '.cs')
        inputs[p.relative_to(ROOT).as_posix()] = sha(p)
        return p.read_text('utf-8-sig').replace('\r\n', '\n')
    def selected(name, signature):
        value = method(source(name), signature)
        methods.setdefault(name, {})[signature] = hashlib.sha256(value.encode()).hexdigest()
        return value
    template = ROOT / 'scripts/templates/partner_achievements/PartnerFriendshipAchievementRules.cs'
    inputs[template.relative_to(ROOT).as_posix()] = sha(template)
    text = template.read_text('utf8')
    text = text.replace('/*ENUMS*/', '\n'.join(source(n).split('namespace Common;\n', 1)[1].strip() for n in ('AchievementState', 'AchievementType')))
    keys = []
    text = text.replace('/*BASE_KEY_METHODS*/', '\n'.join(selected('AchievementDataKey', sig) for sig in ('public override bool Equals(object obj)', 'public override int GetHashCode()')))
    for name in ('AchievementDataKeyNormal', 'AchievementDataKeyNpcFriendshipLevel', 'AchievementDataKeyStarGodNpcFriendshipLevel'):
        value = source(name).split('namespace Common;\n', 1)[1].strip()
        value = re.sub(r'^\s*\[(?:MessagePackObject|Key|IgnoreMember)[^\n]*\]\n', '', value, flags=re.M)
        value = value.replace('public static AchievementDataKey None => Singleton<AchievementDataKeyNormal>.Create(() => new AchievementDataKeyNormal(AchievementType.None));',
            'private static readonly AchievementDataKey none = new AchievementDataKeyNormal(AchievementType.None);\n    public static AchievementDataKey None => none;')
        keys.append(value)
    text = text.replace('/*KEYS*/', '\n'.join(keys))
    text = text.replace('/*INFO_METHODS*/', '\n'.join(selected('AchievementInfo', sig).replace('id >>> 12', '(int)((uint)id >> 12)') for sig in (
        'public static int GroupAndIndexToId(int groupId, int index)', 'public static (int groupId, int index) GetGroupAndIndex(int id)')))
    records = []
    for sig in ('public bool IsCanGetReward(int index)', 'public bool IsAlreadyGotReward(int index)',
                'public bool IsAnyCanGetReward()', 'public bool TryAddGotRewardByIndex(long playerId, int index)',
                'public bool TrySetCanRewardsMaxIndex(int maxIndex)'):
        value = selected('PlayerAchievementRecordDataWrap', sig)
        value, _ = re.subn(r'\t{3}GLoggerErrorInterpolatedStringHandler message =.*?\n\t{3}Log.Error\([^\n]*\);', '\t\t\tNonSequentialClaim?.Invoke(playerId, index);', value, flags=re.S)
        records.append(value)
    text = text.replace('/*RECORD_METHODS*/', '\n'.join(records))
    text = text.replace('/*GROUP_RECORD*/', selected('PlayerAchievement', 'public PlayerAchievementRecordDataWrap GetGroupRecord(int groupId)').replace('new AchievementRecordData', 'new PlayerAchievementRecordDataWrap'))
    players = []
    signatures = ['public bool IsCanGetAchievementReward(AchievementInfo info)', 'public bool IsAlreadyGotAchievementReward(AchievementInfo info)',
        'public bool IsAnyCanGetAchievementReward(List<AchievementDataKey> dataKeys)', 'public bool IsAnyCanGetAchievementReward(AchievementDataKey dataKey)',
        'public bool TryGetCanGetAchievementRewardMaxIndex(int groupId, out int maxCanRewardIndex)',
        'public int TryRecordAlreadyGotAchievementReward(int groupId, int count, out HashSet<int> gotRewardIds, out List<Dictionary<int, long>> rewards, bool needInvoke = true)',
        'public void FireOnRecordAlreadyGotAchievementRewardBatch()', 'public void FireOnUpdateAchievementData(AchievementDataKey dataKey)',
        'public void RecordAchievementRewardByMaxIndex(AchievementDataKey dataKey, int maxIndex)',
        'public void RecordAchievementRewardByDict(Dictionary<AchievementDataKey, int> achievementRewardsDict)',
        'public bool TryResetAchievementGroupRecord(AchievementDataKey dataKey)']
    for sig in signatures:
        value = selected('Player', sig).replace('Parsers.Instance.AchievementRawInfoParser', 'Parser').replace('AchievementRawInfoParser achievementRawInfoParser', 'PartnerFriendshipAchievementRules achievementRawInfoParser')
        value = value.replace('dataKey.GroupId', 'Parser.GetAchievementKeyToGroup(dataKey)').replace('result.GroupId', 'Parser.GetAchievementKeyToGroup(result)')
        value = value.replace('PlayerAchievementDataWrap.CreateNew()', 'new PlayerAchievement()').replace('new AchievementRecordData()', 'new PlayerAchievementRecordDataWrap()')
        players.append(value)
    text = text.replace('/*PLAYER_METHODS*/', '\n'.join(players))
    for flag, name in [('NORMAL_TASK', 'AchievementTaskNpcFriendshipLevel'), ('STAR_TASK', 'AchievementTaskStarGodNpcFriendshipLevel')]:
        parts = []
        for sig in ('public override AchievementState GetAchievementState(int id, Player player)', 'public override (long maxValue, long currentValue) GetProgressValue(int id, Player player)'):
            value = selected(name, sig).replace('public override ', 'public ')
            value = re.sub(r'AchievementData(?:StarGod)?NpcFriendshipLevel (achievementData\w+) = (.*) as AchievementData(?:StarGod)?NpcFriendshipLevel;', r'AchievementData \1 = \2;', value)
            parts.append(value)
        text = text.replace('/*' + flag + '*/', '\n'.join(parts))
    if any(token in text for token in ('/*', 'Parsers.', 'GLogger', 'Log.Error', 'PlayerAchievementDataWrap', ' as AchievementData')): raise ValueError('Review remaining original dependencies')
    text = re.sub(r'^[\t ]+', lambda match: match[0].expandtabs(4), text, flags=re.M)
    rules = ROOT / 'unity/Assets/Official/Partners/partner-rules.json'
    inputs[rules.relative_to(ROOT).as_posix()] = sha(rules)
    rows = [r for r in json.loads(rules.read_bytes())['tables']['achievement'] if r['AchievementType'] in (27, 77)]
    if len(rows) != 1980 or len({r['Group'] for r in rows}) != 99 or any(r['CycleCount'] or r['CycleIncrementParams'] for r in rows): raise ValueError('Review original independent achievement closure')
    target = ROOT / 'unity/Assets/Game/Runtime/Official/Partners/PartnerFriendshipAchievementRules.cs'
    write(target, text)
    meta = metadata(target)
    write(meta, '\n'.join(line.rstrip() for line in meta.read_text('utf8').splitlines()) + '\n')
    asm = target.parent / 'Zjcs.Official.Partners.asmdef'
    data = json.loads(asm.read_bytes())
    if 'Unity.Newtonsoft.Json' not in data['references']: data['references'].append('Unity.Newtonsoft.Json')
    write(asm, json.dumps(data, indent=2) + '\n')
    for name in ('AchievementRawInfoParser', 'AchievementRecordData', 'AchievementDataNpcFriendshipLevel', 'AchievementDataStarGodNpcFriendshipLevel'):
        source(name)
    client = ROOT / '.tools/source-packages/current' / version / 'official-code/client-source/AchievementPanelItem.cs'
    inputs[client.relative_to(ROOT).as_posix()] = sha(client)
    outputs = [target, meta, asm, ROOT / 'unity/Assets/Game/Runtime/Offline/Progression/OfflineCharacterPropertyProgress.cs']
    for name in ('OfflinePartnerAchievementRecord', 'OfflinePartnerAchievementService'):
        path = ROOT / 'unity/Assets/Game/Runtime/Offline/Progression' / (name + '.cs')
        sidecar = metadata(path)
        canonical = ('\n'.join(line.rstrip() for line in sidecar.read_text('utf8').splitlines()) + '\n').encode()
        if sidecar.read_bytes() != canonical: sidecar.write_bytes(canonical)
        outputs.extend((path, sidecar))
    helpers = ['scripts/prepare_partner_achievement_validation.py', 'scripts/fixtures/partner_achievement_validation.cs']
    proof = dict(manifest=version, inputs=inputs, original_methods=methods, outputs={p.relative_to(ROOT).as_posix(): sha(p) for p in outputs},
        generator_sha256=sha(Path(__file__)), rows=len(rows), groups=99,
        validation_helpers={name: sha(ROOT / name) for name in helpers},
        operation=['Retain selected normal/star-god friendship task order, original key equality/hash/opcodes, encoded group/index identity, independent template ordering, record defaults and sequential claim calculation.',
            'Replace Mongo/player/parser dependency access with an explicit in-memory player and selected catalog; preserve all original event and return-code branches.',
            'Only the selected independent friendship templates are projected; reject future cycle configurations. JSON parameters use the existing Newtonsoft dependency.',
            'User-confirmed local server replacement: refresh eligible indices from original friendship task conditions after load/meet/friendship change; re-read the profile for claims and atomically save configured rewards and receipt indices, publishing notifications only after the write.',
            'The local service exposes explicit refresh/claim endpoints; normal and star types stay distinct even when the same NPC has both groups. Same-type requests cover only met, open partners of the requested original type.',
            'Legacy absent records normalize to an empty list. Generic save validation preserves the original 12-bit group and 20-bit index domains; selected-table checks reject out-of-range saved records at the service boundary.',
            'Formal host refresh triggers, original achievement UI/request binding, production tip ownership and Windows Player verification are not yet connected.'])
    write(ROOT / 'assets/reference/partner-achievement-rules-source-manifest.json', json.dumps(proof, ensure_ascii=False, indent=2) + '\n')
    print('Prepared original friendship achievement tasks/records/claims: 1980 rows, 99 groups')


if __name__ == '__main__': main()
