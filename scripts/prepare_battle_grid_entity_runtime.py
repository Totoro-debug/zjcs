"""Port original grid entity client components, with explicit offline view inputs.

This does not implement missing server components. Empty original client wrappers
retain their original lifecycle base; their server data is provided by the host.
"""
import json
import re
from pathlib import Path

from official_apk import ROOT
from prepare_battle_scene_appearance_runtime import identity, meta, write_changed

CODE = ROOT / '.tools/source-packages/current/884_156692/official-code'
DEST = ROOT / 'unity/Assets/Game/Runtime/Official/BattleGridItemEntities'
RECEIPT = ROOT / 'assets/reference/battle-animation/grid-entity-runtime-source-manifest.json'
NAMESPACE = 'Zjcs.Official.BattleGridItemEntities'


def main():
    manifest = CODE.parent / 'code-manifest.json'
    selected = json.loads(manifest.read_bytes())
    assembly = CODE / 'managed/decoded/Assembly-CSharp.dll'
    assert selected['manifest'] == '884_156692'
    assert identity(assembly)['sha256'] == selected['assemblies'][assembly.name]['decoded_sha256']
    DEST.mkdir(parents=True, exist_ok=True)
    inputs, outputs = [], []

    def emit(name, text):
        path = DEST / name
        write_changed(path, text)
        meta(path)
        outputs.extend((identity(path), identity(path.with_name(path.name + '.meta'))))

    def original(name):
        path = CODE / 'client-source' / (name + '.cs')
        inputs.append(identity(path))
        return path.read_text(encoding='utf-8-sig')

    def wrap(text):
        text = re.sub(r'^using (?:Common|Framework.DependencyInjection|GameFramework|GFrameCore.Extensions.ZLogger|Microsoft.Extensions.Logging|ZLogger);\s*', '', text, flags=re.M)
        text = text.replace('[Inject]', '')
        imports = ('using Object = UnityEngine.Object;\n'
                   'using Zjcs.Official.EntityLifecycle;\n'
                   'using Zjcs.Official.EntityTransformViews;\n'
                   'using Zjcs.Official.StoryPlayables;\n'
                   'using Zjcs.Official.StoryPlayableManager;\n'
                   'using Zjcs.Official.BattleSkillPlayables;\n'
                   'using Zjcs.Official.EntityViews;\n'
                   'using Zjcs.Official.EntityClientComponents;\n'
                   'using Zjcs.Official.StoryAnimation;\n'
                   'using Zjcs.Official.StorySourceFade;\n'
                   'using Zjcs.Official.BattleStatusPlayables;\n')
        text = re.sub(r'Object\.op_Implicit\(((?:[^()]|\([^()]*\))*)\)', r'(bool)(\1)', text)
        split = re.search(r'(?m)^(?:\[|public (?:class|interface))', text).start()
        result = ('// Original client component; see grid entity runtime source receipt.\n'
                + imports + text[:split] + '\nnamespace ' + NAMESPACE + '\n{\n'
                + text[split:].rstrip() + '\n}\n')
        return '\n'.join(line.rstrip() for line in result.splitlines()) + '\n'

    # These official client wrappers have literally no body. Their common/server
    # component types are supplied through a host-owned projection, not recreated.
    empty = ['MyFightRoleGrid', 'MyFightGameplayTag', 'MyFightGridMover',
             'MyFightProp', 'MyStatusEnd', 'MyStatusAutoRemove', 'MyFightStatusMoveNear']
    for name in empty:
        text = original(name)
        assert re.search(r'public class ' + name + r'\s*:[^{]+\{\s*\}\s*$', text)
        base = 'MyFightStatusBase' if name == 'MyFightStatusMoveNear' else 'MyComponent'
        text = re.sub(r': My(?:Component|FightStatusBase)<[^>]+>',
                      ': ' + base + '<IBattleGridComponentViewData>', text)
        emit(name + '.cs', wrap(text))

    text = original('MyFightStatusBase')
    text = text.replace('EcComponentInfoEmpty', 'object')
    text = re.sub(r' where T : EcComponent(?: where TInfo : EcComponentInfo)?', '', text)
    emit('MyFightStatusBase.cs', wrap(text))

    text = original('GameCharacterZSet')
    emit('GameCharacterZSet.cs', wrap(text))
    text = original('MyFightRound')
    text = text.replace('MyComponent<FightRoundComponent, FightRoundComponentInfo>',
                        'MyComponent<IBattleGridComponentViewData>')
    emit('MyFightRound.cs', wrap(text))

    text = original('MyFightRoleCharacter')
    text = text.replace('MyComponent<FightRoleCharacterComponent, FightRoleCharacterComponentInfo>',
                        'MyComponent<BattleRoleCharacterViewData>')
    text = text.replace('protected IFightRootProxy mFightRoot { get; set; }',
                        'public IBattleGridRoleViewRoot mFightRoot { get; set; }')
    text = text.replace('CharacterComponent character = Component.Character;',
                        'BattleRoleCharacterIdentity character = Component.Character;')
    text = text.replace('MeManager.MainPlayer != null && character != null && character.PlayerId == MeManager.MainPlayer.Id',
                        'mFightRoot.MainPlayerId.HasValue && character != null && character.PlayerId == mFightRoot.MainPlayerId.Value')
    text = text.replace('mFightRoot.Fight.AddCharacter(this)', 'mFightRoot.AddCharacter(this)')
    text = text.replace('myContainer.GetEntity(Component.MasterId)', 'mFightRoot.GetEntity(Component.MasterId)')
    text = text.replace('mFightRoot.Fight.RemoveCharacter(this)', 'mFightRoot.RemoveCharacter(this)')
    text = text.replace('mFightRoot.HasFightRoot && (Object)(object)mFightRoot.Fight != (Object)null',
                        'mFightRoot.HasFightRoot')
    text = text.replace('MySerializable userData', 'object userData')
    # Original try/catch boundaries and callbacks remain; only the unavailable
    # structured logging backend is replaced with Unity's exception logger.
    text, count = re.subn(r'catch \(Exception ex\)\s*\{.*?\n\t\t\}',
                         'catch (Exception ex)\n\t\t{\n\t\t\tDebug.LogException(ex);\n\t\t}', text, flags=re.S)
    assert count == 2
    emit('MyFightRoleCharacter.cs', wrap(text))

    text = original('MyFightRoleGridItem')
    text = text.replace('MyComponent<FightRoleGridItemComponent>', 'MyComponent<BattleGridItemViewData>')
    text = text.replace('private IMyPlayableManager mMyPlayableManager { get; set; }',
                        'public IMyPlayableManager mMyPlayableManager { get; set; }')
    text = text.replace('private IFightRootProxy mFightRoot { get; set; }',
                        'public IBattleGridRoleViewRoot mFightRoot { get; set; }')
    text = text.replace('StageInfo stageInfo = mFightRoot.StageInfo;\n\t\tif (Parsers.GameSettings.Battle.PvpStageTypes.Contains(stageInfo.Type)',
                        'if (mFightRoot.IsPvpStage')
    text = text.replace('MySerializable userData', 'object userData')
    text = text.replace('MyFightRoleFight component = ((Component)mFightRoot.Character.myEntity).GetComponent<MyFightRoleFight>();',
                        'int? component = mFightRoot.FactionOf(mFightRoot.Character.myEntity);')
    text = text.replace('MyFightRoleFight component2 = ((Component)myCharacter.myEntity).GetComponent<MyFightRoleFight>();',
                        'int? component2 = mFightRoot.FactionOf(myCharacter.myEntity);')
    text = text.replace('(Object)(object)component != (Object)null', 'component.HasValue')
    text = text.replace('(Object)(object)component2 != (Object)null', 'component2.HasValue')
    text = text.replace('component.Component.Faction', 'component.Value')
    text = text.replace('component2.Component.Faction', 'component2.Value')
    text = text.replace('mFightRoot.Fight.myCharacters', 'mFightRoot.Characters')
    assert 'StageInfo' not in text and 'MyFightRoleFight' not in text
    emit('MyFightRoleGridItem.cs', wrap(text))

    # Existing world-map MyActionAgent is the retained 871 implementation with
    # ActionTypePlayables. The selected 884 prefab has the newer event-only class.
    text = original('MyActionAgent')
    text = text.replace('MyComponent<ActionAgentComponent>, IActionAgentStartEventReceiver, IEventReceiver, IActionAgentEndEventReceiver',
                        'MyComponent<ActionAgentComponentData>')
    text = re.sub(r'\bActionAgentComponent\b', 'ActionAgentComponentData', text)
    text = re.sub(r'\bActionComponent\b', 'ActionAgentActionData', text)
    text = text.replace('MySerializable userData', 'object userData')
    text = text.replace('Component.StartEvent += (IActionAgentStartEventReceiver)this;', 'Component.StartEvent += ActionAgent_OnStart;')
    text = text.replace('Component.EndEvent += (IActionAgentEndEventReceiver)this;', 'Component.EndEvent += ActionAgent_OnEnd;')
    text = text.replace('Component.StartEvent -= (IActionAgentStartEventReceiver)this;', 'Component.StartEvent -= ActionAgent_OnStart;')
    text = text.replace('Component.EndEvent -= (IActionAgentEndEventReceiver)this;', 'Component.EndEvent -= ActionAgent_OnEnd;')
    emit('MyActionAgent.cs', wrap(text))

    text = original('ECFightRoleCharacterView')
    text = text.replace('using AppearanceSystem;', '')
    text = text.replace('GamePlayerHUD', 'BattleRolePlayerHud')
    text = text.replace('PlayerAnimatorAppearance', 'BattleRolePlayerAppearance')
    text = text.replace('FightRoleCharacterComponent', 'BattleRoleCharacterViewData')
    text = text.replace('FightRolePlayerComponent', 'IBattleRolePlayerAppearanceData')
    text = text.replace('FightRoleNpcComponent', 'IBattleRoleNpcViewData')
    text = text.replace('MultiLanguageText', 'UnityEngine.Object')
    text = text.replace('private IMyPlayableManager MyPlayableManager { get; set; }',
                        'public IMyPlayableManager MyPlayableManager { get; set; }')
    text = text.replace('private IGamePlayManager GamePlayManager { get; set; }',
                        'public IBattleGridPlayableEnvironment GamePlayManager { get; set; }')
    text = text.replace('private IFightRootProxy FightRoot { get; set; }',
                        'public IBattleGridPlayableEnvironment FightRoot { get; set; }')
    text = text.replace('FightTagUtils.HasTag(FightRoot.GameObject, EFightRootTag.NotPlayGeneratePlayable)',
                        'FightRoot.NotPlayGeneratePlayable')
    text = text.replace('Sex sex, Profession profession, AppearanceData appearanceData',
                        'int sex, int profession, object appearanceData')
    text = text.replace('IAppearanceRuntimeDataOverride runtimeDataOverride', 'object runtimeDataOverride')
    text = text.replace('TryGetComponent<AnimationController>(ref animationController)',
                        'TryGetComponent<AnimationController>(out animationController)')
    text = text.replace('public class ECFightRoleCharacterView', '[Serializable]\npublic class ECFightRoleCharacterView')
    emit('ECFightRoleCharacterView.cs', wrap(text))

    text = original('ECFightRoleStatusDisplayView')
    text = text.replace(', IEcEntityEndEventReceiver, IEventReceiver', '')
    text = text.replace('[Rename("每行最大个数")]', '').replace('[Rename("最大行数")]', '')
    text = text.replace('FightStatusItem mStatusItem', 'Object mStatusItem')
    text = text.replace('FightStatusController mStatusController', 'IBattleStatusDisplayController mStatusController')
    text = text.replace('private IFightRootProxy mFightRoot { get; set; }',
                        'public IBattleGridRoleViewRoot mFightRoot { get; set; }\n\n\tpublic IBattleStatusDisplayFactory StatusDisplayFactory { get; set; }')
    text = text.replace('mEntity.EndEvent += (IEcEntityEndEventReceiver)this;',
                        'mEntity.GetComponent<IBattleEntityEndEvents>().Ended += Entity_OnEnd;')
    text = text.replace('mEntity.EndEvent -= (IEcEntityEndEventReceiver)this;',
                        'mEntity.GetComponent<IBattleEntityEndEvents>().Ended -= Entity_OnEnd;')
    text = text.replace('void IEcEntityEndEventReceiver.Entity_OnEnd(EcEntity entity, MySerializable mySerializable)',
                        'private void Entity_OnEnd(ECViewEntityData entity, object mySerializable)')
    text = text.replace('new FightStatusController(agent, mStatusItem, mStatusItemSlot, mFightRoot, mMaxCountOfRow, mMaxRowCount)',
                        'StatusDisplayFactory.Create(agent, mStatusItem, mStatusItemSlot, mFightRoot, mMaxCountOfRow, mMaxRowCount)')
    text = text.replace('public class ECFightRoleStatusDisplayView', '[System.Serializable]\npublic class ECFightRoleStatusDisplayView')
    emit('ECFightRoleStatusDisplayView.cs', wrap(text))

    contract = DEST / 'BattleGridEntityViewInputs.cs'
    assert contract.is_file(), 'The reviewed view data/service boundary is required'
    meta(contract)
    outputs.extend((identity(contract), identity(contract.with_name(contract.name + '.meta'))))
    emit('Zjcs.Official.BattleGridItemEntities.asmdef', json.dumps(dict(
        name=NAMESPACE, rootNamespace=NAMESPACE, autoReferenced=False,
        references=['Zjcs.Official.EntityLifecycle', 'Zjcs.Official.EntityTransformViews',
                    'Zjcs.Official.StoryPlayables', 'Zjcs.Official.StoryPlayableManager',
                    'Zjcs.Official.BattleStatusPlayables', 'Zjcs.Official.BattleSkillPlayables',
                    'Zjcs.Official.EntityViews', 'Zjcs.Official.EntityClientComponents', 'Zjcs.Official.StoryAnimation',
                    'Zjcs.Official.StorySourceFade']), indent=2) + '\n')
    meta(DEST, folder=True)
    outputs.append(identity(DEST.with_name(DEST.name + '.meta')))
    write_changed(RECEIPT, json.dumps(dict(manifest='884_156692', code_manifest=identity(manifest),
        assembly=identity(assembly), original_code=inputs, outputs=outputs,
        generator=identity(Path(__file__)),
        adaptation=['Host-owned component data replaces unavailable server component classes; the seven empty client wrappers remain empty.',
                    'Original role/master registration and summon callbacks use an explicit scene registry instead of global player/fight services.',
                    'PVP stage membership and nullable entity faction lookup are supplied from original stage settings and current entity state.',
                    'Original playable order, owner/faction selection, null branches and stop behavior are retained.',
                    'Original Z positioning, subscription order and round-progress flag are retained.',
                    'The 884 event-only MyActionAgent is ported separately from the retained 871 client component.',
                    'Full original role and status display view branches are retained with explicit data, playable, HUD/appearance and status display service boundaries; selected source HUD/appearance/status item pointers are null.'],
        limits=['Component extraction alone does not establish complete native grid prefabs, EC view registration or public battle presentation.']),
        ensure_ascii=False, indent=2) + '\n')
    print('Migrated original grid entity client components and views:', len(empty) + 8)


if __name__ == '__main__':
    main()
