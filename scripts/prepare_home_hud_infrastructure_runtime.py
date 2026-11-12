"""Port the original right-entry and download UI without changing lifecycle branches."""
import hashlib
import json
from pathlib import Path
import re

from prepare_home_auto_increase_runtime import ROOT,CODE,sha,metadata

DEST=ROOT/'unity/Assets/Game/Runtime/Official/HomeHudInfrastructureUI'
PORTS='''using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Zjcs.Official.HomeHudUI;
using Zjcs.Official.HomeFunctionMenuUI;
using Zjcs.Official.HomePlayerPanelUI;
namespace Zjcs.Official.HomeHudInfrastructureUI
{
    public interface IHomeHudActivityData { ActivityType ActivityType { get; } int Id { get; } }
    public interface IHomeHudRightEntryServices
    {
        event Action<ActivityType> OnActivitiesUpdate;
        event Action<IHomeHudActivityData> OnActivityPreview;
        event Action<IHomeHudActivityData> OnActivityOpen;
        event Action<IHomeHudActivityData> OnActivityClose;
        IReadOnlyList<HomeRightEntryUIActivityInfo> GetSortedEntries();
        bool TryGetActivity(ActivityType type,int id,out IHomeHudActivityData activity);
        bool TryGetActivityByType(ActivityType type,out Dictionary<int,IHomeHudActivityData> activities);
        IHomeMenuPrefabHandle LoadPrefab(string path);
        // The host binds dependencies before Awake/OnEnable; original ResetUI
        // follows the scene-container Instantiate(prefab,parent,false).
        GameObject InstantiatePrefab(GameObject prefab,Transform parent);
    }
    public interface IHomeHudDownloadPlayer
    {
        bool HasCollectedOnceReward(string type);
        event Action<string> OnCollectOnceReward;
    }
    public interface IHomeHudDownloadHandler
    {
        PaydState CurrentState { get; }
        long CurrentDownloadBytes { get; }
        long TotalDownloadBytes { get; }
    }
    public interface IHomeHudDownloadProgressDialog { void Init(bool allowClickClose); }
    public interface IHomeHudDownloadServices
    {
        IHomeHudDownloadPlayer Player { get; }
        IHomeHudDownloadHandler PaydHandler { get; }
        bool EnablePlayAsYouDownload { get; }
        int CheckConditionId { get; }
        IHomeHudCondition GetCondition(int id);
        IHomeHudConditionHandler ConditionHandler { get; }
        bool IsMet(IHomeHudCondition condition);
        UnityEngine.Object MapRootIdentity { get; }
        UniTask LeaveMap();
        bool IsSplashHidden { get; }
        void PopupProgress(Action<IHomeHudDownloadProgressDialog> initialize);
        UniTask PopupProgressGlobal(Action<IHomeHudDownloadProgressDialog> initialize,bool instant);
    }
    public static class HomeHudInfrastructureUtils
    {
        public static IHomeMenuPrefabHandle LoadPrefab(IHomeHudRightEntryServices services,string path,ref HomePanelDisposableBag bag)
        { var handle=services.LoadPrefab(path);bag.Add(handle);return handle; }
        public static GameObject Instantiate(IHomeHudRightEntryServices services,GameObject prefab,Transform parent)
        { var result=services.InstantiatePrefab(prefab,parent);HomeMenuUIUtils.ResetUI(result);return result; }
    }
}
'''


