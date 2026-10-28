"""Restore the original invitation row widgets, friendship fills and property view."""
from __future__ import annotations
import argparse
import hashlib
import json
from pathlib import Path
import re
import uuid

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / '.tools/source-packages/current/871_156598/official-code/client-source'
DEST = ROOT / 'unity/Assets/Game/Runtime/Official/TeamUI'
RECEIPT = ROOT / 'assets/reference/official-map/map12-team-invite-widgets-source-manifest.json'


def build(write):
    inputs, outputs = [], []

    def record(path):
        return dict(path=path.relative_to(ROOT).as_posix(), sha256=hashlib.sha256(path.read_bytes()).hexdigest())

    def replace(text, old, new):
        if old not in text:
            raise ValueError('Original invitation widget changed: ' + old)
        return text.replace(old, new)

    for name in ('FriendshipInfo', 'PropInfo', 'NpcFriendshipLevelShow',
                 'TeamInvitePlayerInfoView', 'TeamInviteItemBase', 'TeamInviteSortTypeUtils'):
        original = SOURCE / (name + '.cs')
        inputs.append(record(original))
        text = original.read_text(encoding='utf-8-sig')
        text = re.sub(r'^using (?:Common|Framework(?:\.[\w.]+)?|R3);\n', '', text, flags=re.M)
        text = text.replace('\t[Inject]\n', '')
        text = re.sub(r'^\s*//IL_[^\n]*\n', '\n', text, flags=re.M)
        text = text.replace('IFriendshipData', 'ITeamInviteFriendshipData').replace('ActorTargetData', 'IProfessionInfoTarget')
        text = text.replace('StateSwitcher<', 'WorldStateSwitcher<')
        if name not in ('TeamInviteSortTypeUtils',):
            at = text.index('\n{', text.index('public ')) + 2
            configure = '\n\tprotected ITeamInviteUiServices services;\n\tpublic virtual void Configure(ITeamInviteUiServices value) { services = value; }\n'
            if name == 'TeamInvitePlayerInfoView':
                configure = '\n\tprivate ITeamInviteUiServices services;\n\tpublic void Configure(ITeamInviteUiServices value)\n\t{ services = value; mPlayerIcon?.Configure(value.Icons); mFriendShipInfo.Configure(value); mPropInfo.Configure(value); }\n'
            elif name == 'TeamInviteItemBase':
                configure = '\n\tprotected ITeamInviteUiServices services;\n\tpublic virtual void Configure(ITeamInviteUiServices value)\n\t{ services = value; mPlayerInfoView.Configure(value); }\n'
            text = text[:at] + configure + text[at:]
        if name == 'FriendshipInfo':
            text = text.replace('new PlayerFriendshipData(targetPlayerId)', 'services.GetPlayerFriendshipData(targetPlayerId)')
            text = text.replace('((UnityEvent)Btn.onClick).AddListener((UnityAction)ToggleText);', 'Btn.onClick.AddListener(ToggleText);')
            text = text.replace('MeManager.MainPlayer.RelationshipData.OnFriendshipUpdate', 'services.FriendshipUpdated')
            text = text.replace('MeManager.MainPlayer != null', 'services.MainPlayerAvailable')
            text = text.replace('CultureNumberArg.Compact(friendshipToTarget + friendshipToPlayer)', 'services.Icons.PlayerInfo.CompactNumber(unchecked(friendshipToTarget + friendshipToPlayer))')
            text = text.replace('\t\tLevelExpParser levelExpParser = Parsers.Instance.LevelExpParser;\n', '')
            text = text.replace('levelExpParser.CalcLevelByExp', 'services.RelationshipLevelByExp')
            text = text.replace('levelExpParser.GetLevelUpExp', 'services.RelationshipLevelUpExp')
            text = text.replace('levelExpParser.GetExp', 'services.RelationshipExp')
        elif name == 'NpcFriendshipLevelShow':
            text = text.replace('MeManager.MainPlayer.OnNpcFriendshipUpdate', 'services.NpcFriendshipUpdated')
            text = text.replace('MeManager.MainPlayer != null', 'services.MainPlayerAvailable')
            text = text.replace('PlayerNpcDataWrap', 'TeamInviteNpcRecord')
            text = text.replace('MeManager.MainPlayer.NpcManager.GetByNpcClassId', 'services.GetNpc')
            text = text.replace('\t\tNpcFriendshipLevelInfoParser npcFriendshipLevelInfoParser = Parsers.Instance.NpcFriendshipLevelInfoParser;\n', '')
            text = text.replace('npcFriendshipLevelInfoParser.GetMaxLevel', 'services.NpcFriendshipMaxLevel')
            text = replace(text, 'NpcFriendshipLevelInfo npcFriendshipLevelInfo = npcFriendshipLevelInfoParser.Get(mClassId, mFriendshipLevel);', 'long friendshipExp = services.NpcFriendshipExp(mClassId, mFriendshipLevel);')
            text = replace(text, 'NpcFriendshipLevelInfo npcFriendshipLevelInfo2 = npcFriendshipLevelInfoParser.Get(mClassId, mFriendshipLevel + 1);', 'long nextFriendshipExp = services.NpcFriendshipExp(mClassId, mFriendshipLevel + 1);')
            text = text.replace('npcFriendshipLevelInfo2.FriendshipExp', 'nextFriendshipExp').replace('npcFriendshipLevelInfo.FriendshipExp', 'friendshipExp')
        elif name == 'PropInfo':
            text = text.replace('DisposableBag mDisposableBag;', 'TeamInviteResourceOperationBag mDisposableBag = new TeamInviteResourceOperationBag();')
            text = text.replace('\tprivate IResourceLoader ResourceLoader { get; set; }\n', '')
            text = text.replace('mDisposableBag = default;', 'mDisposableBag = new TeamInviteResourceOperationBag();')
            text = replace(text, 'PropCfgInfo propCfgInfo = Parsers.Instance.PropCfgInfoParser.Get(propType);', 'string icon = services.PropSmallIcon(propType);')
            text = replace(text, 'Disposable.AddTo<AssetOperationHandle<Sprite>>(ResourceLoader.LoadSprite(propCfgInfo.SmallIcon), ref mDisposableBag).Result', 'mDisposableBag.Add(services.Icons.PlayerInfo.Resources.LoadAsset<Sprite>(icon)).Result')
            text = replace(text, 'new LanguageUtils.PropValue(propType, propValue, includeUnit: false, includePropDesc: false, -1L)', 'services.PropValue(propType, propValue, includeUnit: false, includePropDesc: false, -1L)')
        elif name == 'TeamInvitePlayerInfoView':
            text = text.replace('protected PlayerIcon mPlayerIcon;', 'protected TeamPlayerIcon mPlayerIcon;')
            text = text.replace('mPlayerIcon.TargetData.IsPlayer', 'mPlayerIcon.TargetData.TargetType == ActorTargetType.Player')
        elif name == 'TeamInviteItemBase':
            text = text.replace('private PlayerCompanionTag mPlayerCompanionTag;', 'private Component mPlayerCompanionTag;')
            text = text.replace('mTargetData.ToFriendshipData()', 'services.GetFriendshipData(mTargetData)')
            text = text.replace('mPlayerCompanionTag.SetPlayerId(mPlayerInfo.PlayerId)', 'services.SetCompanionTag(mPlayerCompanionTag, mPlayerInfo.PlayerId)')
        # Logging is the only omitted original code; preserve default branch values.
        if name in ('TeamInviteItemBase', 'TeamInviteSortTypeUtils'):
            text = re.sub(r'\t\t(?:\t)?GLogger\w+ message =.*?\n\t\t(?:\t)?Log\.(?:Error|Warn)\([^\n]*\);',
                          '\t\tUnityEngine.Debug.LogWarning("Unhandled original invitation sort value.");', text, flags=re.S)
        if re.search(r'MeManager|Parsers\.|GLogger|\bDisposableBag\b|\bIResourceLoader\b|Common;', text):
            raise ValueError('Unmigrated widget dependency: ' + name)
        text = re.sub(r'\n(?:[ \t]*\n){2,}', '\n\n', text)
        text = ('namespace Zjcs.Official.TeamUI\n{\nusing Zjcs.Official.ProfessionUI;\n'
                'using Zjcs.Official.WorldUI;\nusing Object = UnityEngine.Object;\n'
                'using MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;\n' + text + '\n}\n')
        path = DEST / (name + '.cs')
        meta = path.with_name(path.name + '.meta')
        guid = uuid.uuid5(uuid.NAMESPACE_URL, 'zjcs/map12/team-invite/' + path.name).hex
        for target, raw in ((path, text.encode()), (meta, ('fileFormatVersion: 2\nguid: ' + guid + '\n').encode())):
            if write:
                target.write_bytes(raw)
            elif target.read_bytes() != raw:
                raise ValueError('Original widget drift: ' + str(target))
            outputs.append(record(target))
    for name in ('TeamInviteUiServices', 'TeamInvitePlayerItem', 'TeamInviteSubPanel'):
        path = DEST / (name + '.cs')
        meta = path.with_name(path.name + '.meta')
        if write:
            meta.write_bytes(('fileFormatVersion: 2\nguid: ' + uuid.uuid5(uuid.NAMESPACE_URL,
                'zjcs/map12/team-invite/' + path.name).hex + '\n').encode())
        outputs.extend((record(path), record(meta)))
    inputs.extend(record(SOURCE / (name + '.cs')) for name in
                  ('TeamInvitePlayerItem', 'TeamInviteSubPanel', 'InviterBase', 'TeamInviter'))
    inputs.append(record(SOURCE.parent / 'common-source/Common/LanguageUtils.cs'))
    result = dict(format=1, manifest='871_156598', mapClassId=12, inputs=inputs, outputs=outputs,
                  script=record(Path(__file__)), adaptation='Original widget branches, arithmetic, resource disposal and event order. '
                  'Manager/parser calls are injected and original player-icon ports are reused.',
                  scope='Player-mode row/list lifecycle and invitation awaits only; robot toggle navigation, '
                        'native invitation prefab and real-profile integration remain pending.')
    raw = (json.dumps(result, ensure_ascii=False, indent=2) + '\n').encode()
    if write:
        RECEIPT.write_bytes(raw)
    elif RECEIPT.read_bytes() != raw:
        raise ValueError('Invitation widget receipt drift')
    print('Original invitation friendship/property/icon widgets verified')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--write', action='store_true')
    build(parser.parse_args().write)
