"""Port the selected complete player-panel controllers, preserving original branches."""
import hashlib
import json
from pathlib import Path
import re
import subprocess

from prepare_home_experience_runtime import CODE, ROOT, substitute

DEST = ROOT / 'unity/Assets/Game/Runtime/Official/HomePlayerPanelUI'
HEADER = '// Selected original player-panel implementation; typed service boundaries only.\n'


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def metadata(path, folder=False):
    meta = Path(str(path)+'.meta')
    if not meta.exists():
        key = hashlib.md5(('zjcs:home-player-panel:'+path.relative_to(ROOT).as_posix()).encode()).hexdigest()
        importer = 'folderAsset: yes\nDefaultImporter:\n' if folder else ('AssemblyDefinitionImporter:\n' if path.suffix == '.asmdef' else 'MonoImporter:\n  serializedVersion: 2\n')
        meta.write_text('fileFormatVersion: 2\nguid: '+key+'\n'+importer+'  externalObjects: {}\n', 'utf8', newline='\n')
    return meta


def resolved_duration(inputs):
    source = CODE/'client-source-resolved/BuffItemDurationDecorator.cs'
    proof = source.with_suffix('.decompilation.json')
    dotnet = ROOT/'.tools/dotnet/dotnet.exe'
    ilspy = ROOT/'.tools/ilspycmd/tools/net10.0/any/ilspycmd.dll'
    references = CODE.parent/'native-conversion/inputs/Managed'
    assembly = CODE/'managed/decoded/Assembly-CSharp.dll'
    hashes = {p.relative_to(ROOT).as_posix():digest(p) for p in [assembly,ilspy,*sorted(references.glob('*.dll'))]}
    if not source.exists() or not proof.exists():
        command = [str(dotnet),str(ilspy),'-t','BuffItemDurationDecorator','-r',str(references),str(assembly)]
        result = subprocess.run(command,check=True,stdout=subprocess.PIPE,stderr=subprocess.PIPE)
        text = result.stdout.decode('utf-8-sig').replace('\r\n','\n')
        if 'private async UniTask OnShowTimeDown' not in text or 'IAsyncStateMachine' in text:
            raise ValueError('Resolve original duration coroutine dependencies.')
        source.parent.mkdir(parents=True,exist_ok=True)
        source.write_text(text,'utf8',newline='\n')
        proof.write_text(json.dumps(dict(inputs=hashes,sha256=digest(source),command=command),indent=2)+'\n','utf8',newline='\n')
    evidence = json.loads(proof.read_bytes())
    if evidence['inputs'] != hashes or evidence['sha256'] != digest(source):
        raise ValueError('Original duration decompilation changed.')
    for p in (source,proof): inputs[p.relative_to(ROOT).as_posix()] = digest(p)
    inputs.update(hashes)
    return source.read_text('utf8')