def build():
    inputs,outputs={},{}
    def read(path,verify=False):
        if verify:
            proof=path.with_suffix('.decompilation.json');data=json.loads(proof.read_bytes());assert sha(path)==data['sha256'],path
            for relative,digest in data['inputs'].items():assert sha(ROOT/relative)==digest,relative
            inputs[proof.relative_to(ROOT).as_posix()]=sha(proof)
        inputs[path.relative_to(ROOT).as_posix()]=sha(path);return path.read_text('utf-8-sig')
    def write(name,text):
        path=DEST/name;path.parent.mkdir(parents=True,exist_ok=True);path.write_text(text,'utf8',newline='\n')
        for item in (path,metadata(path)):outputs[item.relative_to(ROOT).as_posix()]=sha(item)
    prefix='using Zjcs.Official.HomePlayerPanelUI;\nusing Zjcs.Official.HomeHudUI;\nusing MultiLanguageText=Zjcs.Official.WorldUI.WorldMultiLanguageText;\n'
    for name in ('HomeUIActivityEntryController','PlayAsYouDownloadEntry'):
        text=read(CODE/'client-source-resolved'/(name+'.cs'),True)
        for using in ('Common','Framework','Framework.DependencyInjection','R3','GFrameCore.Extensions.ZLogger','GameFramework','MessagePipe','Microsoft.Extensions.Logging','PlayAsYouDownload','ZLogger'):
            text=text.replace('using '+using+';\n','')
        text=re.sub(r'\t\[Inject\]\n\tprivate [^\n]+ \{ get; set; \}\n','',text)
        text=text.replace('internal class HomeUIActivityEntryController','public class HomeUIActivityEntryController')
        text=text.replace('R3.DisposableBag','HomePanelDisposableBag').replace('private DisposableBag','private HomePanelDisposableBag')
        services='IHomeHudRightEntryServices' if name=='HomeUIActivityEntryController' else 'IHomeHudDownloadServices'
        at=text.index('\n{',text.index('public class'))+2;text=text[:at]+'\n\tpublic '+services+' Services { private get; set; }\n'+text[at:]
        if name=='HomeUIActivityEntryController':
            text=re.sub(r'\tprivate HomeRightEntryUIActivityInfoParser HomeRightEntryUIActivityInfoParser => [^\n]+\n','',text)
            text=text.replace('HomeRightEntryUIActivityInfoParser.GetSortedEntries()','Services.GetSortedEntries()')
            text=text.replace('ActivityDataManager.Instance.','Services.').replace('ActivityData','IHomeHudActivityData')
            text=text.replace('private static bool IsActivityOpen','private bool IsActivityOpen')
            text=text.replace('UIUtils.Instantiate(ResourceLoader.LoadPrefab(info.EntryPath).AddTo(ref mDisposableBag).Result, mEntryRoot)',
                'HomeHudInfrastructureUtils.Instantiate(Services, HomeHudInfrastructureUtils.LoadPrefab(Services, info.EntryPath, ref mDisposableBag).Result, mEntryRoot)')
        else:
            text=text.replace('ICondition','IHomeHudCondition')
            # Logging implementations are platform boundaries; retain their
            # original guard and every gameplay statement after the log.
            start=text.index('\t\t\tMicrosoft.Extensions.Logging.ILogger logger = Logger;')
            end=text.index('\n\t\t}',start)
            text=text[:start]+'\t\t\tDebug.LogError("PlayAsYouDownloadEntry 配置错误！mHeightMax:"+mHeightMax+" > mHeightMin:"+mHeightMin);'+text[end:]
            start=text.index('\t\tMicrosoft.Extensions.Logging.ILogger logger = Logger;')
            end=text.index('\t\tMapLeaveHandler.LeaveMap().Forget();',start)
            text=text[:start]+text[end:]
            text=text.replace('MeManager.MainPlayer','Services.Player')
            text=text.replace('ChannelManager.Instance.ReserveObject.EnablePlayAsYouDownload','Services.EnablePlayAsYouDownload')
            text=text.replace('PaydHandler','Services.PaydHandler')
            for before,after in [('ProgressSubscribe.Subscribe(OnProgressUpdate).AddTo(ref mDisposableBag)','mDisposableBag.Add(Services.SubscribeProgress(OnProgressUpdate))'),
                ('StateSubscribe.Subscribe(OnStateUpdate).AddTo(ref mDisposableBag)','mDisposableBag.Add(Services.SubscribeState(OnStateUpdate))'),
                ('Parsers.GameSettings.PlayAsYouDownloadCheckConditionId','Services.CheckConditionId'),
                ('Parsers.Instance.ConditionInfoParser.GetCondition','Services.GetCondition'),
                ('ConditionHandler.Instance','Services.ConditionHandler'),
                ('mCheckPointCondition.IsMet(Services.Player)','Services.IsMet(mCheckPointCondition)'),
                ('MapRootManagerProxy.MapRoot','Services.MapRootIdentity'),('MapLeaveHandler.LeaveMap()','Services.LeaveMap()'),
                ('LoadingPanelManager.Instance.SplashStatus == SplashStatus.Hide','Services.IsSplashHidden')]:
                assert before in text,before;text=text.replace(before,after)
            text=text.replace('WindowManager.Instance.Popup((PlayAsYouDownloadProgressDialog dialog)',
                'Services.PopupProgress((IHomeHudDownloadProgressDialog dialog)',1)
            text=text.replace('WindowManager.Instance.Popup((PlayAsYouDownloadProgressDialog dialog)',
                'Services.PopupProgressGlobal((IHomeHudDownloadProgressDialog dialog)')
            text=text.replace('}, instant: false, WindowLifeType.Global).ToUniTask();','}, instant: false);')
        assert not re.search(r'\b(?:Inject|MeManager|Parsers|ResourceLoader|ActivityDataManager|R3|WindowManager|Logger|ProgressSubscribe|StateSubscribe|MapLeaveHandler|MapRootManagerProxy)\b',text),name
        assert 'Unknown result type' not in text and 'IAsyncStateMachine' not in text,name
        write(name+'.cs','// Original selected home HUD infrastructure; explicit local service bindings.\n'+prefix+'namespace Zjcs.Official.HomeHudInfrastructureUI\n{\n'+text+'}\n')
    text=read(CODE/'common-source/Common/ActivityType.cs').replace('namespace Common;','namespace Zjcs.Official.HomeHudInfrastructureUI\n{')
    write('ActivityType.cs',text+'}\n')
    for name in ('PaydState','PlayAsYouDownloadProgressUpdateEvent','PlayAsYouDownloadStateUpdateEvent'):
        text=read(CODE/'launch-source/PlayAsYouDownload'/(name+'.cs')).replace('namespace PlayAsYouDownload;','namespace Zjcs.Official.HomeHudInfrastructureUI\n{')
        write(name+'.cs',text+'}\n')
    # Retain every consumed original config field and the original sorting
    # implementation. Luban deserialization is performed by the verified
    # constructor tool; nullable values remain distinct from numeric zero.
    text=read(CODE/'common-source/Common/HomeRightEntryUIActivityInfo.cs')
    start=text.index('\tpublic readonly int Id;');end=text.index('\n\tpublic const int __ID__',start)
    fields=text[start:end]
    write('HomeRightEntryUIActivityInfo.cs','namespace Zjcs.Official.HomeHudInfrastructureUI\n{\npublic sealed class HomeRightEntryUIActivityInfo\n{\n'+fields+'''
    public HomeRightEntryUIActivityInfo(int id,int sortedId,string entryPath,bool isActivity,ActivityType? activityType,int? activityId)
    { Id=id;SortedId=sortedId;EntryPath=entryPath;IsActivity=isActivity;ActivityType=activityType;ActivityId=activityId; }
}
}
''')
    text=read(CODE/'common-source/Common/HomeRightEntryUIActivityInfoParser.cs')
    start=text.index('\tpublic override void PostParse()');end=text.index('\n\tprotected override IReadOnlyDictionary',start)
    methods=text[start:end].replace('public override void PostParse()','public void PostParse()').replace('\t\t((ParserBase)this).PostParse();\n','')
    write('HomeRightEntryUIActivityInfoParser.cs','''using System.Collections.Generic;
namespace Zjcs.Official.HomeHudInfrastructureUI
{
public sealed class HomeRightEntryUIActivityInfoParser
{
    readonly HomeRightEntryUIActivityInfo[] all;
    List<HomeRightEntryUIActivityInfo> mSortedEntries;
    public HomeRightEntryUIActivityInfoParser(HomeRightEntryUIActivityInfo[] values){all=values;PostParse();}
    HomeRightEntryUIActivityInfo[] GetAll()=>all;
'''+methods+'\n}\n}\n')
    from official_apk import Apk
    from official_tables import Constructors
    for path in (CODE/'global-metadata.dat',ROOT/'scripts/official_tables.py'):
        inputs[path.relative_to(ROOT).as_posix()]=sha(path)
    constructors=Constructors(CODE);tables={};config_sources={}
    with Apk(reuse_current_cache=True) as apk:
        for obj in apk.objects('defaultpackage_assets_config_luban.bundle'):
            if obj.type.name!='TextAsset':continue
            source=obj.read();name=source.m_Name.removesuffix('.g')
            if name not in ('home_right_entry_ui_activity','game_settings'):continue
            raw=source.m_Script.encode('utf8','surrogateescape')
            kind='TbHomeRightEntryUIActivityInfo' if name=='home_right_entry_ui_activity' else 'TbGameSettings'
            tables[name]=constructors.table(raw,kind)
            config_sources[name]=dict(source=apk.provenance(obj),sha256=hashlib.sha256(raw).hexdigest(),constructor=kind)
    for name in constructors.sources:
        for folder in ('common-source/Common','common-source/Common.LubanGen'):
            path=CODE/folder/(name+'.cs')
            if path.exists():inputs[path.relative_to(ROOT).as_posix()]=sha(path);break
    json_path=ROOT/'unity/Assets/Game/Resources/Home/HomeRightEntryConfig.json'
    rows=tables['home_right_entry_ui_activity']
    entries=[dict(id=r['Id'],sortedId=r['SortedId'],entryPath=r['EntryPath'],isActivity=r['IsActivity'],
        hasActivityType=r['ActivityType'] is not None,activityType=r['ActivityType'] or 0,
        hasActivityId=r['ActivityId'] is not None,activityId=r['ActivityId'] or 0) for r in rows]
    json_path.write_text(json.dumps(dict(manifest='884_156692',checkConditionId=tables['game_settings']['PlayAsYouDownloadCheckConditionId'],
        entries=entries),ensure_ascii=False,indent=2)+'\n','utf8',newline='\n')
    json_meta=Path(str(json_path)+'.meta')
    json_meta.write_text('fileFormatVersion: 2\nguid: '+hashlib.md5(b'zjcs:home-right-entry-ui-config').hexdigest()+'\nTextScriptImporter:\n  externalObjects: {}\n','utf8',newline='\n')
    for path in (json_path,json_meta):outputs[path.relative_to(ROOT).as_posix()]=sha(path)
    write('OfficialHomeRightEntryConfig.cs','''using System;
using System.Linq;
using UnityEngine;
namespace Zjcs.Official.HomeHudInfrastructureUI
{
    public sealed class OfficialHomeRightEntryConfig
    {
        [Serializable] sealed class Payload { public string manifest;public int checkConditionId;public Row[] entries; }
        [Serializable] sealed class Row { public int id,sortedId,activityType,activityId;public string entryPath;public bool isActivity,hasActivityType,hasActivityId; }
        public int CheckConditionId { get; }
        public HomeRightEntryUIActivityInfoParser Entries { get; }
        OfficialHomeRightEntryConfig(Payload data)
        {
            CheckConditionId=data.checkConditionId;
            Entries=new HomeRightEntryUIActivityInfoParser(data.entries.Select(r=>new HomeRightEntryUIActivityInfo(r.id,r.sortedId,r.entryPath,r.isActivity,
                r.hasActivityType?(ActivityType?)r.activityType:null,r.hasActivityId?(int?)r.activityId:null)).ToArray());
        }
        public static OfficialHomeRightEntryConfig LoadDefault()
        {
            var text=Resources.Load<TextAsset>("Home/HomeRightEntryConfig");if(!text)throw new InvalidOperationException("Original home right-entry configuration is unbound.");
            var data=JsonUtility.FromJson<Payload>(text.text);if(data==null||data.manifest!="884_156692"||data.entries==null)throw new InvalidOperationException("Unexpected original home right-entry configuration.");
            return new OfficialHomeRightEntryConfig(data);
        }
    }
}
''')
    service_text=PORTS.replace('IHomeHudDownloadPlayer Player { get; }',
        'IHomeHudDownloadPlayer Player { get; }\n        IDisposable SubscribeProgress(Action<PlayAsYouDownloadProgressUpdateEvent> callback);\n        IDisposable SubscribeState(Action<PlayAsYouDownloadStateUpdateEvent> callback);')
    write('HomeHudInfrastructureServices.cs',service_text)
    write('Zjcs.Official.HomeHudInfrastructureUI.asmdef',json.dumps(dict(name='Zjcs.Official.HomeHudInfrastructureUI',references=[
        'Unity.ugui','UniTask','Zjcs.Official.HomeHudUI','Zjcs.Official.HomeFunctionMenuUI','Zjcs.Official.HomePlayerPanelUI','Zjcs.Official.WorldUI']),indent=2)+'\n')
    folder_meta=metadata(DEST,True);outputs[folder_meta.relative_to(ROOT).as_posix()]=sha(folder_meta)
    receipt=dict(manifest='884_156692',generator_sha256=sha(Path(__file__)),inputs=inputs,outputs=outputs,configuration_sources=config_sources,
        scope='Selected original right-entry/download UI lifecycle and original selected entry configuration. Native hierarchy, local hosts and runtime verification are required.')
    (ROOT/'assets/reference/home-hud-infrastructure-runtime-source-manifest.json').write_text(json.dumps(receipt,ensure_ascii=False,indent=2)+'\n','utf8',newline='\n')
    print('Original home HUD infrastructure:',len(outputs),'outputs',flush=True)


if __name__=='__main__':build()
