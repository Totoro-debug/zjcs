"""Migrate both original round cards at their external entity/data seam."""
from pathlib import Path
import hashlib
import json
import re

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / '.tools/source-packages/current/884_156692/official-code'
DEST = ROOT / 'unity/Assets/Game/Runtime/Official/BattleRoundUI'


def sha(path): return hashlib.sha256(path.read_bytes()).hexdigest()


def replace_method(text, name, body):
    match = re.search(r'\tprivate void ' + name + r'\([^\n]*\)\n\t\{', text)
    if not match: raise ValueError(name)
    start = match.end(); depth = 1; end = start
    while depth:
        if text[end] == '{': depth += 1
        elif text[end] == '}': depth -= 1
        end += 1
    return text[:start] + '\n' + body + '\n\t}' + text[end:]


def port(name):
    source = SOURCE / 'client-source' / (name + '.cs')
    scene = name.endswith('Scene')
    text = source.read_text(encoding='utf-8-sig')
    text = re.sub(r'^using (?:Common|Framework(?:\.[\w.]+)?|GFrameCore[\w.]*|Microsoft[\w.]*|ZLogger);\n', '', text, flags=re.M)
    text = re.sub(r'^\s*//IL_[^\n]*\n', '', text, flags=re.M)
    text = text.replace('UIBase, IGameObjectPoolItem, IGameRoundCharacterItem', 'UIBase, IGameRoundCharacterItem')
    text = re.sub(r'\bRoundRuntimeData\b', 'object', text).replace('public object object =>', 'public object RoundRuntimeData =>')
    text = text.replace('FightRoleCharacterComponent', 'BattleRoundRole').replace('FightSkillAsyncFirstPlayerComponent', 'IBattleRoundMimic')
    text = text.replace('LiteInfoCallback mLiteInfoCallback', 'IBattleRoundPlayerRequest mLiteInfoCallback')
    start = text.index('\t[Inject]'); end = text.index('\n\tpublic object RoundRuntimeData', start)
    text = text[:start] + '\tprivate IBattleRoundCardBindings bindings;\n\n\tpublic void Bind(IBattleRoundCardBindings value) => bindings = value ?? throw new ArgumentNullException(nameof(value));\n' + text[end:]
    text = text.replace('CircleSpriteRenderer', 'WorldCircleSpriteRenderer')
    text = text.replace('mGameCharacter.GetComponent<IBattleRoundMimic>()', 'mGameCharacter.Mimic')
    text = text.replace('mSkillAsyncFirstPlayer.OnTargetInitial.Subscribe(mOnSkillAsyncTargetInitialAction)', 'mSkillAsyncFirstPlayer.Subscribe(mOnSkillAsyncTargetInitialAction)')
    text = text.replace('Action<BattleRoundRole>', 'Action<long>')
    text = text.replace('mGamePlayManager.GameFactionHandler.GetData(mGameCharacter)', 'bindings.GetFaction(mGameCharacter)')
    text = text.replace('mLiteInfoCallback?.Release()', 'mLiteInfoCallback?.Dispose()')
    text = replace_method(text, 'RefreshIcon', '''		MainPlayerEffect.SetActive(false);
		if (mGameCharacter.NpcClassId.HasValue) { RefreshNpcIcon(mGameCharacter.NpcClassId.Value); return; }
		if (mGameCharacter.PlayerId.HasValue) { RefreshPlayerIcon(mGameCharacter.PlayerId.Value); return; }
		if (mGameCharacter.PetEntityClassId.HasValue) { RefreshMonsterIcon(mGameCharacter.PetEntityClassId.Value); return; }
		if (mGameCharacter.HasGuard)
		{
			if (mGameCharacter.GuardKind == BattleRoundGuardKind.Arena
				|| mGameCharacter.GuardKind == BattleRoundGuardKind.LegionFight
				|| mGameCharacter.GuardKind == BattleRoundGuardKind.MultiGroupRivalry)
			{
				BattleRoundPlayer info = mGameCharacter.GuardAppearance;
				RefreshPlayerHeadTemp(info.Sex, info.Profession, info.AppearanceData);
			}
		}
		else if (mSkillAsyncFirstPlayer != null)
		{
			if (mSkillAsyncFirstPlayer.TryGetTargetPlayerId(out var playerId)) RefreshMimicsMonsterIcon(playerId);
		}
		else RefreshMonsterIcon(mGameCharacter.MonsterId);''')
    text = replace_method(text, 'OnSkillAsyncTargetInitial', '\t\tRefreshPlayerIcon(component);').replace('OnSkillAsyncTargetInitial(BattleRoundRole component)', 'OnSkillAsyncTargetInitial(long component)')
    text = replace_method(text, 'RefreshNpcIcon', '''		NpcIconGO.gameObject.SetActive(true);
		if (!bindings.TryGetNpcIcon(npcClassId, out string icon))
			Debug.LogError("[Config] can`t find npc Info, npcClassId:" + npcClassId);
		else NpcIconSr.SetSprite(bindings.GetSprite(icon));''')
    text = text.replace('MeManager.MainPlayer.Id', 'bindings.MainPlayer.Id').replace('Player mainPlayer = MeManager.MainPlayer', 'BattleRoundPlayer mainPlayer = bindings.MainPlayer')
    text = text.replace('AppearanceConfig.Value.MimicsMonsterColor', 'bindings.MimicsMonsterColor')
    text = replace_method(text, 'RefreshOtherPlayerSpine', '''		if (!mIsWaitingPlayerInfo)
		{
			mIsWaitingPlayerInfo = true;
			(mLiteInfoCallback ?? (mLiteInfoCallback = bindings.CreatePlayerRequest(OnPlayerInfoGot))).Request(playerId);
		}''')
    text = text.replace('PlayerLiteInfo info', 'BattleRoundPlayer info')
    text = text.replace('Profession profession', 'int profession').replace('AppearanceData appearanceData', 'object appearanceData')
    text = replace_method(text, 'RefreshMonsterIcon', '''		if (!bindings.TryGetMonsterIcon(monsterId, out var config))
		{
			Debug.LogError("Can't find monster info by " + monsterId);
			return;
		}
		switch (config.Type)
		{
		case 2: RefreshNpcIcon(config.NpcClassId); return;
		case 1: RefreshMonsterIconImpl(config.Path); return;
		case 3: RefreshMonsterIconImpl(config.Path); return;
		}
		Debug.LogError("Invalid monster icon type " + monsterId + " " + config.Type);''')
    if not scene:
        # The UGUI card intentionally uses the original pet/monster path split,
        # rather than the scene card's MonsterIconType dispatch.
        text = text.replace('RefreshMonsterIcon(mGameCharacter.PetEntityClassId.Value);',
            'RefreshMonsterIcon(mGameCharacter.PetEntityClassId.Value, isPet: true);')
        text = text.replace('RefreshMonsterIcon(mGameCharacter.MonsterId);',
            'RefreshMonsterIcon(mGameCharacter.MonsterId, isPet: false);')
        text = replace_method(text, 'RefreshNpcIcon', '''\t\tNpcIconGO.gameObject.SetActive(true);
\t\tif (!bindings.TryGetNpcIcon(npcClassId, out string icon))
\t\t\tDebug.LogError("[Config] can`t find npc Info, npcClassId:" + npcClassId);
\t\telse NpcIconImg.sprite = bindings.GetSprite(icon);''')
        text = replace_method(text, 'RefreshMonsterIcon', '''\t\tstring iconName = isPet ? "ItemIcon/GameHeadIcon/Pet/G_" + monsterId : "ItemIcon/GameHeadIcon/Monster/G_" + monsterId;
\t\tSprite dynamicTextureSprite = bindings.GetSprite(iconName);
\t\tMonsterIconImg.sprite = dynamicTextureSprite;
\t\t((Component)MonsterIconImg).gameObject.SetActive(true);
\t\tMonsterHurtImg.sprite = dynamicTextureSprite;''')
    text = text.replace('mFightRoot.GetDynamicTextureSprite(', 'bindings.GetSprite(')
    if re.search(r'\b(?:Common|Inject|ILogger|GLogger|FightRole\w+|MeManager|Parsers|PlayerLiteInfoManager)\b', text):
        raise ValueError('Unadapted external dependency')
    text = '// Official ' + name + '; external data supplied at the local presentation seam.\nnamespace Zjcs.Official.BattleRoundUI\n{\nusing Zjcs.Official.ProfessionUI;\nusing Zjcs.Official.WorldUI;\n' + text + '\n}\n'
    return source, text


