"""Port the selected client round manager; retain its queue and animation code."""
from pathlib import Path
import hashlib
import json
import re

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / '.tools/source-packages/current/884_156692/official-code'
DEST = ROOT / 'unity/Assets/Game/Runtime/Official/BattleRoundUI'


def sha(path): return hashlib.sha256(path.read_bytes()).hexdigest()


def replace_body(text, name, body):
    match = re.search(r'\t(?:private|public) (?:void|bool) ' + name + r'\([^\n]*\)\n\t\{', text)
    if not match: raise ValueError(name)
    start = match.end(); end = start; depth = 1
    while depth:
        if text[end] == '{': depth += 1
        elif text[end] == '}': depth -= 1
        end += 1
    return text[:start] + '\n' + body + '\n\t}' + text[end:]


def main():
    source = SOURCE / 'client-source/GameRoundUI.cs'
    text = source.read_text('utf-8-sig')
    text = re.sub(r'^using (?:Common|Framework(?:\.[\w.]+)?);\n', '', text, flags=re.M)
    text = re.sub(r'^\s*//IL_[^\n]*\n', '', text, flags=re.M)
    text = text.replace('GameUIBase, IMyContainerAddEntityEventReceiver, IEventReceiver', 'UIBase')
    text = text.replace('FightRoundDriverComponent', 'BattleRoundDriver').replace('RoundRuntimeData', 'BattleRoundData')
    text = text.replace('EcEntityIdentifier', 'int').replace('CharacterRoundDriverUpdateState', 'BattleRoundUpdateState')
    text = text.replace('DamageEventArgs', 'BattleRoundDamage').replace('FightRoleCharacterComponent', 'BattleRoundRole')
    text = text.replace('\n\tprivate FightComponent mFight;\n', '')
    text = text.replace('\n\tprivate IDisposable mOnSameGameUpdateDisposable;\n', '')
    start = text.index('\t[Inject]'); end = text.index('\n\tprivate void Awake()', start)
    text = text[:start] + '''\tprivate IBattleRoundUIBindings bindings;

\tpublic void Initialize(IBattleRoundUIBindings value)
\t{
\t\tInitWrapper(() => bindings = value ?? throw new ArgumentNullException(nameof(value)));
\t}

\tprivate static BattleRoundData Data(IGameRoundCharacterItem item) => (BattleRoundData)item.RoundRuntimeData;
''' + text[end:]
    text = text.replace('mFight = myGame.Component;\n\t\tmRoundDriver = mFight.RoundDriver;', 'mRoundDriver = bindings.Driver;')
    text = text.replace('mGamePlayConfig.Value', 'bindings.Configuration').replace('mFightRoot.GameRoundScene', 'bindings.Scene')
    text = text.replace('bindings.Scene.SetAttachUI(IconBack)', 'bindings.Scene.SetAttachUI(IconBack, bindings.SceneCamera)')
    text = text.replace('UIUtils.Instantiate(portraitIconPrefab, portraitIconPoolTransform, EUIInstantiateFlag.Transform)', 'bindings.InstantiateCard(portraitIconPrefab, portraitIconPoolTransform)')
    text = text.replace('UIUtils.Instantiate(GetPortraitIconPrefab(), GetPortraitIconSlotTransform(), EUIInstantiateFlag.Transform)', 'bindings.InstantiateCard(GetPortraitIconPrefab(), GetPortraitIconSlotTransform())')
    text = replace_body(text, 'AddEvent', '''\t\tmRoundDriver.OnDataRemove += OnRoundDataRemove;
\t\tmRoundDriver.OnDataInsert += OnRoundDataInsert;
\t\tmRoundDriver.OnDataUpdate += OnRoundDataUpdate;
\t\tmRoundDriver.OnCurActingUpdated += OnCurrentActingUpdated;
\t\tbindings.SameGameUpdated += OnSameGameUpdated;
\t\tbindings.Damage += MainGame_OnDamage;
\t\tbindings.Die += MainGame_OnDie;
\t\tbindings.EntityAdded += MyContainer_OnEntityAdd;''')
    text = replace_body(text, 'RemoveEvent', '''\t\tif (mRoundDriver != null)
\t\t{
\t\t\tmRoundDriver.OnDataRemove -= OnRoundDataRemove;
\t\t\tmRoundDriver.OnDataInsert -= OnRoundDataInsert;
\t\t\tmRoundDriver.OnDataUpdate -= OnRoundDataUpdate;
\t\t\tmRoundDriver.OnCurActingUpdated -= OnCurrentActingUpdated;
\t\t}
\t\tbindings.SameGameUpdated -= OnSameGameUpdated;
\t\tbindings.Damage -= MainGame_OnDamage;
\t\tbindings.Die -= MainGame_OnDie;
\t\tbindings.EntityAdded -= MyContainer_OnEntityAdd;''')
    text = text.replace('MyContainer_OnEntityAdd(MyEntity entity)', 'MyContainer_OnEntityAdd(int entityId)')
    text = text.replace('entity.Id', 'entityId')
    text = text.replace('mFightRoot.Fight.GamePlayStatus != GameStatus.Play', '!bindings.IsPlaying')
    text = text.replace('mFight.Container.GetEntity(data.EntityId)?.GetComponent<BattleRoundRole>()', 'bindings.GetRole(data.EntityId)')
    text = replace_body(text, 'CanHurtByDamage', '''\t\tlong num = args.Damage + -args.Shield;
\t\tif (num > 0) return false;
\t\tif (num < 0) return true;
\t\treturn args.HasHitEntity && args.DamageType != 0;''')
    text = text.replace('BattleRoundDriver roundDriver = mFight.RoundDriver;', 'BattleRoundDriver roundDriver = bindings.Driver;')
    text = text.replace('.RoundComponent.CalcRoundInterval()', '.Interval')
    text = text.replace('(gameRoundCharacterItem as IGameObjectPoolItem).OnGameObjectPoolReset()', 'gameRoundCharacterItem.OnGameObjectPoolReset()')
    text = text.replace('(icon as IGameObjectPoolItem).OnGameObjectPoolClear()', 'icon.OnGameObjectPoolClear()')
    text = text.replace('gameRoundCharacterItem.Init(data, isCurrent, fightRoleCharacterComponent);',
        'bindings.PrepareCard(gameRoundCharacterItem, data.EntityId);\n\t\tgameRoundCharacterItem.Init(data, isCurrent, fightRoleCharacterComponent);')
    text = text.replace('icon.Refresh(data, isCurrent, fightRoleCharacterComponent);',
        'bindings.PrepareCard(icon, data.EntityId);\n\t\t\t\ticon.Refresh(data, isCurrent, fightRoleCharacterComponent);')
    # Cards deliberately retain opaque round data: they never inspect its fields.
    # Only the manager reads the projected driver values.
    text = re.sub(r'(\w+(?:\[i(?: - 1)?\])?)\.BattleRoundData', r'Data(\1)', text)
    text = text.replace('mIconList[i].BattleRoundData', 'Data(mIconList[i])')
    text = replace_body(text, 'IsRoundAvailable', '''\t\tentityNotExist = !bindings.HasEntity(entityId);
\t\tif (entityNotExist) return false;
\t\treturn bindings.ShowOnRoundProgress(entityId);''')
    if re.search(r'\b(?:mFight|mFightRoot|myGame|IGameObjectPoolItem|MyEntity|Parsers|GameStatus|MeManager|Inject)\b', text):
        raise ValueError('Unadapted manager dependency')
    target = DEST / 'GameRoundUI.cs'
    target.write_text('// Selected original client manager; only external driver/entity/loading queries are adapted.\nnamespace Zjcs.Official.BattleRoundUI\n{\nusing Zjcs.Official.ProfessionUI;\n' + text + '\n}\n', 'utf8', newline='\n')
    paths = [target, DEST/'BattleRoundUIBindings.cs']
    for path in paths:
        meta = Path(str(path)+'.meta')
        if not meta.exists(): meta.write_text('fileFormatVersion: 2\nguid: '+hashlib.md5(('zjcs:round-ui:'+path.name).encode()).hexdigest()+'\n','utf8',newline='\n')
    receipt = ROOT/'assets/reference/battle-hud/round-manager-runtime-source-manifest.json'
    inputs = [source, SOURCE/'client-source/GameUIBase.cs'] + [SOURCE/'common-source/Common'/name
        for name in ('CharacterRoundDriverUpdateState.cs', 'RoundRuntimeData.cs', 'FightRoundDriverComponent.cs')]
    receipt.write_text(json.dumps(dict(manifest='884_156692', inputs={p.relative_to(ROOT).as_posix():sha(p) for p in inputs},
        outputs={p.relative_to(ROOT).as_posix():sha(p) for path in paths for p in (path,Path(str(path)+'.meta'))},
        generator_sha256=sha(Path(__file__)), adaptations=[
            'Explicit local driver/entity/event projections replace live FightRoot and Common server proxies.',
            'The original GameUIBase initialized notification enters through Initialize and the existing UIBase lifecycle.',
            'PrepareCard supplies local entity/photograph bindings before the original Init/Refresh calls.',
            'Original pooling, ordering, availability/waiting, death queue, minimum spacing, wrap and interpolation retained.']),ensure_ascii=False,indent=2)+'\n','utf8',newline='\n')


if __name__ == '__main__': main()
