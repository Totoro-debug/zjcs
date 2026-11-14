"""Migrate selected ConditionHandler subscription and notification ordering."""
import hashlib
import json
import re
from pathlib import Path

from prepare_home_auto_increase_runtime import ROOT, CODE, metadata, sha, method
from prepare_partner_gift_configuration import normalize, without_logging


def build():
    inputs, methods, outputs = {}, {}, {}

    def source(name):
        path = CODE/'common-source/Common'/(name+'.cs')
        inputs[path.relative_to(ROOT).as_posix()] = sha(path)
        return path.read_text('utf-8-sig').replace('\r\n', '\n')

    def selected(name, signature, diagnostics=0):
        value = method(source(name), signature)
        methods.setdefault(name, {})[signature] = hashlib.sha256(value.encode()).hexdigest()
        return without_logging(value, diagnostics) if diagnostics else value

    source('ConditionType')
    predicate_proof = ROOT/'assets/reference/home-condition-source-manifest.json'
    predicate = json.loads(predicate_proof.read_bytes())
    assert predicate['manifest'] == '884_156692'
    enum_owner = ROOT/'unity/Assets/Game/Runtime/Official/HomeConditions/HomeConditionRules.cs'
    assert sha(enum_owner) == predicate['installed_outputs'][enum_owner.relative_to(ROOT).as_posix()]
    for path in (predicate_proof, enum_owner): inputs[path.relative_to(ROOT).as_posix()] = sha(path)
    raw = source('ConditionHandler')
    start = raw.index('\tpublic readonly HashSet<ConditionType> Excluded')
    end = raw.index('\n\t};', start)+len('\n\t};')
    excluded = raw[start:end]
    text = '''using System;
using System.Collections.Generic;
using System.Linq;
using ConditionType = Zjcs.Official.HomeConditions.HomeConditionRules.ConditionType;
namespace Zjcs.Official.HomeConditions
{
    // Values and composite children retain their selected configuration identity.
    public interface IHomeConditionNotification
    {
        ConditionType Type { get; }
        bool IsComposite { get; }
        List<IHomeConditionNotification> GetConditions();
    }
    // Original registry and callback order. The local player owner forwards the
    // corresponding committed player events and destroys this registry on exit.
    public sealed class HomeConditionNotifications
    {
        private IHomeConditionPlayer mPlayer;
        private readonly Action<Exception> reportException;
        private readonly Action onConditionMapChange;
        private readonly ConditionInfoParser conditionInfoParser = new ConditionInfoParser();
        private bool mIsInvoking;
        private Dictionary<ConditionType,Dictionary<Action,int>> mConditionActionMap = new Dictionary<ConditionType,Dictionary<Action,int>>();
        private Dictionary<ConditionType,Dictionary<Action,int>> mPendingRemoveMap = new Dictionary<ConditionType,Dictionary<Action,int>>();
        private Dictionary<ConditionType,Dictionary<Action,int>> mPendingAddMap = new Dictionary<ConditionType,Dictionary<Action,int>>();
        public HomeConditionNotifications(IHomeConditionPlayer player, Action<Exception> reportException, Action onConditionMapChange=null)
        {
            mPlayer=player??throw new ArgumentNullException(nameof(player));
            this.reportException=reportException??throw new ArgumentNullException(nameof(reportException));
            this.onConditionMapChange=onConditionMapChange;
        }
        public void Destroy() { mConditionActionMap.Clear(); mPlayer=null; }
'''+excluded+'\n'
    signatures = (
        'private bool IsExcluded(ConditionType conditionType)',
        'public void Register(ICondition condition, Action action)',
        'private void Register(ConditionType conditionType, Action action)',
        'public void Deregister(ICondition condition, Action action)',
        'private void Deregister(ConditionType conditionType, Action action)',
        'private void AddToPendingMap(ConditionType conditionType, Action action, Dictionary<ConditionType, Dictionary<Action, int>> map)',
        'public void DeregisterConditionType(ConditionType conditionType)',
        'private void InvokeConditionAction(ConditionType conditionType)',
        'private void PostInvoke()')
    for signature in signatures:
        value = selected('ConditionHandler', signature, int('InvokeConditionAction' in signature))
        value = value.replace('ICondition', 'IHomeConditionNotification')
        value = re.sub(r'^[\t ]*ConditionInfoParser conditionInfoParser = Parsers.Instance.ConditionInfoParser;\n', '', value, flags=re.M)
        value = value.replace('mServerHoursConditionHandler?.OnConditionMapChange();', 'onConditionMapChange?.Invoke();')
        if 'InvokeConditionAction' in signature:
            value, count = re.subn(r'catch \(Exception exception\)\s*\{\s*\}',
                'catch (Exception exception) { reportException(exception); }', value)
            assert count == 1
        text += value+'\n'
    events = (
        'MPlayer_OnLevelUpdate', 'MPlayer_OnSubRankUpdate', 'MPlayer_OnWorldMapProgressUpdate',
        'MPlayer_OnItemCountUpdate', 'MPlayer_OnMapDataAdd', 'MPlayer_OnMapStatusUpdate',
        'MPlayer_OnStoryCountUpdate', 'MPlayer_OnStoryStatusUpdate', 'MPlayer_OnProfessionPromote',
        'MPlayer_OnHangUpLevelUp', 'MPlayer_OnHangUpUnlock', 'MPlayer_OnAddNewNpc',
        'MPlayer_OnOpenChest', 'MPlayer_OnTaskAdd', 'MPlayer_OnTaskUpdate',
        'MPlayer_OnTaskRemove', 'MPlayer_OnAddFinishedTask',
        'MPlayer_OnDemonBattlefieldLayerUpdate', 'MPlayer_OnUpdateBlackboardData')
    for name in events:
        signature = re.search(r'private void '+name+r'\([^\n]*\)', raw).group()
        value = selected('ConditionHandler', signature)
        body = value[value.index('{')+1:value.rindex('}')]
        assert not re.sub(r'InvokeConditionAction\(ConditionType\.\w+\);|\s+', '', body), name
        value = value.replace(signature, 'public void Notify'+name[len('MPlayer_On'):]+'()')
        text += value+'\n'
    signature = 'private void MPlayer_OnActiveableEntUpdate(MapActiveableEntityInfo obj)'
    value = selected('ConditionHandler', signature)
    guard = 'obj.ActiveEntType == MapWorldActiveableEntityType.Goddess'
    assert value.count(guard) == 1
    value = value.replace(signature, 'public void NotifyActiveableEntUpdate(bool isGoddess)')
    text += value.replace(guard, 'isGoddess')+'\n'
    text += 'private sealed class ConditionInfoParser\n{\n'
    for signature in ('public void Expand(ICondition condition, List<ICondition> result)',
        'public void Distinct(List<ICondition> list)'):
        value = selected('ConditionInfoParser', signature).replace('ICondition', 'IHomeConditionNotification')
        value = value.replace('(condition as CompositeCondition).GetConditions()', 'condition.GetConditions()')
        text += value+'\n'
    text += '}\npublic static ConditionType TypeForName(string name)\n{\nswitch(name)\n{\n'
    leaves = ('NoneCondition','AlwaysCondition','LevelCondition','HasItemCondition','MapUnlockCondition',
        'MapRegionActivedCondition','HasManualMeetNpcCondition','CompositeAndCondition','CompositeOrCondition',
        'HasItemTypeCondition','RankCondition','SubRankCondition','MaxProfessionRankCondition',
        'StoryCountCondition','AnyMapGoddessActivedCondition','StagePassCondition',
        'WorldMapProgressCondition','MapGameTotalCountCondition','MapLogicEntityEndByTagCondition',
        'MapRegionGoddessCondition','TotalOpenChestCondition','DemonBattleLayerPassCondition',
        'TaskSubmitCondition','TaskCompleteCondition','RegionCodeCondition','ServerRankUnlockDaysCondition',
        'PlanesMergeActivityEnableCondition')
    for name in leaves:
        value = re.search(r'public override ConditionType Type => ConditionType\.(\w+);', source(name)).group(1)
        text += 'case "'+name+'": return ConditionType.'+value+';\n'
    text += 'default: throw new NotSupportedException("Condition outside migrated notification closure: "+name);\n}\n}\n}\n}\n'
    assert not any(token in text for token in ('Parsers.', 'MeManager.', 'ServerHoursConditionHandler', 'GLogger', 'Log.Error'))
    destination = ROOT/'unity/Assets/Game/Runtime/Official/HomeConditions/HomeConditionNotifications.cs'
    destination.write_text(normalize('// Selected official ConditionHandler/ConditionInfoParser; local lifetime and event ports.\n'+text),
        encoding='utf8', newline='\n')
    for path in (destination, metadata(destination)):
        outputs[path.relative_to(ROOT).as_posix()] = sha(path)
    receipt = dict(manifest='884_156692', generator_sha256=sha(Path(__file__)), inputs=inputs,
        methods=methods, outputs=outputs, adaptations=[
            'Original reference counts, excluded types, composite expansion/distinct order, callback snapshots and pending add/remove order.',
            'Payload-independent selected player callbacks retain their original ordered condition types.',
            'Activeable-entity notification preserves the Goddess branch via an explicit boolean payload port, followed by the original unconditional world-progress notification.',
            'The local owner supplies player events, exception reporting and optional condition-map-change notification; online/static player service hooks are not installed.',
            'Leaf type identities are read from the selected original condition classes; evaluation remains in HomeConditionRules.'])
    (ROOT/'assets/reference/home-condition-notifications-source-manifest.json').write_text(
        json.dumps(receipt, ensure_ascii=False, indent=2)+'\n', encoding='utf8', newline='\n')
    print('Original home condition notifications:', len(methods['ConditionHandler']), 'methods', flush=True)


if __name__ == '__main__':
    build()
