"""Migrate original home entry controls, condition observers and tip visibility."""
import json
from pathlib import Path
import re

from prepare_home_auto_increase_runtime import ROOT,CODE,sha,metadata

DEST=ROOT/'unity/Assets/Game/Runtime/Official/HomeMainUiControls'
NAMES=('FunctionEntrysPanelBase','FunctionEntrysPanel','MainTaskFunctionLimit',
    'ChatQuickMsgEntryController','TipDotObject')

PORTS='''using System;
using UnityEngine;
using Zjcs.Official.HomeHudUI;

namespace Zjcs.Official.HomeMainUiControls
{
    // Dependency boundaries for the original parser, player, input and window
    // calls. Implementations bind the current local player before activation.
    public interface IHomeMainUiConditions
    {
        int TaskFirstTraceConditionId { get; }
        IHomeHudCondition GetCondition(int id);
        IHomeHudCondition GetFunctionCondition(FunctionType type);
        IHomeHudConditionHandler ConditionHandler { get; }
        bool IsMet(IHomeHudCondition condition);
        bool HasMainMap { get; }
        bool OpenMapChannel { get; }
    }
    public struct HomeUiGlobalPointerDownResult { public Vector2 ScreenPos; }
    public interface IHomeMainUiInput
    {
        void HookGlobalPointerDown(Action<HomeUiGlobalPointerDownResult> listener);
        void UnhookGlobalPointerDown(Action<HomeUiGlobalPointerDownResult> listener);
    }
    public interface IHomeMainFunctionEntryDialog { void Init(); }
    public interface IHomeMainUiWindows
    {
        bool HasPanel { get; }
        bool HasDialog { get; }
        void ReportFunctionEntryClick();
        void PopupFunctionEntryDialog(Action<IHomeMainFunctionEntryDialog> initialize);
    }
}
'''


def replace(text,before,after,count=1):
    assert text.count(before)==count,(before,text.count(before),count)
    return text.replace(before,after)


