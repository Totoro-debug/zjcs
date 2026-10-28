"""Port complete player-info UI application branches with explicit data boundaries."""
import json
from pathlib import Path
import re
from prepare_unity_profession_photographer import ROOT,CODE,RUNTIME,record,save
from prepare_unity_profession_ui_parts import common
from prepare_unity_profession_player_appearance import binding


CONTEXT='''    private IProfessionPlayerInfoBindings mBindings;
    private IProfessionPlayerInfoBindings InfoBindings => mBindings ?? ProfessionPlayerInfoBindings.Required;
    public void ConfigureInfo(IProfessionPlayerInfoBindings bindings)
    { mBindings = bindings ?? throw new System.ArgumentNullException(nameof(bindings)); }

'''


def adapt_data(text):
    text=text.replace('ActorTargetData','IProfessionInfoTarget').replace('NpcTargetData','IProfessionInfoNpcTarget').replace('CultureTextArg NameArg','object NameArg')
    text=re.sub(r'\bAppearanceData (?=\w)','object ',text)
    text=re.sub(r'\bProfession (?=\w)','int ',text)
    text=re.sub(r'\bSubRank (?=\w)','int ',text)
    return text


def build():
    manifest_path=CODE.parent.parent/'code-manifest.json';manifest=json.loads(manifest_path.read_bytes())
    if manifest['manifest']!='871_156598':raise ValueError('Player info source version changed')
    assemblies=[]
    for name in ('Assembly-CSharp.dll','Console.dll'):
        path=CODE.parent/'managed/decoded'/name
        if record(path)['sha256']!=manifest['assemblies'][name]['decoded_sha256']:raise ValueError('Player info assembly changed')
        assemblies.append(record(path))
    ports=[]
    for name in ('ActorTargetType','TeamTargetCatalog'):
        source=CODE.parent/'common-source/Common'/(name+'.cs');text=common(source.read_text(encoding='utf8')).replace('namespace Common;\n','')
        ports.append(save(name,source,text))
    source=CODE/'PlayerInfoData.cs';text=common(source.read_text(encoding='utf8'));text=text[:text.index('\tpublic PlayerInfoData(Player player)')]+'}\n'
    ports.append(save('PlayerInfoData',source,adapt_data(text)))
    source=CODE/'PlayerInfoUI.cs';text=adapt_data(common(source.read_text(encoding='utf8'))).replace('IProfessionAppearancePartAsset','AssetOperationHandle<GameObject>')
    text=re.sub(r'^using (?:AppearanceSystem|R3);\n','',text,flags=re.M)
    text='using Zjcs.Official.WorldUI;\n'+text
    text=text.replace('MultiLanguageText','WorldMultiLanguageText').replace('StateSwitcher<','WorldStateSwitcher<')
    for name in ('PlayerAnimationAppearanceScene','HeadCharacterPortrait','HonorTitleItemProvider','PlanesIconItem'):
        text=re.sub(r'\b'+name+r' (?=\w)','Component ',text)
    text=text.replace('private DisposableBag mDisposableBag;','private ProfessionResourceOperationBag mDisposableBag = new ProfessionResourceOperationBag();').replace('\t\tmDisposableBag = default;\n','')
    text=binding(text,'\t[Inject]','\tpublic PlayerInfoData PlayerInfoData',CONTEXT)
    text=text.replace('new CultureTextArg(mPlayerInfoData.Name, mPlayerInfoData.TargetData.IsNpc)','InfoBindings.CreateNameArg(mPlayerInfoData.Name, mPlayerInfoData.TargetData.IsNpc)')
    text=text.replace('ProfessionSettings.Value.GetFullBodyImg(mPlayerInfoData.Profession, mPlayerInfoData.Sex).AssetGuid','InfoBindings.GetFullBodyGuid(mPlayerInfoData.Profession, mPlayerInfoData.Sex)')
    text=re.sub(r'Disposable.AddTo<AssetOperationHandle<(Sprite|GameObject)>>\((.*?), ref mDisposableBag\)',r'mDisposableBag.Add(\2)',text)
    text=text.replace('ResourceLoader.LoadAssetByGuid<Sprite>','InfoBindings.Resources.LoadAssetByGuid<Sprite>').replace('ResourceLoader.LoadSprite(','InfoBindings.Resources.LoadAsset<Sprite>(').replace('ResourceLoader.LoadPrefab(','InfoBindings.Resources.LoadAsset<GameObject>(')
    text=text.replace('MeManager.MainPlayer','InfoBindings.MainPlayer')
    text=text.replace('PlayerAppearanceScene.RefreshAppearance(','((IProfessionPlayerInfoScene)PlayerAppearanceScene).RefreshAppearance(')
    text=text.replace('HonorTitleItemProvider.ClearTitleItem()','((IProfessionPlayerInfoTitle)HonorTitleItemProvider).ClearTitleItem()').replace('HonorTitleItemProvider.Init(','((IProfessionPlayerInfoTitle)HonorTitleItemProvider).Init(')
    text=text.replace('!appearanceData.HonorItemDict.TryGetValue(HonorType.Title, out var _)','!InfoBindings.HasHonorTitle(appearanceData)')
    text=text.replace('NpcInfo npcInfo = Parsers.Instance.NpcInfoParser.Get(npcClassId);','ProfessionInfoNpcDefinition npcInfo = InfoBindings.GetNpc(npcClassId);')
    text=text.replace('UIUtils.Instantiate(result, NpcAppearanceSlot.transform, EUIInstantiateFlag.KeepScale)','InfoBindings.InstantiateKeepScale(result, NpcAppearanceSlot.transform)').replace('NpcUtils.TryOverrideNpcFigureModelInitAnimationPlay(','InfoBindings.OverrideNpcInitialAnimation(')
    text=text.replace('PlayerHeadPortrait.GeneratePortrait(','((IProfessionPlayerInfoHead)PlayerHeadPortrait).GeneratePortrait(')
    a=text.index('\t\t\tGLoggerErrorInterpolatedStringHandler');b=text.index('\n\t\t}',a)
    text=text[:a]+'\t\t\tDebug.LogError("肖像生成失败. 玩家ID为" + mPlayerInfoData.Id + "\\n" + exception);'+text[b:]
    text=text.replace('LanguageUtils.GetEnumKey(mPlayerInfoData.SubRank)','InfoBindings.SubRankKey(mPlayerInfoData.SubRank)').replace('CultureNumberArg.Compact(','InfoBindings.CompactNumber(')
    text=text.replace('PlanesIconItem.Init(','((IProfessionPlayerInfoPlanesIcon)PlanesIconItem).Init(')
    text=text.replace('LanguageUtils.TextKeyArgWrap playerNameWithPlanesNameText = PlanesNameProvider.GetPlayerNameWithPlanesNameText(','ProfessionInfoText playerNameWithPlanesNameText = InfoBindings.NameWithPlanes(')
    text=text.replace('ProfessionBaseInfoParser professionBaseInfoParser = Parsers.Instance.ProfessionBaseInfoParser;','IProfessionPlayerInfoBindings professionBaseInfoParser = InfoBindings;').replace('ProfessionBaseInfoParser parser','IProfessionPlayerInfoBindings parser')
    text=text.replace('parser.GetThirdIconPath(','parser.ThirdProfessionIcon(').replace('parser.GetFirstIconPath(','parser.FirstProfessionIcon(')
    text=text.replace('Profession.None','0').replace('LanguageUtils.GetProfessionNameKey(','InfoBindings.ProfessionNameKey(')
    text=text.replace('InfoBindings.MainPlayer.IsFunctionLimit(FunctionType.PinNpc)','InfoBindings.MainPlayer.IsNpcPinLimited').replace('InfoBindings.MainPlayer.NpcManager.GetByNpcClassId(npcClassId)?.IsCollected','InfoBindings.MainPlayer.IsNpcCollected(npcClassId)')
    text=text.replace('((Component)this).GetComponentInParent<TeamInviteSubPanelBase>()?.TeamTarget?.TargetCatalog','InfoBindings.TeamCatalog(this)').replace('JointStrikeUtils.CanJointTogether(','InfoBindings.CanJointTogether(')
    text=text.replace('PlayerRobotInvite playerRobotInvite = ((Component)this).GetComponentInParent<TeamInviteRobotPanel>()?.RobotInviteCfg;','ProfessionInfoRobotInvite playerRobotInvite = InfoBindings.RobotInvite(this);').replace('Parsers.Instance.PlayerRobotInviteParser.Get(RobotInvitePlayType.All)','InfoBindings.DefaultRobotInvite()')
    ports.append(save('PlayerInfoUI',source,text))
    source=CODE/'PlayerIconMe.cs';text=adapt_data(common(source.read_text(encoding='utf8')))
    text='using System;\n'+text
    text=text.replace('public PlayerIconMeHpSlider IconMeHpSlider;','public Component IconMeHpSlider;').replace('PlayerInfoPanelBase.PanelPopupSceneType','int').replace('PlayerIconStatus.IconStatus','int')
    text=re.sub(r'\bPlayerLiteInfo (?=\w)','object ',text)
    text=text.replace('\tprivate int mStatus;',CONTEXT+'\tprivate int mStatus;')
    text=text.replace('IconMeHpSlider.Init()','((IProfessionPlayerInfoHpSlider)IconMeHpSlider).Init()').replace('InfoUI.PlayerHeadPortrait.ResetInitState()','((IProfessionPlayerInfoHead)InfoUI.PlayerHeadPortrait).ResetInitState()')
    text=text.replace('MeManager.Instance.OnPlayerUpdate','InfoBindings.OnPlayerUpdate').replace('MeManager.Instance != null','InfoBindings.ManagerAvailable').replace('MeManager.MainPlayer','InfoBindings.MainPlayer')
    text=text.replace('new PlayerInfoData(InfoBindings.MainPlayer)','InfoBindings.BuildData(InfoBindings.MainPlayer)').replace('new PlayerInfoData(mPlayerLiteInfo, mTargetData)','InfoBindings.BuildLiteData(mPlayerLiteInfo, mTargetData)')
    text=text.replace('StageType.PlayerDuel.GetSkillGroupId(-1L, -1L)','InfoBindings.DuelSkillGroup(-1L, -1L)').replace('WindowManager.Instance.Popup(this, (PlayerDetailInfoPanel panel) =>','InfoBindings.Popup(this, panel =>').replace('new PlayerBriefInfo(InfoBindings.MainPlayer, isOnline: true)','InfoBindings.BuildBriefInfo(InfoBindings.MainPlayer, online: true)')
    ports.append(save('PlayerIconMe',source,text))
    for port in ports:
        output=(ROOT/port['runtime']['path']).read_text(encoding='utf8')
        for token in ('Parsers.Instance','MeManager.','WindowManager.','[Inject]','GLogger','Disposable.AddTo','AssetOperationHandle<','CultureTextArg','LanguageUtils.','ProfessionSettings.Value'):
            if token in output:raise ValueError('Unresolved original player info endpoint: '+token)
    bindings=RUNTIME/'ProfessionPlayerInfoBindings.cs';validation=ROOT/'unity/Assets/Game/Editor/ProfessionUI/ProfessionPlayerInfoValidation.cs'
    proof=dict(format=1,manifest='871_156598',code_manifest=record(manifest_path),assemblies=assemblies,ports=ports,generator=record(Path(__file__)),
        bindings=[record(p) for p in (bindings,bindings.with_name(bindings.name+'.meta'))],validation=[record(p) for p in (validation,validation.with_name(validation.name+'.meta'))],
        dependencies=[record(ROOT/'assets/reference/profession-ui/player-appearance-source-manifest.json'),record(ROOT/'assets/reference/profession-ui/image-resource-source-manifest.json')],
        operation='Copy complete PlayerInfoUI and PlayerIconMe application methods/fields and both consumed target/context enums. Scope PlayerInfoData to its original consumed field projection and Id getter; full/lite/brief/guard data constructors remain upstream provider boundaries, not fabricated constructors. Protocol appearance/lite data remain opaque original references and SubRank/Profession/status/context values remain integers. Preserve every player/NPC/self/UI/scene/head/title/planes/collection/pin/cooldown branch and event/window/resource lifecycle. Original state switchers and resource disposal bag are reused.',
        validated_scope='Unity 2022.3.57f1c2 Play Mode and simulated mouse down/up passed data/appearance reference identity, refresh-state order, all nine event routes including original repeated-subscription behavior, init/enable/disable/reset and actual Start AutoInit, self/UI/scene/head/NPC routing, title/planes/text/icons, collection/pin/joint/assist branches, retained handles and destruction disposal, and delayed detail-popup callback. D3D button fixture screenshot inspected and pointer raycast checked; it is not source page artwork. Player/config/resource/target-data producers and child widgets are fixtures; production services and complete profession UI remain unbound.')
    (ROOT/'assets/reference/profession-ui/player-info-source-manifest.json').write_text(json.dumps(proof,ensure_ascii=False,indent=2)+'\n',encoding='utf8',newline='\n')
    print('Ported complete player-info UI and self icon application branches.')


if __name__=='__main__':build()