def build():
    DEST.mkdir(parents=True,exist_ok=True)
    inputs,outputs = {},{}

    def read(relative):
        p = CODE/relative
        inputs[p.relative_to(ROOT).as_posix()] = digest(p)
        return p.read_text('utf-8-sig').replace('\r\n','\n')

    def write(name,text):
        p = DEST/(name+'.cs')
        if p.exists() and not p.read_text('utf8').startswith(HEADER):
            raise ValueError('Preserve unrelated player-panel source: '+str(p))
        content=HEADER+'''using System;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Zjcs.Official.HomeHudUI;
using Zjcs.Official.HomePlayerInfoUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
namespace Zjcs.Official.HomePlayerPanelUI
{
using Object = UnityEngine.Object;
using MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;
'''+text+'\n}\n'
        content='\n'.join(line.rstrip() for line in content.splitlines())+'\n'
        if not p.exists() or p.read_bytes()!=content.encode('utf8'):
            p.write_text(content,'utf8',newline='\n')

    names = ('PublicPlayerInfoUI','MainPlayerHpImage','PlayerIconMeHpSlider','PlayerHpRedPoint',
             'PlayerBlessLevelViewController','PlayerBuffContentUIController','PlayerBuffContentUI',
             'PlayerBuffStatusController','BuffItemUI','FightStatusItemBase','FightStatusUIItem',
             'BuffItemBaseDecorator','BuffItemDurationDecorator','IPlayerStatusProvider','BuffItemUIUtils')
    for name in names:
        text = read('client-source/'+name+'.cs')
        if name == 'BuffItemDurationDecorator': text = resolved_duration(inputs)
        for line in ('using Common;\n','using Framework;\n','using Framework.DependencyInjection;\n','using R3;\n',
                     'using Framework.Logging;\n','using Framework.Logging.InterpolatedStringHandler;\n'):
            text = text.replace(line,'')
        text = re.sub(r'^\s*//IL_[^\n]*\n','\n',text,flags=re.M)
        text = text.replace('MeManager.MainPlayer','PanelBindings.MainPlayer')
        text = text.replace(' : PlayerUIBase',' : HomePlayerPanelUIBase')
        text = text.replace('StateSwitcher<','WorldStateSwitcher<')
        text = text.replace('((UnityEvent)mClickBtn.onClick)','mClickBtn.onClick').replace('((UnityEvent)mBuffBtn.onClick)','mBuffBtn.onClick')
        text = text.replace('EntryDetailDialog.EntryDetailData','HomeEntryDetailData')
        text = text.replace('PlayerStatusEffectDataWrap','IHomePanelStatusEffect')
        if name == 'PlayerIconMeHpSlider':
            text = substitute(text,' : HomePlayerPanelUIBase',' : HomePlayerPanelUIBase, IProfessionPlayerInfoHpSlider',1)
        if name == 'PlayerHpRedPoint':
            text = substitute(text,' : RedDotItemBase',' : HomePanelRedDotItemBase',1)
            for before,after in [('PlayerItemDataWrap itemData','HomeExperienceItem itemData'),('AwardSourceData sourceData','object sourceData'),
                ('itemData.Info','PanelBindings.GetItemInfo(itemData.Id)'),('ItemType.Cooking','HomePanelItemType.Cooking'),
                ('CookingInfo','HomePanelCookingInfo'),('Parsers.GameSettings.PlayerHpRedDotThreshold','PanelBindings.PlayerHpRedDotThreshold')]:
                text = substitute(text,before,after)
        if name == 'PlayerBlessLevelViewController':
            pos = text.index('{')+1
            text = text[:pos]+'''
    private IHomePlayerPanelBindings PanelBindings => HomePlayerPanelScope.Required(this);
'''+text[pos:]
            for before,after in [('IntroductionPopBtn','HomeIntroductionPopBtn'),('BlessPropBtn','ProfessionBlessPropBtn'),
                ('mProfessionBlessPropBtn','mBlessPropBtn'),('PlayerBriefInfo','IHomePanelPlayerBrief'),('Player mainPlayer','IHomePanelPlayer mainPlayer'),
                ('Rank pre, Rank cur','int pre, int cur'),('LanguageUtils.TextKeyArgWrap','HomeInfoTextKey'),
                ('LanguageUtils.GetSeasonLevelUnEffectDesc','PanelBindings.GetSeasonLevelUnEffectDesc'),
                ('PlanesInfoUtils.PlanesRank','PanelBindings.PlanesRank'),('mainPlayer.PlayerPropCenter.','mainPlayer.'),
                ('LanguageManager.Instance.GetTextFormated','PanelBindings.TextFormatted'),('LanguageManager.Instance.GetText','PanelBindings.Text')]:
                text = substitute(text,before,after)
        if name == 'PlayerBuffContentUI':
            text = substitute(text,'[Inject]\n\tprivate IResourceLoader ResourceLoader { get; set; }','private IHomePanelResourceLoader ResourceLoader => PanelBindings.Resources;',1)
            text = substitute(text,'new PlayerBuffStatusController(ResourceLoader,','new PlayerBuffStatusController(PanelBindings, ResourceLoader,',1)
            start = text.index('\t\t\tWindowManager.Instance.Popup(')
            end = text.index('\n\t\t}',start)
            text = text[:start]+'\t\t\tPanelBindings.PopupEntries(this, entryDatas);'+text[end:]
        if name == 'PlayerBuffStatusController':
            text = substitute(text,'IResourceLoader','IHomePanelResourceLoader')
            text = substitute(text,'public PlayerBuffStatusController(','private readonly IHomePlayerPanelBindings PanelBindings;\n\n\tpublic PlayerBuffStatusController(IHomePlayerPanelBindings bindings, ',1)
            text = substitute(text,'mDisposableBag = default;','PanelBindings = bindings ?? throw new ArgumentNullException(nameof(bindings));\n\t\tmDisposableBag = default;',1)
            text = substitute(text,'BuffItemUIUtils.CreateBuffItemEntryDetailData(value)','BuffItemUIUtils.CreateBuffItemEntryDetailData(PanelBindings, value)',1)
            text = substitute(text,'Disposable.AddTo<AssetOperationHandle<GameObject>>','HomePanelDisposable.AddTo<IHomeHudAssetHandle<GameObject>>',1)
        if name == 'BuffItemUI':
            pos=text.index('{')+1
            text=text[:pos]+'\n    private IHomePlayerPanelBindings PanelBindings => HomePlayerPanelScope.Required(this);\n'+text[pos:]
            text=substitute(text,'EntryInfo entryInfo = Parsers.Instance.EntryInfoParser.Get(entryId);','HomePanelEntryInfo entryInfo = PanelBindings.GetEntry(entryId);',1)
            start=text.index('\t\tWindowManager.Instance.Popup(')
            text=text[:start]+'''\t\tPanelBindings.PopupEntry(this, () => BuffItemUIUtils.CreateBuffItemEntryDetailData(PanelBindings, mStatusEffectData));
\t}
}
'''
        if name == 'FightStatusItemBase':
            start=text.index('\t[Inject]')
            end=text.index('\n\tprivate void OnEnable()',start)
            text=text[:start]+'''\tprotected IHomePanelResourceLoader ResourceLoader => HomePlayerPanelScope.Required(this).Resources;
'''+text[end:]
        if name == 'FightStatusUIItem':
            text=substitute(text,'Disposable.AddTo<AssetOperationHandle<Sprite>>','HomePanelDisposable.AddTo<IHomeHudAssetHandle<Sprite>>')
        if name == 'BuffItemDurationDecorator':
            text=substitute(text,'TimeManager.Instance.GetDateTime()','HomePlayerPanelScope.Required(this).GetDateTime()',1)
        text=text.replace('DisposableBag','HomePanelDisposableBag').replace('CancellationDisposable','HomePanelCancellationDisposable')
        # Keep the original field name for diagnostics and source comparison.
        text=text.replace('mHomePanelDisposableBag','mDisposableBag').replace('mHomePanelCancellationDisposable','mCancellationDisposable')
        if name == 'BuffItemUIUtils':
            text=substitute(text,'(IHomePanelStatusEffect statusEffectData)','(IHomePlayerPanelBindings PanelBindings, IHomePanelStatusEffect statusEffectData)')
            text=substitute(text,'CreateBuffItemEntryDetailDataById(int entryId','CreateBuffItemEntryDetailDataById(IHomePlayerPanelBindings PanelBindings, int entryId',1)
            text=text.replace('StatusEffectInfo info','IHomePanelStatusInfo info').replace('EntryInfo entryInfo = Parsers.Instance.EntryInfoParser.Get(entryId);','HomePanelEntryInfo entryInfo = PanelBindings.GetEntry(entryId);')
            for before,after in [('LanguageUtils.TextKeyArgWrap','HomePanelTextKeyArgWrap'),('LanguageUtils.GetEntryNameKey','PanelBindings.GetEntryNameKey'),
                ('LanguageUtils.GetEntryDescKey','PanelBindings.GetEntryDescKey'),('LanguageUtils.GetStatusEffectVaildShowScopesName','PanelBindings.GetStatusEffectVaildShowScopesName'),
                ('LanguageUtils.TryGetCookingBuffEntryDesc','PanelBindings.TryGetCookingBuffEntryDesc'),('LanguageManager.Instance.GetTextFormated','PanelBindings.TextFormatted')]:
                text=substitute(text,before,after)
            text=substitute(text,'GetStatusEffectDataAdditionInfo(statusEffectData)','GetStatusEffectDataAdditionInfo(PanelBindings, statusEffectData)',1)
            text=substitute(text,'GetDurationStatusEffectDataAdditionInfo(statusEffectData)','GetDurationStatusEffectDataAdditionInfo(PanelBindings, statusEffectData)',1)
            start=text.index('\t\t\tGLoggerErrorInterpolatedStringHandler')
            end=text.index('\n\t\t\treturn null;',start)
            text=text[:start]+'\t\t\tUnityEngine.Debug.LogError("Unknown original status icon type: " + statusEffectData.Info.IconType);'+text[end:]
        write(name,text)
    for name,rel in [('StatusEffectIconType','common-source/Common'),('EStatusSourceType','common-source/Common'),('CookingType','common-source/Common'),
                     ('EntryDetailAdditionInfoType','client-source/Common'),('EntryDetailAdditionBaseInfo','client-source/Common'),('EntryDetailAdditionEndTimeInfo','client-source/Common')]:
        write(name,substitute(read(rel+'/'+name+'.cs'),'namespace Common;\n','',1))
    write('PreviewTool',read('framework-source/PreviewTool.cs'))
    text=read('client-source/RedDotItemBase.cs')
    for line in ('using Common;\n','using Framework.DependencyInjection;\n'):text=text.replace(line,'')
    text=text.replace('RedDotItemBase','HomePanelRedDotItemBase').replace(' : PlayerUIBase',' : HomePlayerPanelUIBase')
    text=substitute(text,'[Inject]\n\tprivate TipDotSystem TipDotSystem { get; set; }','private IProfessionTipDotSystem TipDotSystem => PanelBindings.TipDotSystem;',1)
    text=text.replace('TipDotSystem tipDotSystem = TipDotSystem;','IProfessionTipDotSystem tipDotSystem = TipDotSystem;')
    write('HomePanelRedDotItemBase',text)
    text=read('common-source/Common/LanguageUtils.cs')
    start=text.index('\tpublic struct TextKeyArgWrap');end=text.index('\n\tpublic ',start+10)
    text=text[start:end].replace('TextKeyArgWrap','HomePanelTextKeyArgWrap')
    text=substitute(text,'Singleton<HomePanelTextKeyArgWrap>.Create(() => new HomePanelTextKeyArgWrap(""))','new HomePanelTextKeyArgWrap("")',1)
    write('HomePanelTextKeyArgWrap',text)
    # Reuse the previously verified R3-compatible lifecycle primitives without
    # pulling the unrelated map-window assembly into this UI module.
    for rel,old,new in [('World/Map13MiniMapMarkSpawn/MiniMapSpawnDisposableBag.cs','MiniMapSpawnDisposableBag','HomePanelDisposableBag'),
                        ('World/Map13MiniMapWindows/CancellationDisposable.cs','CancellationDisposable','HomePanelCancellationDisposable')]:
        source=ROOT/'unity/Assets/Game/Runtime/Official'/rel
        inputs[source.relative_to(ROOT).as_posix()]=digest(source)
        text=source.read_text('utf8')
        start=text.index('public '+('struct ' if old.endswith('Bag') else 'sealed class ')+old)
        text=text[start:text.rfind('}')].strip().replace(old,new)
        write(new,'using System.Threading;\n'+text)
    assembly=dict(name='Zjcs.Official.HomePlayerPanelUI',references=['Zjcs.Official.HomePlayerInfoUI','Zjcs.Official.HomeHudUI',
        'Zjcs.Official.ProfessionUI','Zjcs.Official.WorldUI','Zjcs.Official.World.NativeLabelUI','Zjcs.Official.World.NativeText','Unity.TextMeshPro','Unity.ugui','UniTask'])
    (DEST/'Zjcs.Official.HomePlayerPanelUI.asmdef').write_text(json.dumps(assembly,indent=2)+'\n','utf8',newline='\n')
    for path in (DEST,*sorted(DEST.iterdir())):
        if path.suffix=='.meta':continue
        meta=metadata(path,path.is_dir())
        for file in ((meta,) if path.is_dir() else (path,meta)):outputs[file.relative_to(ROOT).as_posix()]=digest(file)
    for path in (ROOT/'unity/Assets/Game/Runtime/Offline/HomeUI/OfflineHomePlayerPanel.cs',
                 ROOT/'unity/Assets/Game/Runtime/Offline/HomeUI/Zjcs.Offline.HomeUI.asmdef'):
        for file in (path,metadata(path)):outputs[file.relative_to(ROOT).as_posix()]=digest(file)
    path=CODE/'client-source/EntryDetailDialog.cs'
    inputs[path.relative_to(ROOT).as_posix()]=digest(path)
    return dict(sources=inputs,outputs=outputs)


if __name__=='__main__':
    print(json.dumps(build(),ensure_ascii=False,indent=2))