def build():
    inputs,outputs={},{}
    def write(name,text):
        path=DEST/name;path.parent.mkdir(parents=True,exist_ok=True)
        path.write_text(text,'utf8',newline='\n')
        for item in (path,metadata(path)):outputs[item.relative_to(ROOT).as_posix()]=sha(item)
    for name in NAMES:
        path=CODE/'client-source-resolved'/(name+'.cs');proof=path.with_suffix('.decompilation.json')
        receipt=json.loads(proof.read_bytes());assert sha(path)==receipt['sha256']
        for relative,digest in receipt['inputs'].items():assert sha(ROOT/relative)==digest,relative
        for item in (path,proof):inputs[item.relative_to(ROOT).as_posix()]=sha(item)
        text=path.read_text('utf8')
        assert 'Unknown result type' not in text and 'IAsyncStateMachine' not in text
        for using in ('using Common;\n','using Framework;\n','using Framework.DependencyInjection;\n',
            'using Framework.Logging;\n','using Framework.Logging.InterpolatedStringHandler;\n'):
            text=text.replace(using,'')
        if name=='FunctionEntrysPanelBase':
            text=replace(text,'StateSwitcher<bool>','WorldStateSwitcher<bool>')
            text=replace(text,'\t[Inject]\n\tprotected IInputOperationModule InputOperationModule { get; set; }',
                '\tpublic IHomeMainUiInput InputOperationModule { private get; set; }\n\tpublic IHomeMainUiWindows Windows { protected get; set; }\n\tpublic Camera UiCamera { private get; set; }')
            text=text.replace('OnGlobalPointerDownResult','HomeUiGlobalPointerDownResult')
            text=replace(text,'HookOnInput<HomeUiGlobalPointerDownResult>','HookGlobalPointerDown')
            text=replace(text,'UnhookOnInput<HomeUiGlobalPointerDownResult>','UnhookGlobalPointerDown')
            text=replace(text,'WindowManager.Instance.HasWindowType(WindowType.Panel)','Windows.HasPanel')
            text=replace(text,'WindowManager.Instance.HasWindowType(WindowType.Dialog)','Windows.HasDialog')
            text=replace(text,'UIUtils.CheckIfClickInsideUI(item, clickPosition)',
                'HomeMainUiUtils.CheckIfClickInsideUI(item, clickPosition, UiCamera)')
        elif name=='FunctionEntrysPanel':
            text,n=re.subn(r'WindowUtils.ReportClickPanelEvent\([^\n]+\);','Windows.ReportFunctionEntryClick();',text)
            assert n==1
            text=replace(text,'WindowManager.Instance.Popup((FunctionEntryDialog dialog)',
                'Windows.PopupFunctionEntryDialog((IHomeMainFunctionEntryDialog dialog)')
        elif name in ('MainTaskFunctionLimit','ChatQuickMsgEntryController'):
            text=text.replace('ICondition','IHomeHudCondition')
            text=replace(text,'private IHomeHudCondition mCondition;',
                'private IHomeHudCondition mCondition;\n\n\tpublic IHomeMainUiConditions Services { private get; set; }')
            text=text.replace('ConditionHandler.Instance','Services.ConditionHandler')
            if name=='MainTaskFunctionLimit':
                text=replace(text,'condition.IsMet(MeManager.MainPlayer)','Services.IsMet(condition)')
                text=replace(text,'Parsers.GameSettings.TaskSettings.TaskFirstTraceConditionId','Services.TaskFirstTraceConditionId')
                text=replace(text,'Parsers.Instance.ConditionInfoParser.GetCondition(taskFirstTraceConditionId)',
                    'Services.GetCondition(taskFirstTraceConditionId)')
                text,n=re.subn(r'\t\t\tGLoggerErrorInterpolatedStringHandler message =[\s\S]*?\t\t\tLog.Error\([^\n]+\);',
                    '\t\t\tDebug.LogError("没有找到 Condition: "+taskFirstTraceConditionId+" <- TaskFirstTraceConditionId");',text)
                assert n==1
            else:
                text=replace(text,'mCondition.IsMet(MeManager.MainPlayer)','Services.IsMet(mCondition)')
                text=replace(text,'\t\tFunctionLimitInfoParser functionLimitInfoParser = Parsers.Instance.FunctionLimitInfoParser;\n\t\tmCondition = functionLimitInfoParser.Get(FunctionType.Chat).Condition;',
                    '\t\tmCondition = Services.GetFunctionCondition(FunctionType.Chat);')
                text=replace(text,'MeManager.MainMap != null','Services.HasMainMap')
                text=replace(text,'Parsers.Instance.MapInfoParser.Get(MeManager.MainMap.Container.ClassId).OpenMapChannel','Services.OpenMapChannel')
        elif name=='TipDotObject':
            text=replace(text,'\t[Inject]\n\tprivate TipDotSystem TipDotSystem { get; set; }',
                '\tpublic IProfessionTipDotSystem TipDotSystem { private get; set; }')
            text=text.replace('TipDotSystem tipDotSystem','IProfessionTipDotSystem tipDotSystem')
        text='// Original selected home UI control; local service bindings only.\n'+text
        text='\n'.join(line.rstrip() for line in text.splitlines()).rstrip()+'\n'
        write(name+'.cs','using Zjcs.Official.ProfessionUI;\nusing Zjcs.Official.WorldUI;\nusing Zjcs.Official.HomeHudUI;\nnamespace Zjcs.Official.HomeMainUiControls\n{\n'+text+'}\n')
    path=CODE/'client-source/UIUtils.cs';inputs[path.relative_to(ROOT).as_posix()]=sha(path)
    raw=path.read_text('utf-8-sig');start=raw.index('\tpublic static bool CheckIfClickInsideUI(');end=raw.index('\n\tpublic static ',start+1)
    method=raw[start:end]
    method=replace(method,'Vector2 clickPosition)','Vector2 clickPosition, Camera uiCamera)')
    method=replace(method,'\t\tCamera uiCamera = GetUiCamera();\n','')
    method=replace(method,'ref val','out val')
    method='\n'.join(line for line in method.splitlines() if '//IL_' not in line)
    write('HomeMainUiUtils.cs','// Original UIUtils click containment; UI camera supplied by the local scope.\nusing UnityEngine;\nnamespace Zjcs.Official.HomeMainUiControls\n{\npublic static class HomeMainUiUtils\n{\n'+method+'\n}\n}\n')
    write('HomeMainUiServices.cs',PORTS)
    write('Zjcs.Official.HomeMainUiControls.asmdef',json.dumps(dict(name='Zjcs.Official.HomeMainUiControls',
        references=['Unity.ugui','Zjcs.Official.ProfessionUI','Zjcs.Official.WorldUI','Zjcs.Official.HomeHudUI']),indent=2)+'\n')
    folder_meta=metadata(DEST,True)
    outputs[folder_meta.relative_to(ROOT).as_posix()]=sha(folder_meta)
    receipt=dict(manifest='884_156692',generator_sha256=sha(Path(__file__)),inputs=inputs,outputs=outputs,
        scope='Original menu entry/base, task/chat condition observers, tip visibility and click containment. Native hierarchy and actual local service integration remain required.')
    (ROOT/'assets/reference/home-main-ui-controls-source-manifest.json').write_text(json.dumps(receipt,ensure_ascii=False,indent=2)+'\n','utf8',newline='\n')
    print('Original home UI controls:',len(outputs),'outputs',flush=True)


if __name__=='__main__':build()
