"""Migrate the selected pinned-task UI; task rules stay with the real task host."""
import json
from pathlib import Path
import re

from prepare_home_auto_increase_runtime import ROOT, CODE, sha, metadata, method

DEST = ROOT/'unity/Assets/Game/Runtime/Official/HomeTaskUI'


def build():
    inputs, outputs = {}, {}

    def read(path, resolved=False):
        inputs[path.relative_to(ROOT).as_posix()] = sha(path)
        if resolved:
            proof = path.with_suffix('.decompilation.json')
            data = json.loads(proof.read_bytes())
            assert sha(path) == data['sha256'], path
            for name, digest in data['inputs'].items():
                assert sha(ROOT/name) == digest, name
            inputs[proof.relative_to(ROOT).as_posix()] = sha(proof)
        return path.read_text('utf-8-sig')

    def write(name, text):
        path = DEST/name
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(text, 'utf8', newline='\n')
        for item in (path, metadata(path)):
            outputs[item.relative_to(ROOT).as_posix()] = sha(item)

    prefix = '''// Selected official pinned-task UI; explicit local service bindings only.
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
using Zjcs.Official.ActivityBattleUI;
using MultiLanguageText=Zjcs.Official.WorldUI.WorldMultiLanguageText;
namespace Zjcs.Official.HomeTaskUI
{
'''
    names = ('PinnedTaskPanel', 'TaskListItem', 'TaskGoal', 'TaskConditionItemBase',
             'TaskConditionItem', 'TaskListRecommendCombatRatingItem', 'UIColorByLevel',
             'ShowRemainTimeInfo', 'MapRegionLimitStatusRefreshHelper',
             'MapRegionLimitStatusRefreshProviderForLevel', 'TraceTaskComponent')
    for name in names:
        text = read(CODE/'client-source-resolved'/(name+'.cs'), True)
        text = re.sub(r'^using (?:Common|Framework(?:\.DependencyInjection|\.Logging(?:\.InterpolatedStringHandler)?)?);\n', '', text, flags=re.M)
        text = text.replace('internal class PinnedTaskPanel', 'public class PinnedTaskPanel')
        text = text.replace('StateSwitcher<', 'WorldStateSwitcher<')
        text = re.sub(r'\bPlayer (?=\w)', 'IHomeTaskPlayer ', text)
        if name not in ('TaskConditionItem', 'TraceTaskComponent'):
            text = text.replace('\n{\n', '\n{\n\tpublic IHomeTaskServices Services { get; set; }\n', 1)
        text = re.sub(r'\[Inject\]\n\t(?:private|public) ([^\n]+) (\w+) \{ get; set; \}',
            lambda m: '\tprivate '+m[1]+' '+m[2]+' => Services.'+m[2]+';', text)
        text = text.replace('IScriptableObjectConfig<GlobalColors>', 'IHomeTaskGlobalColors')
        text = text.replace('IScriptableObjectConfig<CombatRatingConfig>', 'IHomeTaskCombatColors')
        for before, after in (
            ('MeManager.MainPlayer', 'Services.Player'), ('MeManager.Instance', 'Services'),
            ('TimeManager.Instance.', 'Services.'),
            ('TimeUtils.GetDailyResetTimeFromDate', 'Services.GetDailyResetTimeFromDate'),
            ('Singleton<PlayerTaskComparer>.Instance.PinComparer', 'PlayerTaskPinComparer.PinComparer'),
            ('Parsers.Instance.MapRegionInfoParser.Get', 'Services.GetMapRegion'),
            ('MapPlanesRankInfo.CreateFromPlanes()', 'Services.PlanesRank'),
            ('BattleFormula.Instance.CalcRankSuppression', 'Services.CalcRankSuppression'),
            ('Formula.Instance.GetCombatRatingLevel', 'Services.GetCombatRatingLevel'),
            ('TaskLimitCheckerFactory.TryCheck', 'Services.TryCheckTaskLimit'),
            ('ConditionHandler.Instance.', 'Services.Conditions.'),
            ('LanguageUtils.GetTaskTitleKey', 'Services.GetTaskTitleKey'),
            ('UIUtils.Instantiate<TaskListItem>(myTaskItemTemplate.gameObject, myTaskItemSlot, EUIInstantiateFlag.Transform)',
             'Services.InstantiateTaskItem(myTaskItemTemplate.gameObject, myTaskItemSlot)'),
            ('WindowManager.Instance.Popup((TaskDetailDialog panel) =>\n\t\t{\n\t\t\tpanel.Init(MyData);\n\t\t});', 'Services.OpenTaskDetail(MyData);'),
            ('ServiceProxyBase<PlayerServiceProxyImpl>.Instance.OperPlayerDebugCompleteTask', 'Services.DebugCompleteTask'),
        ):
            text = text.replace(before, after)
        if name == 'PinnedTaskPanel':
            pattern = r'ServiceProxyBase<PlayerServiceProxyImpl>\.Instance\.OperPlayerRefreshTask\(new PlayerTaskRefreshRequest\s*\{\s*RefreshTaskType = TaskType.All\s*\}, invokeByPlayer: false\);'
            text, count = re.subn(pattern, 'Services.RefreshAllTasks(invokeByPlayer: false);', text)
            assert count == 2
        if name == 'TaskListItem':
            text = text.replace('base.OnEnableAndInited();', 'base.OnEnableAndInited();\n\t\tmLimitStatusRefreshHelper.Services = Services;', 1)
            text, count = re.subn(r'ServiceProxyBase<PlayerServiceProxyImpl>\.Instance\.OperPlayerRefreshLimitTimeTask\(new PlayerRefreshLimitTimeTaskRequest\s*\{\s*TaskId = TaskId\s*\}, invokeByPlayer: false\);',
                'Services.RefreshLimitTimeTask(TaskId, invokeByPlayer: false);', text)
            assert count == 1
        if name == 'TaskGoal':
            start = text.index('\t\t\tGLoggerErrorInterpolatedStringHandler')
            end = text.index('\n\t\t\treturn;', start)
            text = text[:start]+'\t\t\tDebug.LogError("Path: " + Services.GetFullName(gameObject) + " RegisterCondition Error");'+text[end:]
        if name == 'MapRegionLimitStatusRefreshHelper':
            text = text.replace('GAction<MapRegionLimitType>', 'Action<MapRegionLimitType>').replace('new Action<MapRegionLimitType>()', 'delegate { }')
            text = text.replace('new MapRegionLimitStatusRefreshForRankUnlockTime()', 'Services.CreateTimedRegionLimitProvider()')
            text = text.replace('new MapRegionLimitStatusRefreshProviderForLevel()', 'new MapRegionLimitStatusRefreshProviderForLevel { Services = Services }')
        assert not re.search(r'\b(?:Inject|MeManager|Parsers|WindowManager|ServiceProxyBase|GLoggerErrorInterpolatedStringHandler|TaskLimitCheckerFactory|IScriptableObjectConfig)\b', text), name
        assert 'Unknown result type' not in text, name
        write(name+'.cs', prefix+text+'}\n')

    for name in ('ITraceTaskHandler', 'IMapRegionLimitStatusRefreshProvider'):
        text = read(CODE/'client-source'/(name+'.cs')).replace('using Common;\n', '')
        write(name+'.cs', prefix+text+'}\n')
    for name in ('MapRegionLimitType', 'MapRegionStatus', 'SubRank'):
        text = read(CODE/'common-source/Common'/(name+'.cs'))
        write(name+'.cs', text.replace('namespace Common;', 'namespace Zjcs.Official.HomeTaskUI\n{')+'}\n')
    text = read(CODE/'client-source/CombatRateingColorType.cs')
    write('CombatRateingColorType.cs', 'namespace Zjcs.Official.HomeTaskUI\n{\n'+text+'}\n')
    text = read(CODE/'framework-firstpass-source/Framework/NumberWithColorArg.cs')
    write('NumberWithColorArg.cs', prefix+text.replace('namespace Framework;', '')+'}\n')
    comparer = read(CODE/'common-source/Common/PlayerTaskComparer.cs')
    body = method(comparer, 'private int Comparer(Player player, PlayerTask x, PlayerTask y, List<(ComparerType comparerType, ComparerOrderType orderType)> priorityList)')
    body = body.replace('private int Comparer(', 'private static int Comparer(').replace('Player player', 'IHomeTaskPlayer player')
    score = method(comparer, 'private int GetScore(Player player, PlayerTask playerTask, ComparerType comparerType)')
    selected = []
    for kind, end in [('Order','Limit'),('Completed','Receive'),('Duration','AutoTrace')]:
        start = score.index('\t\tcase ComparerType.'+kind+':')
        stop = score.index('\t\tcase ComparerType.'+end+':',start)
        selected.append(score[start:stop])
    selected.append(score[score.index('\t\tcase ComparerType.AutoTrace:'):score.index('\t\tdefault:')])
    sort = read(CODE/'common-source/ItemComparerBase.cs')
    convert = method(sort,'public static int ConvertLong2IntForSort(long longValue)')
    write('PlayerTaskPinComparer.cs', prefix+'''using System;
using System.Collections.Generic;
internal static class PlayerTaskPinComparer
{
    private enum ComparerType { Duration, AutoTrace, Completed, Order }
    public static int PinComparer(IHomeTaskPlayer player, PlayerTask x, PlayerTask y) => Comparer(player,x,y,new List<(ComparerType,ComparerOrderType)> {
        (ComparerType.Duration,ComparerOrderType.Asc),(ComparerType.AutoTrace,ComparerOrderType.Desc),
        (ComparerType.Completed,ComparerOrderType.Desc),(ComparerType.Order,ComparerOrderType.Asc) });
'''+body.replace('ItemComparerBase.ComparerResult', 'ComparerResult')+'''
    private static int GetScore(IHomeTaskPlayer player, PlayerTask playerTask, ComparerType comparerType)
    { TaskInfo taskInfo=playerTask.Info(); switch(comparerType) {
'''+''.join(selected)+''' default: throw new System.ArgumentOutOfRangeException(nameof(comparerType)); } }
    private static int ComparerResult(long x,long y,ComparerOrderType type) => ConvertLong2IntForSort(type==ComparerOrderType.Asc ? x-y : y-x);
'''+convert+'}\n}\n')
    order = read(CODE/'common-source/ComparerOrderType.cs')
    write('ComparerOrderType.cs', 'namespace Zjcs.Official.HomeTaskUI\n{\n'+order+'}\n')
    for path in ('common-source/Common/PlayerTaskComparer.cs', 'common-source/ItemComparerBase.cs',
                 'common-source/Common/TimeUtils.cs', 'client-source-resolved/MapRegionLimitStatusRefreshForRankUnlockTime.cs'):
        read(CODE/path)
    read(CODE/'client-source/UIUtils.cs')
    read(CODE/'client-source/EUIInstantiateFlag.cs')
    contracts = ROOT/'scripts/fixtures/home_task_ui_services.cs'
    write('HomeTaskServices.cs', read(contracts))
    write('Zjcs.Official.HomeTaskUI.asmdef', json.dumps(dict(name='Zjcs.Official.HomeTaskUI', references=[
        'Unity.ugui', 'Zjcs.Official.ProfessionUI', 'Zjcs.Official.WorldUI',
        'Zjcs.Official.World.NativeLabelUI', 'Zjcs.Official.ActivityBattleUI']), indent=2)+'\n')
    folder = metadata(DEST, True)
    outputs[folder.relative_to(ROOT).as_posix()] = sha(folder)
    receipt = dict(manifest='884_156692', generator_sha256=sha(Path(__file__)), inputs=inputs, outputs=outputs,
        scope='Original pinned task sorting, task item/goal, trace observer, condition text and region-limit presentation. Real task state, conditions, rewards, navigation, daily refresh and timed region provider remain explicit host services.')
    (ROOT/'assets/reference/home-task-ui-runtime-source-manifest.json').write_text(json.dumps(receipt, ensure_ascii=False, indent=2)+'\n', 'utf8', newline='\n')
    print('Original home task UI:', len(outputs), 'outputs', flush=True)


if __name__ == '__main__':
    build()