def main():
    outputs = {}
    sources = []
    for name in ('GameRoundCharacterItemScene', 'GameRoundCharacterItem'):
        source, content = port(name)
        sources.append(source)
        path = DEST / (name + '.cs'); path.write_text(content, encoding='utf-8', newline='\n')
        meta = Path(str(path) + '.meta')
        if not meta.exists(): meta.write_text('fileFormatVersion: 2\nguid: '+hashlib.md5(('zjcs:round-card:'+name).encode()).hexdigest()+'\n', encoding='utf-8')
        for p in (path, meta): outputs[p.relative_to(ROOT).as_posix()] = sha(p)
    interface = SOURCE / 'client-source/IGameRoundCharacterItem.cs'
    value = interface.read_text(encoding='utf-8-sig').replace('using Common;','').replace('FightRoleCharacterComponent', 'BattleRoundRole')
    value = re.sub(r'\bRoundRuntimeData (?=\w)', 'object ', value)
    value = value.replace('\n}', '\n\tvoid OnGameObjectPoolReset();\n\n\tvoid OnGameObjectPoolClear();\n}')
    path = DEST / interface.name; path.write_text('namespace Zjcs.Official.BattleRoundUI\n{\n'+value+'\n}\n',encoding='utf-8',newline='\n')
    meta = Path(str(path)+'.meta')
    if not meta.exists(): meta.write_text('fileFormatVersion: 2\nguid: '+hashlib.md5(b'zjcs:round-card:interface').hexdigest()+'\n',encoding='utf-8')
    for p in (path,meta):outputs[p.relative_to(ROOT).as_posix()] = sha(p)
    for name in ('BattleRoundCardBindings.cs', 'BattleRoundCardBindings.cs.meta',
        'BattleRoundPortraitAssets.cs', 'BattleRoundPortraitAssets.cs.meta'):
        p=DEST/name;outputs[p.relative_to(ROOT).as_posix()]=sha(p)
    for name in ('OfficialBattleRoundCardContext.cs','OfficialBattleRoundCardContext.cs.meta',
        'OfficialBattleSceneTimelineView.cs','OfficialBattleSceneTimelineView.cs.meta'):
        p=ROOT/'unity/Assets/Game/Runtime/Presentation/Combat'/name
        outputs[p.relative_to(ROOT).as_posix()]=sha(p)
    inputs = {p.relative_to(ROOT).as_posix():sha(p) for p in (*sources,interface,SOURCE/'common-source/Common/RoundRuntimeData.cs',SOURCE/'common-source/Common/MonsterIconType.cs',SOURCE/'common-source/Common/LanguageUtils.cs')}
    receipt = ROOT/'assets/reference/battle-hud/round-card-runtime-source-manifest.json'
    receipt.write_text(json.dumps(dict(manifest='884_156692',inputs=inputs,outputs=outputs,generator_sha256=sha(Path(__file__)),
        adaptations=['Entity component/config/player queries are supplied at IBattleRoundCardBindings; original branch priority retained.',
            'RoundRuntimeData is opaque to this view and retained as object; its rules remain in the battle timeline.',
            'Resolved guard appearances replace online guard parser/wrapper queries; the three supported guard kinds are retained.',
            'Reusable local player request and mimic subscription replace client managers; original waiting/reset/disposal order retained.',
            'Existing portrait enums, circle renderer and Unity logging adapters; original serialized fields, animation and death/pool order retained.'],
        scope='Both original round card classes and interface; explicit local projection of existing completed photographs, NPC/monster/pet icons and faction images. Native asset/config binding is recorded separately. Guard/mimic entity projection remains pending; the original round manager has a separate source receipt.'),ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print('Ported both original round cards and interface.')


if __name__ == '__main__': main()
