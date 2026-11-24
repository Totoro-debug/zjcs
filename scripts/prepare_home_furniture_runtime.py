"""Migrate the selected FurnitureBase, HomeNotice and storage controller bodies."""
import hashlib
import inspect
import json
from pathlib import Path
import re

from official_apk import ROOT
from official_source import OfficialSource


DEST = ROOT / 'unity/Assets/Game/Runtime/Official/HomeFurniture'
RECEIPT = ROOT / 'assets/reference/home-furniture-controller-source-manifest.json'
TEMPLATES = Path(__file__).parent / 'templates/home_furniture'


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def replace(text, old, new):
    if text.count(old) != 1:
        raise ValueError('Original furniture boundary changed: ' + old)
    return text.replace(old, new)


def prepare():
    source = OfficialSource()
    code = ROOT / '.tools/source-packages/current' / source.version / 'official-code'
    receipt = code.parent / 'code-manifest.json'
    identity = json.loads(receipt.read_bytes())
    assembly = code / 'managed/decoded/Assembly-CSharp.dll'
    if identity['manifest'] != source.version or sha(assembly.read_bytes()) != identity['assemblies']['Assembly-CSharp.dll']['decoded_sha256']:
        raise ValueError('Selected furniture assembly differs')
    inputs = {p.relative_to(ROOT).as_posix(): sha(p.read_bytes()) for p in (receipt, assembly)}
    previous = json.loads(RECEIPT.read_bytes())['outputs'] if RECEIPT.exists() else {}
    outputs = {}

    def write(path, raw):
        name = path.relative_to(ROOT).as_posix()
        if path.exists() and path.read_bytes() != raw and sha(path.read_bytes()) != previous.get(name):
            raise ValueError('Preserve another furniture change: ' + name)
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(raw)
        outputs[name] = sha(raw)

    def metadata(path, folder=False):
        token = hashlib.md5(('zjcs:home-furniture-runtime:' + path.relative_to(ROOT).as_posix()).encode()).hexdigest()
        kind = 'DefaultImporter' if folder else 'AssemblyDefinitionImporter' if path.suffix == '.asmdef' else 'MonoImporter'
        raw = 'fileFormatVersion: 2\nguid: ' + token + '\n'
        if folder:
            raw += 'folderAsset: yes\n'
        raw += kind + ':\n  externalObjects: {}\n'
        write(Path(str(path) + '.meta'), raw.encode())

    def read(name):
        path = code / 'client-source' / (name + '.cs')
        raw = path.read_bytes()
        inputs[path.relative_to(ROOT).as_posix()] = sha(raw)
        text = raw.decode('utf-8-sig').replace('\r\n', '\n')
        return re.sub(r'(?m)^using [^\n]+;\n|^\s*//IL_[^\n]*\n|^\s*\[Inject\]\n', '', text)

    header = '''// Selected original controller body; local services replace global client owners.
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;
using Zjcs.Official.HomeUI;
using Zjcs.Official.HomeSceneInput;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
namespace Zjcs.Official.HomeFurniture
{
using Object = UnityEngine.Object;
'''

    def emit(name, text):
        text = re.sub(r'\bStateSwitcher<', 'WorldStateSwitcher<', text)
        text = re.sub(r'\bMultiLanguageText\b', 'WorldMultiLanguageText', text)
        text = re.sub(r'\bConditionHandlerRegistration\b', 'IDisposable', text).replace('IConditionNotice', 'IHomeFurnitureConditionNotice')
        text = re.sub(r'\bICondition\b', 'IHomeFurnitureCondition', text)
        text = text.replace('IStoryPlayContext', 'IHomeFurnitureStoryContext')
        source_header = header
        if name in ('EntityClickSound', 'HomeNpcChatEntry'):
            pass
        if name == 'EntityClickSound':
            source_header = source_header.replace('using UnityEngine.Serialization;\n',
                'using UnityEngine.Serialization;\nusing Zjcs.Official.HomeMainUiControls;\n')
        if name == 'HomeNpcBubbleTip':
            source_header = source_header.replace('using Zjcs.Official.HomeUI;\n',
                'using Zjcs.Official.HomeUI;\nusing Zjcs.Official.HomeHudUI;\n')
        if 'List<' in text or 'HashSet<' in text or 'KeyValuePair<' in text:
            source_header = source_header.replace('using System.Collections;\n', 'using System.Collections;\nusing System.Collections.Generic;\n')
        if name in ('HomeWeapon', 'AppearancePartSideSwitcher', 'HomeWeaponAnimation', 'HomeNpcBubbleTip'):
            source_header = source_header.replace('using UnityEngine.Serialization;\n',
                'using UnityEngine.Serialization;\nusing Spine;\nusing Spine.Unity;\nusing Animation = Spine.Animation;\nusing AnimationState = Spine.AnimationState;\n')
        text = '\n'.join(line.rstrip() for line in (source_header + text + '\n}\n').split('\n'))
        code_only = re.sub(r'//[^\n]*', '', text)
        if any(token in code_only for token in ('MeManager', 'Parsers.Instance', 'WindowManager', 'AwardManager', 'Framework.', '[Inject]', 'ServiceProvider', 'PlayerPlayState', 'StoryPlayEntry', 'GLogger', 'Log.', 'NpcPanelUtils', 'ServiceProxyBase', 'TimeManager', 'TimeUtils', 'AppearanceAnimUtils', 'Disposable.')) or re.search(r'\bDisposableBag\b', code_only):
            raise ValueError('Unbound furniture service: ' + name)
        path = DEST / (name + '.cs')
        write(path, text.encode())
        metadata(path)

    text = read('FurnitureBase')
    text = replace(text, '''\tprivate IServiceProvider ServiceProvider { get; set; }
\tprivate IPlayerPlayStateModule PlayerPlayStateModule { get; set; }
\tprivate IStoryPlayEntry StoryPlayEntry { get; set; }
\tprotected IHomeData HomeData { get; set; }''', '''    public IHomeFurnitureServices Services { get; set; }

    // Bind the recovered original fields before activation/Start.
    public void Bind(EFurniture furniture, WorldStateSwitcher<EFurnitureState> states,
        HomeNotice notice, GameObject repairEffect, float showTime, float fullTime,
        IHomeFurnitureServices services)
    {
        mFurniture = furniture;
        mStateSwitcher = states;
        mHomeNotice = notice;
        mRepairEffect = repairEffect;
        mRepairEffectShowTime = showTime;
        mRepairEffectFullTime = fullTime;
        Services = services ?? throw new ArgumentNullException(nameof(services));
    }''')
    text = text.replace('Parsers.Instance.FurnitureInfoParser.Get(mFurniture)', 'Services.GetFurnitureInfo(mFurniture)')
    text = text.replace('MeManager.ConditionHandler.Subscribe(', 'Services.SubscribeCondition(')
    text = text.replace('AwardManager.Instance.RegisterHandler(', 'Services.RegisterAwardHandler(')
    text = text.replace('AwardManager.Instance.RemoveHandler(', 'Services.RemoveAwardHandler(')
    text = text.replace('AwardManager.Instance != null', 'Services != null')
    text = text.replace('MeManager.MainPlayer', 'Services.Player')
    text = text.replace('HomeData.OnPostHomeChange.Subscribe(', 'Services.SubscribePostHomeChange(')
    text = text.replace('StoryPlayEntry.Dispose(', 'Services.DisposeStory(')
    text = text.replace('IHomePlayerData playerData = HomeData.PlayerData;', 'IHomeFurniturePlayerData playerData = Services.PlayerData;')
    text = replace(text, 'ServerInfoManager.Instance.CheckFunctionDisabled(Services.Player, ServerFunctionType.Furniture, new CommonFunctionsDisabledKey((long)mFurniture))', 'Services.CheckFunctionDisabled(mFurniture)')
    text = text.replace('BroadcastManager.ShowErrText(53851, null, false)', 'Services.ShowError(53851)')
    text = replace(text, 'private bool HandleReward(PlayerAwardMsg msg)', 'private bool HandleReward(HomeFurnitureAward msg)')
    text = text.replace('msg.SourceData.SourceOperate != SourceOperate.RepairFurniture', '!msg.IsRepairFurniture')
    begin = text.index('\t\tGLoggerDebugInterpolatedStringHandler message =')
    end = text.index('\n\t\treturn true;', begin)
    text = text[:begin] + '\t\tDebug.Log(GetType().Name + " HandleAward RepairFurniture", this);' + text[end:]
    text = text.replace('Services.Player.GetStoryStatus(storyName) == StoryStatus.None', '!Services.Player.HasStartedStory(storyName)')
    text = text.replace('PlayerPlayStateClientUtils.DoStoryAction(PlayerPlayStateModule, () =>', 'Services.DoStoryAction(() =>')
    text = text.replace('new StoryPlayCommonContext("FurnitureBase", Services.Player, null, null)', 'Services.CreateStoryContext("FurnitureBase")')
    text = text.replace('StoryPlayEntry.Play(', 'Services.PlayStory(')
    text = replace(text, '''\t\t\tWindowManager.Instance.Popup((FurnitureRepairDialog d) =>
\t\t\t{
\t\t\t\td.Init(Furniture, isBuild: false);
\t\t\t});''', '\t\t\tServices.PopupRepair(Furniture, isBuild: false);')
    emit('FurnitureBase', text)

    text = read('HomeNotice')
    text = replace(text, '''\tprivate IServiceProvider ServiceProvider { get; set; }
\tprivate IHomeData HomeData { get; set; }''', '''    public IHomeFurnitureServices Services { get; set; }

    public void Bind(WorldMultiLanguageText text, WorldStateSwitcher<bool> states,
        string customRepairNotice, IHomeFurnitureServices services)
    {
        mRepairNoticeTxt = text;
        mShowStateSwitcher = states;
        mCustomRepairNotice = customRepairNotice;
        Services = services ?? throw new ArgumentNullException(nameof(services));
    }''')
    text = text.replace('MeManager.MainPlayer', 'Services.Player')
    text = text.replace('MeManager.ConditionHandler.Subscribe(', 'Services.SubscribeCondition(')
    text = text.replace('HomeData.InMyHome', 'Services.InMyHome')
    text = text.replace('.IsMet(Services.Player)', '.IsMet()')
    text = text.replace('ConditionNoticeFactory.Create(ServiceProvider, mUnlockCondition)', 'Services.CreateConditionNotice(mUnlockCondition)')
    text = text.replace('BroadcastManager.Instance.SystemMsg(', 'Services.SystemMessage(')
    emit('HomeNotice', text)
    emit('StoreBoxFurniture', read('StoreBoxFurniture').replace('WindowManager.Instance.Popup<PlayerBagPanel>()', 'Services.PopupPlayerBag()'))
    text = read('EntityClickSound')
    text = replace(text, 'public Event Sound;', 'public AudioClip Sound;\n    [NonSerialized] public Action<AudioClip, GameObject> Post;')
    text = text.replace('protected IInputOperationModule InputOperationModule { get; set; }',
        'public IInputOperationModule InputOperationModule { get; set; }')
    text = replace(text, 'Sound.Post(((Component)this).gameObject);', 'Post?.Invoke(Sound, ((Component)this).gameObject);')
    emit('EntityClickSound', text)

    emit('HomeViewMode', read('HomeViewMode'))
    text = read('HomeViewModeControl')
    text = replace(text, 'private IHomeData HomeData { get; set; }',
        'public IHomeFurnitureViewModes ViewModes { get; set; }')
    text = text.replace('HomeData.OnHomeViewModeChange.Subscribe(', 'ViewModes.Subscribe(')
    text = text.replace('HomeData.HomeViewMode', 'ViewModes.HomeViewMode')
    emit('HomeViewModeControl', text)

    text = read('Home.ActorBehaviour/ActionEventData').replace('namespace Home.ActorBehaviour;\n', '')
    emit('ActionEventData', text)
    text = read('HomeActionEventHandler')
    text = replace(text, 'private ISubscriber<ActionEventData> Subscriber { get; set; }',
        'public IHomeFurnitureActionEvents ActionEvents { get; set; }')
    text = replace(text, 'SubscriberExtensions.Subscribe<ActionEventData>(Subscriber, (Action<ActionEventData>)OnHandleEvent, Array.Empty<MessageHandlerFilter<ActionEventData>>())',
        'ActionEvents.Subscribe(OnHandleEvent)')
    emit('HomeActionEventHandler', text)
    emit('HomeSwitcherActionEventHandler', read('HomeSwitcherActionEventHandler'))

    text = read('HomeWeaponRack')
    text = replace(text, 'private DisposableBag mDisposableBag;',
        'private readonly HomeFurnitureLifetime mDisposableBag = new HomeFurnitureLifetime();')
    text = replace(text, '''\tprivate IResourceLoader ResourceLoader { get; set; }
\tprivate IHomeData HomeData { get; set; }
\tprivate IServiceProvider ServiceProvider { get; set; }''',
        '    public IHomeWeaponRackServices Services { get; set; }')
    text = replace(text, '''\t\tmDisposableBag = default;
\t\tDisposable.AddTo<IDisposable>(HomeData.OnHomeChange.Subscribe(OnHomeChange), ref mDisposableBag);''',
        '\t\tmDisposableBag.Add(Services.SubscribeHomeChange(OnHomeChange));')
    text = text.replace('MeManager.MainPlayer', 'Services.MainPlayer')
    text = text.replace('IHomePlayerData playerData = HomeData.PlayerData;',
        'IProfessionPlayerAppearanceSource playerData = Services.PlayerData;')
    text = text.replace('AppearanceData appearanceData = playerData.AppearanceData;',
        'AppearanceData appearanceData = (AppearanceData)playerData.AppearanceData;')
    text = text.replace('AppearanceUtils.', 'Services.Appearance.')
    text = text.replace('playerData.Profession,', '(Profession)playerData.Profession,')
    text = text.replace('slot.DestroyChildren();',
        'for (int index = slot.childCount - 1; index >= 0; index--) Object.Destroy(slot.GetChild(index).gameObject);')
    text = text.replace('Parsers.Instance.SpineSkinInfoParser.Get(', 'Services.Tables.SpineSkinInfoParser.Get(')
    text = text.replace('AppearanceAddressUtils.GetPlayerPartPrefabRenderer(', 'ProfessionAppearancePartPaths.GetPlayerPartPrefabRenderer(')
    text = replace(text, 'Disposable.AddTo<AssetOperationHandle<GameObject>>(ResourceLoader.LoadPrefab(playerPartPrefabRenderer), ref mDisposableBag).Result',
        'mDisposableBag.Add(Services.LoadPrefab(playerPartPrefabRenderer)).Result')
    text = text.replace('ServiceProviderExtensions.Instantiate(ServiceProvider, result, slot, false)', 'Services.Instantiate(result, slot, false)')
    text = text.replace('quaternion.op_Implicit(quaternion.identity)', 'Quaternion.identity')
    emit('HomeWeaponRack', text)

    from prepare_unity_profession_ui_parts import log_blocks
    for name in ('AppearancePartSideType', 'AppearancePartSide', 'AppearancePartSideSwitchListener'):
        emit(name, read(name))
    text = log_blocks(read('AppearancePartSideSwitcher'))
    text = replace(text, '''\t\t\tGLoggerErrorInterpolatedStringHandler message = new GLoggerErrorInterpolatedStringHandler(0, 0, out var _);
\t\t\tLog.Error(exception, ref message, null, "UpdateSide", "/Volumes/Data/Projects/idleXX_cn_release_android/Client/Assets/Scripts/AppearanceSystem/SplitPart/AppearancePartSideSwitcher.cs", 64);''',
        '\t\t\tDebug.LogException(exception, this);')
    text = replace(text, 'public AppearancePartSideType Side;',
        'public IHomeWeaponAnimationBindings AnimationBindings { get; set; }\n\n\tpublic AppearancePartSideType Side;')
    text = text.replace('AppearanceAnimUtils.PlayAnimation(', 'HomeWeaponAnimation.PlayAnimation(AnimationBindings, ')
    text = text.replace('Object.op_Implicit((Object)(object)SkeletonAnimation)', '(bool)SkeletonAnimation')
    text = text.replace('Object.op_Implicit((Object)(object)SkeletonGraphic)', '(bool)SkeletonGraphic')
    emit('AppearancePartSideSwitcher', text)
    text = read('HomeWeapon')
    text = text.replace('TryGetComponent<AppearanceRendererLite>(ref appearanceRendererLite)',
        'TryGetComponent<AppearanceRendererLite>(out appearanceRendererLite)')
    emit('HomeWeapon', text)

    from prepare_unity_profession_appearance_resolver import method
    animation_source = read('AppearanceSystem.Runtime/AppearanceAnimUtils')
    bodies = []
    for name in ('SkeletonAnimation', 'SkeletonGraphic'):
        value = log_blocks(method(animation_source, 'public static void PlayAnimation(' + name + ' '))
        # This overload reuses the decompiler's earlier logging-enabled local.
        # The shared log helper handles only declarations with `out var`.
        if name == 'SkeletonAnimation':
            value = replace(value, '''\t\t\t\tGLoggerWarnInterpolatedStringHandler message2 = new GLoggerWarnInterpolatedStringHandler(9, 1, out enabled);
\t\t\t\tif (enabled)
\t\t\t\t{
\t\t\t\t\tmessage2.AppendLiteral("不存在指定的动画:");
\t\t\t\t\tmessage2.AppendFormatted(animationName, 0, null, "animationName");
\t\t\t\t}
\t\t\t\tLog.Warn(ref message2, null, "PlayAnimation", "/Volumes/Data/Projects/idleXX_cn_release_android/Client/Assets/Scripts/AppearanceSystem/Utils/AppearanceAnimUtils.cs", 206);''',
                '\t\t\t\tDebug.LogWarning("不存在指定的动画:" + animationName);')
            value = replace(value, '\t\tbool enabled;\n', '')
        value = value.replace('PlayAnimation(' + name + ' ',
            'PlayAnimation(IHomeWeaponAnimationBindings bindings, ' + name + ' ', 1)
        value = value.replace('GetOrLoadSpineAnimation(', 'bindings.GetOrLoadSpineAnimation(')
        value = value.replace('UpdateAnimation(', 'bindings.UpdateAnimation(')
        bodies.append(value)
    bodies.append(method(animation_source, 'private static void SetAnimation('))
    emit('HomeWeaponAnimation', 'public static class HomeWeaponAnimation\n{\n' + '\n'.join(bodies) + '\n}\n')

    text = replace(read('DiningTableNpcPopClick'),
        'public class DiningTableNpcPopClick : ClickableHomeObj\n{',
        'public class DiningTableNpcPopClick : ClickableHomeObj\n{\n\tpublic IHomeFurnitureServices Services { get; set; }\n')
    text = replace(text, 'NpcPanelUtils.Open(NpcPanelTabType.Dialogue);', 'Services.PopupNpcPanel(1);')
    emit('DiningTableNpcPopClick', text)

    text = read('HomeNpcChatEntry')
    text = replace(text, 'MeManager.MainPlayer != null', 'Services != null && Services.Player != null')
    text = text.replace('MeManager.MainPlayer.IsFunctionLimit(FunctionType.NPCDialogue)', 'Services.IsFunctionLimit(57)')
    text = text.replace('MeManager.MainPlayer', 'Services.Player')
    text = replace(text, 'WindowManager.Instance.Popup<NpcPanel>();', 'Services.PopupNpcPanel();')
    text = replace(text, 'TimeManager.Instance.GetDateTime()', 'Services.GetDateTime()')
    text = replace(text, 'TimeUtils.GetDailyResetTimeFromDate(dateTime)', 'Services.GetDailyResetTimeFromDate(dateTime)')
    text = text.replace('ServiceProxyBase<PlayerServiceProxyImpl>.Instance.OperPlayerNpcDialogueRefreshDailyDialogue(invokeByPlayer: false);',
        'Services.RefreshDailyDialogues(invokeByPlayer: false);')
    emit('HomeNpcChatEntry', text)

    text = replace(read('HomeNpcBubbleTip'), 'internal class HomeNpcBubbleTip : RedDotItemBase', 'public class HomeNpcBubbleTip : RedDotItemBase')
    text = replace(text, '''\tprivate DisposableBag mDisposableBag;
\tprivate IResourceLoader ResourceLoader { get; set; }
\tprivate IHomeData HomeData { get; set; }''', '''\tprivate readonly HomeFurnitureLifetime mSubscriptions = new HomeFurnitureLifetime();

\tprivate readonly List<string> mLoadedIcons = new List<string>();

\tpublic IHomeFurnitureServices FurnitureServices { get; set; }

\tpublic IHomeNpcBubbleTipServices BubbleServices { get; set; }

\tpublic IHomeWeaponAnimationBindings AnimationBindings { get; set; }''')
    text = replace(text, '''\tprotected override void OnEnableAndInited()
\t{
\t\tbase.OnEnableAndInited();
\t\tmDisposableBag = default;
\t\tMeManager.MainPlayer.OnNpcDialogueAdd += MainPlayer_OnNpcDialogueAdd;
\t\tMeManager.MainPlayer.OnNpcDialogueUpdate += MainPlayer_OnNpcDialogueUpdate;
\t\tMeManager.MainPlayer.OnNpcDialogueRemove += MainPlayer_OnNpcDialogueRemove;
\t\tDisposable.AddTo<IDisposable>(HomeData.OnPostHomeChange.Subscribe(OnPostHomeChange), ref mDisposableBag);
\t}

\tprotected override void OnDisableAndInited()
\t{
\t\tif (MeManager.MainPlayer != null)
\t\t{
\t\t\tMeManager.MainPlayer.OnNpcDialogueAdd -= MainPlayer_OnNpcDialogueAdd;
\t\t\tMeManager.MainPlayer.OnNpcDialogueUpdate -= MainPlayer_OnNpcDialogueUpdate;
\t\t\tMeManager.MainPlayer.OnNpcDialogueRemove -= MainPlayer_OnNpcDialogueRemove;
\t\t}
\t\tmDisposableBag.Dispose();
\t\tbase.OnDisableAndInited();
\t}

\tprivate void MainPlayer_OnNpcDialogueUpdate(PlayerNpcDialogueDataWrap obj)
\t{
\t\tSetDirty();
\t}

\tprivate void MainPlayer_OnNpcDialogueAdd(PlayerNpcDialogueDataWrap obj)
\t{
\t\tSetDirty();
\t}

\tprivate void MainPlayer_OnNpcDialogueRemove(PlayerNpcDialogueDataWrap obj)
\t{
\t\tSetDirty();
\t}''', '''\tprivate void ReleaseLoadedIcons()
\t{
\t\tif ((Object)(object)mIcon != (Object)null)
\t\t{
\t\t\tmIcon.sprite = null;
\t\t}
\t\tif (BubbleServices != null)
\t\t{
\t\t\tfor (int i = 0; i < mLoadedIcons.Count; i++)
\t\t\t{
\t\t\t\tBubbleServices.UnloadNpcIcon(mLoadedIcons[i]);
\t\t\t}
\t\t}
\t\tmLoadedIcons.Clear();
\t}

\tprotected override void OnEnableAndInited()
\t{
\t\tbase.OnEnableAndInited();
\t\tmSubscriptions.Dispose();
\t\tif (BubbleServices != null)
\t\t{
\t\t\tmSubscriptions.Add(BubbleServices.SubscribeDialogueChanged(OnDialogueChanged));
\t\t\tmSubscriptions.Add(BubbleServices.SubscribePostHomeChange(OnPostHomeChange));
\t\t}
\t}

\tprotected override void OnDisableAndInited()
\t{
\t\tmSubscriptions.Dispose();
\t\tReleaseLoadedIcons();
\t\tbase.OnDisableAndInited();
\t}

\tprivate void OnDialogueChanged()
\t{
\t\tSetDirty();
\t}''')
    text = replace(text, '\t\tmDisposableBag.Clear();\n\t\tbase.Refresh();', '\t\tReleaseLoadedIcons();\n\t\tbase.Refresh();')
    text = replace(text, 'AppearanceAnimUtils.PlayAnimation(mSkeletonAnimation, animationName, loop: true);',
        'HomeWeaponAnimation.PlayAnimation(AnimationBindings, mSkeletonAnimation, animationName, loop: true);')
    begin = text.index('\tpublic override bool ShowRedDot()')
    text = text[:begin] + '''\tpublic override bool ShowRedDot()
\t{
\t\tif (FurnitureServices == null || !FurnitureServices.InMyHome || FurnitureServices.Player == null)
\t\t{
\t\t\treturn false;
\t\t}
\t\tif (FurnitureServices.IsFunctionLimit(57))
\t\t{
\t\t\treturn false;
\t\t}
\t\tif (BubbleServices != null && BubbleServices.TryFindActiveDialogueNpcClassId(out int npcClassId))
\t\t{
\t\t\tSetNpc(npcClassId);
\t\t\treturn true;
\t\t}
\t\treturn base.ShowRedDot();
\t}

\tpublic HomeNpcBubbleTip SetNpc(int npcClassId)
\t{
\t\tstring iconPath = BubbleServices?.GetNpcIconPath(npcClassId);
\t\tif (string.IsNullOrEmpty(iconPath))
\t\t{
\t\t\tDebug.LogError($"{nameof(HomeNpcBubbleTip)}.{nameof(SetNpc)}({npcClassId}) 没有找到 Npc 配置数据！", this);
\t\t\treturn this;
\t\t}
\t\tmIcon.sprite = BubbleServices.LoadNpcIcon(iconPath);
\t\tmLoadedIcons.Add(iconPath);
\t\treturn this;
\t}
}'''
    emit('HomeNpcBubbleTip', text)

    # Reuse the existing resolver only after proving that the complete method
    # closure used by this selected rack has the same original bodies.
    from prepare_unity_profession_appearance_resolver import adapt, method
    for function in (adapt, method, adapt.__globals__['clean'], adapt.__globals__['log_blocks']):
        dependency = Path(inspect.getsourcefile(function))
        inputs[dependency.relative_to(ROOT).as_posix()] = sha(dependency.read_bytes())
    original_resolver = read('AppearanceSystem.Runtime/AppearanceUtils')
    resolver_path = ROOT / 'unity/Assets/Game/Runtime/Official/ProfessionUI/ProfessionAppearanceResolver.cs'
    installed_resolver = resolver_path.read_text('utf-8-sig')
    resolver_methods = {}

    def normalized(text):
        return re.sub(r'\s+', '', re.sub(r'//[^\n]*', '', text))

    def implementations(text, name):
        matches = re.finditer(r'(?m)^\s*(?:public|private|protected)\s+(?:static\s+)?[^\n]+\b' + name + r'\(', text)
        return [method(text, match[0].lstrip()) for match in matches]

    for name in ('TryGetFinalSkinId', 'TryGetSkinId', 'TryGetAppendSkinId', 'TryGetEquipSkinId',
                 'IsSkinLimit', 'ResolveAppendAppearanceMutex', 'ResolveEffectiveSkin',
                 'PropagateAppendSkinState', 'DecideAppendSkinState', 'IsBlocking',
                 'KillBlockCycle', 'IsInBlockCycle', 'IsFilterSkinId', 'IsFilterSkinInfo'):
        original = [adapt(value).replace(' static ', ' ').replace('Parsers.Instance.', 'mTables.')
            .replace('AppendAppearanceInfoParser ', 'ProfessionAppendAppearanceInfoParser ')
            for value in implementations(original_resolver, name)]
        installed = implementations(installed_resolver, name)
        if not original or [normalized(value) for value in original] != [normalized(value) for value in installed]:
            raise ValueError('Selected weapon-rack resolver closure changed: ' + name)
        resolver_methods[name] = [sha(normalized(value).encode()) for value in original]
    original_nested = original_resolver[original_resolver.index('\tprivate enum AppendSkinState'):original_resolver.index('\tprivate static readonly Dictionary<int, string> sIdDict')]
    installed_nested = installed_resolver[installed_resolver.index('\tprivate enum AppendSkinState'):installed_resolver.index('\tpublic string GetSkinName(')]
    if normalized(adapt(original_nested)) != normalized(installed_nested):
        raise ValueError('Selected weapon-rack append-state types changed')
    resolver_methods['AppendSkinState/AppendSkinEntry'] = [sha(normalized(adapt(original_nested)).encode())]

    for port in sorted(TEMPLATES.glob('*.cs')):
        inputs[port.relative_to(ROOT).as_posix()] = sha(port.read_bytes())
        write(DEST / port.name, port.read_bytes())
        metadata(DEST / port.name)
    definition = DEST / 'Zjcs.Official.HomeFurniture.asmdef'
    write(definition, (json.dumps(dict(name='Zjcs.Official.HomeFurniture', references=[
        'Zjcs.Official.HomeUI', 'Zjcs.Official.HomeHudUI', 'Zjcs.Official.HomeSceneInput', 'Zjcs.Official.HomeMainUiControls', 'Zjcs.Official.ProfessionUI',
        'Zjcs.Official.WorldUI', 'spine-unity', 'spine-csharp']), indent=2) + '\n').encode())
    metadata(definition)
    metadata(DEST, True)
    RECEIPT.parent.mkdir(parents=True, exist_ok=True)
    validations = [ROOT / path for path in (
        'scripts/fixtures/home_furniture_controller_validation.cs',
        'scripts/fixtures/home_weapon_rack_controller_validation.cs',
        'scripts/fixtures/home_native_weapon_validation.cs',
        'scripts/fixtures/home_native_weapon_controller_validation.cs',
        'scripts/fixtures/home_native_weapon_catalog_validation.cs',
        'scripts/fixtures/home_appearance_availability_validation.cs',
        'scripts/test_home_furniture_controllers.ps1')]
    shared = [ROOT / path for path in (
        'unity/Assets/Game/Runtime/Official/HomeSceneInput/ClickableHomeObj.cs',
        'unity/Assets/Game/Runtime/Official/ProfessionUI/UIBase.cs',
        'unity/Assets/Game/Runtime/Official/WorldUI/WorldStateSwitcher.cs',
        'unity/Assets/Game/Runtime/Official/WorldUI/WorldMultiLanguageText.cs',
        'unity/Assets/Game/Runtime/Official/ProfessionUI/ProfessionAppearanceResolver.cs',
        'unity/Assets/Game/Runtime/Official/ProfessionUI/ProfessionAppearancePartPaths.cs',
        'unity/Assets/Game/Runtime/Official/ProfessionUI/BaseSkeletonFollower.cs',
        'unity/Assets/Game/Runtime/Official/HomeUI/EFurniture.cs',
        'unity/Assets/Game/Runtime/Official/HomeHudUI/HomePlayerUIBase.cs',
        'unity/Assets/Game/Runtime/Official/HomeHudUI/RedDotItemBase.cs')]
    RECEIPT.write_text(json.dumps(dict(manifest=source.version, inputs=inputs, outputs=outputs,
        validation_inputs={path.relative_to(ROOT).as_posix(): sha(path.read_bytes()) for path in validations},
        shared_dependencies={path.relative_to(ROOT).as_posix(): sha(path.read_bytes()) for path in shared},
        reused_appearance_method_closure=resolver_methods,
        generator_sha256=sha(Path(__file__).read_bytes()), adaptations=[
            'Original four-state priority, click branches, condition subscriptions and teardown are retained.',
            'Original notice transitions, time-offset callbacks, notice text keys and arguments are retained.',
            'Original repair effect delays and story-context reuse/disposal are retained.',
            'Original entity click sound keeps all child collider targets, input enable/disable subscriptions and emitter identity; decoded event audio is supplied by its owner.',
            'Weapon-rack appearance mutex/final-skin resolution and Home-specific part paths reuse the migrated original services.',
            'Weapon-rack change-only reload, zero/empty skin branches, follower resets and resource lifetime are retained.',
            'Original owner/visit/photo view-mode and named boolean action-event switches retain subscription lifetimes.',
            'Client globals and injected owners are explicit local ports; no fallback rules or story content are introduced.',
            'Original selected native click dispatcher, state switcher, UI lifecycle and localization component are reused.',
            'Decompiler casts are retained; logging calls use Unity logging instead of the client logging framework.'
        ], scope='Shared original furniture controllers and storage click action. Native prefab dependencies and local host binding are validated separately.'), ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    print('Restored original furniture runtime:', len(outputs), 'outputs')


if __name__ == '__main__':
    prepare()
