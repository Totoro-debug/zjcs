"""Generate native home window ports, with optional reproduction of their recorded source.

An isolated checkout can read the original input cache through --source-root;
--reuse-selected-source preserves the already migrated controller source set.
"""
import argparse
import hashlib
import json
from pathlib import Path
import re
import subprocess

from prepare_home_auto_increase_runtime import ROOT, CODE, metadata, method, sha

DEST = ROOT/'unity/Assets/Game/Runtime/Official/HomeHangUpWindowsUI'


def build(source_root=None, reuse_selected=False):
    inputs, outputs = {}, {}
    source_root = Path(source_root or ROOT).resolve()
    code = source_root / CODE.relative_to(ROOT)
    receipt_path = ROOT/'assets/reference/home-hangup-windows-runtime-source-manifest.json'
    selected = json.loads(receipt_path.read_bytes()) if reuse_selected else None
    previous_outputs, ancestry = {}, {}
    if selected:
        prefixes = {name.split('/official-code/')[0] + '/official-code' for name in selected['inputs']
                    if '/official-code/' in name}
        if len(prefixes) != 1:
            raise ValueError('The selected original controller source must have one verified code prefix.')
        code = source_root / prefixes.pop()
        # Reproducing existing ports reads their recorded inputs. It does not
        # change the active official source or download/update an old migration.
        for name, expected in selected['inputs'].items():
            if name.startswith('.tools/') and sha(source_root/name) != expected:
                raise ValueError('Selected original window input differs: ' + name)
        ancestry = selected.get('source_reuse', {}).get('binding_ancestry', {}).copy()
        for name, expected in selected['outputs'].items():
            path = ROOT/name
            previous_outputs[name] = path.read_bytes()
            actual = sha(path)
            if actual == expected:
                continue
            # Some older receipts precede committed local resource-owner fixes.
            # Identify the exact recorded and current blobs before regenerating;
            # never replace an original input hash to admit a different source.
            current = subprocess.check_output(['git', '-C', str(ROOT), 'show', 'HEAD:' + name])
            if hashlib.sha256(current).hexdigest() != actual:
                raise ValueError('Uncommitted selected window output differs: ' + name)
            for revision in subprocess.check_output(['git', '-C', str(ROOT), 'log', '--format=%H', '--', name]).decode().splitlines():
                recorded = subprocess.check_output(['git', '-C', str(ROOT), 'show', revision + ':' + name])
                if hashlib.sha256(recorded).hexdigest() == expected:
                    ancestry[name] = dict(recorded_revision=revision, recorded_sha256=expected,
                        baseline_revision=subprocess.check_output(['git', '-C', str(ROOT), 'log', '-1', '--format=%H', '--', name]).decode().strip(),
                        baseline_sha256=actual)
                    break
            else:
                raise ValueError('Recorded window output has no matching committed source: ' + name)

    def read(name, folder='client-source-resolved'):
        path = code/folder/(name+'.cs')
        inputs[path.relative_to(source_root).as_posix()] = sha(path)
        if folder == 'client-source-resolved':
            receipt = path.with_suffix('.decompilation.json')
            proof = json.loads(receipt.read_bytes())
            assert proof['sha256'] == sha(path), name
            for relative, digest in proof['inputs'].items():
                assert sha((source_root if relative.startswith('.tools/') else ROOT)/relative) == digest, relative
                inputs[relative] = digest
            inputs[receipt.relative_to(source_root).as_posix()] = sha(receipt)
        return path.read_text('utf-8-sig').replace('\r\n', '\n')

    def replace(text, old, new):
        assert old in text, old
        return text.replace(old, new)

    def replace_method(text, signature, new):
        return replace(text, method(text, signature), '\t'+new+'\n')

    def emit(name, text):
        path = DEST/(name+'.cs'); path.parent.mkdir(parents=True, exist_ok=True)
        header = '''// Selected original client methods; explicit player/configuration/presentation ports.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine.Events;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Official.HomeUI;
using Zjcs.Official.HomeHangUpTips;
using Zjcs.Official.HomeSceneUiControls;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
using Object = UnityEngine.Object;
using MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;
using PlayerPropItem = Zjcs.Official.ProfessionUI.ProfessionPlayerPropItem;
using PropType = Zjcs.Official.ProfessionUI.ProfessionPropertyType;
using PropConfigUtils = Zjcs.Official.ProfessionUI.ProfessionPropertyConfig;
using HangUpInfo = Zjcs.Official.HomeHangUpWindowsUI.HomeWindowHangUpInfo;
using FastHangUpInfo = Zjcs.Official.HomeHangUpWindowsUI.HomeWindowFastHangUpInfo;
using Player = Zjcs.Official.HomeHangUpWindowsUI.IHomeHangUpWindowPlayer;
namespace Zjcs.Official.HomeHangUpWindowsUI
{
'''
        path.write_text(header+text+'\n}\n', 'utf8', newline='\n')
        for item in (path, metadata(path)): outputs[item.relative_to(ROOT).as_posix()] = sha(item)

    def clean(text):
        text = re.sub(r'^using [^\n]+;\n', '', text, flags=re.M)
        text = re.sub(r'^\[UIWindow\([^\n]*\n', '', text, flags=re.M)
        text = re.sub(r'\s*\[Inject\]\s*(private|public) [^\n]+ \w+ \{ get; set; \}', '', text)
        text = text.replace('StateSwitcher<', 'WorldStateSwitcher<')
        text = text.replace('MeManager.MainPlayer', 'Services.Player')
        text = text.replace('LanguageManager.Instance.GetTextFormated', 'Services.TextFormatted')
        text = text.replace('LanguageManager.Instance.GetText', 'Services.Text')
        text = text.replace('BattleFormula.Instance.', 'Services.')
        text = text.replace('HangUpUtils.', 'Services.')
        text = text.replace('Parsers.Instance.HangUpInfoParser.Get(', 'Services.GetHangUpInfo(')
        text = text.replace('R3.DisposableBag', 'HomeWindowResourceBag')
        text = re.sub(r'(public class \w+ : [^\n]+\n\{)', r'\1\n    public IHomeHangUpWindowServices Services { protected get; set; }', text, count=1)
        return text

    for name in ('EPropHandleType', 'OutputGroupType'):
        source = read(name, 'common-source/Common')
        # Unity 2022 C# uses a block namespace, retaining every original member.
        emit(name, source[source.index('public enum'):])

    text = clean(read('FurnitureOutputInfo'))
    text = text.replace('LanguageUtils.GetHangUpBaseOutput(', 'Services.GetHangUpBaseOutput(')
    text = text.replace('NumberDisplayRuleUtils.Format((long)Math.Round(value, 1))', 'formatter((long)Math.Round(value, 1))')
    text = text.replace('public static string GetPropValue(double value)', 'public static string GetPropValue(double value, Func<long, string> formatter)')
    # All consumers keep the original conversion; the user's number display
    # setting is an explicit formatter rather than a fabricated global default.
    text = re.sub(r'GetPropValue\((hangUpReward|value|value2|value3|num|num2)\)', r'GetPropValue(\1, Services.FormatCount)', text)
    # This original component is also used independently of a furniture window.
    # Its data port does not require reward/navigation/network operations.
    text = text.replace('public IHomeHangUpWindowServices Services { protected get; set; }', '''private IHomeFurnitureOutputServices outputServices;
    public IHomeHangUpWindowServices Services { set => outputServices = new HomeFurnitureOutputServices(value); }
    public IHomeFurnitureOutputServices OutputServices { get => outputServices; set => outputServices = value; }''')
    text = text.replace('Services.Player.PlayerPropCenter', 'OutputServices.PropertyCenter')
    text = text.replace('Services.Player.NonBattleProps', 'OutputServices.NonBattleProps')
    text = re.sub(r'(?<!Output)Services\.', 'OutputServices.', text)
    text = text.replace('public List<EPropHandleType> BranchExploreEPropHandleTypes => mBranchExploreEPropHandleTypes;', '''public List<EPropHandleType> BranchExploreEPropHandleTypes => mBranchExploreEPropHandleTypes;
    public IReadOnlyList<PlayerPropItem> OutputItems => new[] { mBaseOutputPropItem, mAddSumPropItem,
        mBranchExplorePropItem, mTreasurePropItem, mMouthCardPropItem, mAstrologicalPropItem,
        mSumOutputPropItem, mSumStorePropItem };''')
    emit('FurnitureOutputInfo', text)

    text = clean(read('FurnitureHangUpQuickReceive'))
    text = text.replace('private ICondition mCondition;', 'private object mCondition;')
    text = text.replace('FunctionType.HangUpQuickReceiveSwitch', '164')
    text = text.replace('FunctionLimitInfo functionLimitInfo = Parsers.Instance.FunctionLimitInfoParser.Get(', 'HomeWindowFunctionLimitInfo functionLimitInfo = Services.GetFunctionLimitInfo(')
    text = text.replace('ConditionHandler.Instance.Register(', 'Services.RegisterCondition(').replace('ConditionHandler.Instance.Deregister(', 'Services.DeregisterCondition(')
    text = replace_method(text, 'private void OnQuickReceiveToggleChanged(bool isOn)', '''private void OnQuickReceiveToggleChanged(bool isOn)
    {
        if (isOn != Services.Player.GetHangUpQuickReceiveState(mFurniture))
            Services.SwitchQuickReceive(mFurniture, isOn, (resp, err) => mQuickReceiveToggle.SetIsOnWithoutNotify(!isOn));
    }''')
    emit('FurnitureHangUpQuickReceive', text)

    text = clean(read('FurnitureBedDialog'))
    text = text.replace('AK.Wwise.Event', 'OfficialWwiseEvent')
    # The original default-valued R3 struct supports AddTo before Awake.
    # A class-backed port must also own loads made while the window is staged
    # inactive, and must not discard those handles when Awake subsequently runs.
    text = text.replace('private HomeWindowResourceBag mDisposableBag;',
                        'private HomeWindowResourceBag mDisposableBag = new HomeWindowResourceBag();')
    text = text.replace('mDisposableBag = default;', 'mDisposableBag ??= new HomeWindowResourceBag();')
    text = text.replace('mDisposableBag.Dispose();', 'mDisposableBag?.Dispose();')
    text = text.replace('HomeSettings.Value.GetFurnitureTypeNameKey(', 'Services.GetFurnitureTypeNameKey(')
    text = text.replace('Parsers.Instance.HangUpInfoParser.TryGetMainHangUpInfo(', 'Services.TryGetMainHangUpInfo(')
    text = text.replace('Parsers.Instance.FastHangUpInfoParser.Get(', 'Services.GetFastHangUpInfo(')
    text = text.replace('FurnitureInfo furnitureInfo = Parsers.Instance.FurnitureInfoParser.Get(', 'HomeWindowFurnitureInfo furnitureInfo = Services.GetFurnitureInfo(')
    text = text.replace('LanguageUtils.GetItemNameKey(', 'Services.GetItemNameKey(').replace('LanguageUtils.GetItemDescKey(', 'Services.GetItemDescKey(')
    text = text.replace('ImageSlot.DestroyChildren();', 'foreach (Transform child in ImageSlot) Destroy(child.gameObject);')
    text = replace(text, 'UIUtils.Instantiate<FurnitureIconWidget>(ResourceLoader.LoadPrefab(LanguageUtils.GetFurnitureIconPrefab(mFurniture)).AddTo(ref mDisposableBag).Result, ImageSlot)', 'Instantiate(mDisposableBag.Add(Services.Resources.LoadAsset<GameObject>(Services.GetFurnitureIconPrefabPath(mFurniture))).Result, ImageSlot).GetComponent<FurnitureIconWidget>()')
    text = text.replace('PlayerOutputAdditionDataWrap', 'object')
    text = replace(text, 'mTipLevelDisposable = Subscriber.Subscribe(OnTipLevelChange);', 'mTipLevelDisposable = Services.SubscribeTips(OnTipLevelChange);')
    text = replace(text, 'BaseHangUpFurniture furnitureComponent = HomeEntityManager.GetFurnitureComponent<BaseHangUpFurniture>(mFurniture);\n\t\tUpdateTipLevelDotSwitcher(furnitureComponent.TipLevel);', 'UpdateTipLevelDotSwitcher(Services.GetTipLevel(mFurniture));')
    text = text.replace('BroadcastManager.Instance.SystemMsg(', 'Services.SystemMessage(')
    text = text.replace('ServiceProxyBase<PlayerServiceProxyImpl>.Instance.OperPlayerGetHangUpReward(mHangUpIdList).OnSuccess += (PlayerAwardMsg rep) =>', 'Services.Collect(mHangUpIdList, (IHomeWindowAwardMessage rep) =>')
    text = replace(text, '\t\t\tHangUpPublisher.PublishAsync(default);\n\t\t\tRefreshHangUpTimer();\n\t\t};', '\t\t\tServices.PublishCollected();\n\t\t\tRefreshHangUpTimer();\n\t\t});')
    text = text.replace('Formula.Instance.MergeAward(', 'Services.MergeAward(').replace('AwardListPanel.PopupAwardListPanel(', 'Services.PopupAwardList(')
    text = text.replace('FunctionType.HomeBedSpeedConfirm', 'Services.HomeBedSpeedConfirmFunctionId')
    for key in ('home_bed_confirm1', 'home_bed_confirm2'):
        original = 'WindowManager.Instance.Popup((ConfirmDialog panel) =>\n\t\t\t{\n\t\t\t\tpanel.Init("'+key+'", () =>\n\t\t\t\t{\n\t\t\t\t\tFastHnagUp();\n\t\t\t\t});\n\t\t\t});'
        text = replace(text, original, 'Services.Confirm("'+key+'", FastHnagUp);')
    text = replace(text, 'WindowManager.Instance.Popup((ConfirmDialog panel) =>\n\t\t\t\t{\n\t\t\t\t\tpanel.Init(key, () =>\n\t\t\t\t\t{\n\t\t\t\t\t\tFreeFastHangUp();\n\t\t\t\t\t});\n\t\t\t\t\tpanel.SetTitleText(titleKey);\n\t\t\t\t});', 'Services.Confirm(key, titleKey, FreeFastHangUp);')
    text = replace(text, 'WindowManager.Instance.Popup((FurnitureFastHangUpDialog go) =>\n\t\t\t{\n\t\t\t\tgo.Init(mFastHangUpInfo.FastHangUpId, null);\n\t\t\t\tgo.ParentPanel = gameObject;\n\t\t\t});', 'Services.OpenFastHangUp(mFastHangUpInfo.FastHangUpId, gameObject);')
    text = text.replace('SubRank highSubRank = Services.Player.SubRank.ToRank().GetHighSubRank();', 'int highSubRank = Services.GetRankHighSubRank(Services.Player.SubRank);')
    text = text.replace('PlayerRankUtils.IsRankLimited(', 'Services.IsRankLimited(').replace('Formula.Instance.GetMaxPlayerLevel(', 'Services.GetMaxPlayerLevel(')
    text = text.replace('Parsers.Instance.MapRegionInfoParser.FirstMainMapRegionByRank.TryGetValue(Services.Player.SubRank.ToRank(), out var value)', 'Services.TryGetFirstMainMapRegion(Services.Player.SubRank, out var value)')
    text = text.replace('Services.Player.GetMapRegionStatus(value.ClassId, value.Region) == MapRegionStatus.Public', 'Services.Player.IsMapRegionPublic(value.ClassId, value.Region)')
    text = text.replace('WindowManager.Instance.Close(', 'Services.Close(').replace('HangUpUIUtils.OperFastHangUp(', 'Services.OperFastHangUp(')
    text = text.replace('Parsers.GameSettings.PlayerExpItemId', 'Services.PlayerExpItemId')
    text = text.replace('mainPlayer.GetAllMapActiveEntCount(MapWorldActiveableEntityType.LostGoddess)', 'mainPlayer.GetLostGoddessActiveCount()')
    text = re.sub(r'FurnitureOutputInfo.GetPropValue\((value|hangUpReward)\)', r'FurnitureOutputInfo.GetPropValue(\1, Services.FormatCount)', text)
    text = text.replace('ItemInfo hangUpRewardItem', 'HomeWindowItemInfo hangUpRewardItem')
    text = text.replace('ResourceLoader.LoadSprite(hangUpRewardItem.Icon).AddTo(ref mDisposableBag).Result', 'mDisposableBag.Add(Services.Resources.LoadAsset<Sprite>(hangUpRewardItem.Icon)).Result')
    text = text.replace('mainPlayer.CalcOutputAdditionExtStore(', 'Services.CalcOutputAdditionExtStore(')
    text = text.replace('TimeUtils.SecondToHour(hangUpSeconds)', '(hangUpSeconds / 3600.0)')
    emit('FurnitureBedDialog', text)

    text = clean(read('BlessingRuleBtn'))
    text = replace(text, 'WindowManager.Instance.Popup((BlessingLevelRuleDialog d) =>\n\t\t{\n\t\t\td.Init();\n\t\t});', 'Services.OpenBlessingRule();')
    emit('BlessingRuleBtn', text)

    text = clean(read('OutputAdditionDetailButton'))
    text = replace(text, 'private HomeWindowResourceBag mDisposableBag;', 'private HomeWindowResourceBag mDisposableBag;\n    private IDisposable functionStateSubscription;')
    text = text.replace('ServerInfoManager.Instance.OnFunctionDisabledUpdate += OnFunctionDisabledUpdate;', 'functionStateSubscription = Services.SubscribeOutputAdditionFunctionState(OnFunctionDisabledUpdate);')
    text = replace(text, 'if (ServerInfoManager.Instance != null)\n\t\t{\n\t\t\tServerInfoManager.Instance.OnFunctionDisabledUpdate -= OnFunctionDisabledUpdate;\n\t\t}', 'functionStateSubscription?.Dispose();')
    text = text.replace('mDisposableBag.Dispose();', 'mDisposableBag?.Dispose();').replace('mDisposableBag = default;', 'mDisposableBag = new HomeWindowResourceBag();')
    text = replace(text, 'mEnterNewDaySubscriber.Subscribe(OnNewDay).AddTo(ref mDisposableBag);', 'mDisposableBag.Add(Services.SubscribeNewDay(OnNewDay));')
    text = replace_method(text, 'private void OnFunctionDisabledUpdate(ServerFunctionType type, bool arg2, HashSet<FunctionsDisabledKeyBase> set)', '''private void OnFunctionDisabledUpdate()
    {
        // The port subscribes only to the original OutputAddition function.
        if (isActiveAndEnabled) Refresh();
    }''')
    text = text.replace('IEnterNewDay _', 'object _')
    text = text.replace('ServerInfoManager.Instance.CheckFunctionDisabled(Services.Player, ServerFunctionType.OutputAddition)', 'Services.IsOutputAdditionDisabled')
    text = text.replace('List<(List<OutputAdditionInfo>, DateTime)>', 'List<(List<HomeWindowOutputAdditionInfo>, DateTime)>')
    text = text.replace('OutputAdditionUtils.GetCurrOutputGroupPropList(Services.Player, mOutputGroupType)', 'Services.GetCurrOutputGroupPropList(mOutputGroupType)')
    text = text.replace('OutputAdditionPropType outputAdditionPropType', 'int outputAdditionPropType')
    text = text.replace('OutputAdditionUtils.ToPercentage(Services.Player.GetOutputAdditionPropValue(outputAdditionPropType))', '((float)Services.Player.GetOutputAdditionPropValue(outputAdditionPropType) / 10000f * 100f)')
    text = replace(text, 'WindowManager.Instance.Popup((PopTipOutputAddition panel) =>\n\t\t{\n\t\t\tpanel.Init(mOutputGroupType);\n\t\t});', 'Services.OpenOutputAddition(mOutputGroupType);')
    emit('OutputAdditionDetailButton', text)

    text = clean(read('BlessingLevelRuleDialog'))
    text = text.replace('IHomeHangUpWindowServices Services', 'IHomeBlessingRuleServices Services')
    text = text.replace('Services.Player.', 'Services.Window.Player.')
    text = text.replace('mRuleDialog.SetRule("BlessingLevelRule");',
        'mRuleDialog.Rules = Services.RuleTips;\n\t\tmRuleDialog.SetRule("BlessingLevelRule");')
    emit('BlessingLevelRuleDialog', text)

    text = clean(read('BlessingLevelRuleItem'))
    text = text.replace('IHomeHangUpWindowServices Services', 'IHomeBlessingRuleServices Services')
    text = text.replace('Parsers.Instance.HangUpInfoParser.TryGetMainHangUpInfo(', 'Services.Window.TryGetMainHangUpInfo(')
    text = text.replace('Services.GetHangUpReward(', 'Services.Window.GetHangUpReward(')
    text = text.replace('Services.TextFormatted(', 'Services.Window.TextFormatted(')
    text = text.replace('Parsers.Instance.HomeLevelInfoParser.Get(blessingLevel).FurnitureLevelUpLimit',
        'Services.FurnitureLevelUpLimit(blessingLevel)')
    emit('BlessingLevelRuleItem', text)

    text = clean(read('RuleTipNumer', 'client-source'))
    text = text.replace('    public IHomeHangUpWindowServices Services { protected get; set; }', '')
    emit('RuleTipNumer', text)

    for name in ('HomeHangUpWindowServices', 'HomeBlessingRuleServices', 'OfficialHomeHangUpWindowAssetCatalog'):
        path = DEST/(name+'.cs')
        for item in (path, metadata(path)): outputs[item.relative_to(ROOT).as_posix()] = sha(item)
    path = DEST/'Zjcs.Official.HomeHangUpWindowsUI.asmdef'
    path.write_text(json.dumps(dict(name='Zjcs.Official.HomeHangUpWindowsUI', references=[
        'Unity.ugui', 'Zjcs.Official.HomeUI', 'Zjcs.Official.HomeHangUpTips', 'Zjcs.Official.HomeSceneUiControls', 'Zjcs.Official.ProfessionUI',
        'Zjcs.Official.WorldUI', 'Zjcs.Official.World.NativeLabelUI']), indent=2)+'\n','utf8',newline='\n')
    for item in (path, metadata(path), metadata(DEST, True)): outputs[item.relative_to(ROOT).as_posix()] = sha(item)
    for relative in ('client-source/NumberDisplayRuleUtils.cs', 'common-source/Common/FunctionType.cs',
                     'common-source/Common/PropType.cs', 'common-source/Common/OutputAdditionUtils.cs',
                     'common-source/Common/Formula.cs', 'framework-firstpass-source/UIBase.cs'):
        path = code/relative; inputs[path.relative_to(source_root).as_posix()] = sha(path)
    # Retain explicit dependency identity when reusing the original shared code.
    for relative in ('ProfessionUI/UIBase.cs', 'ProfessionUI/ProfessionPlayerPropItem.cs',
                     'ProfessionUI/ProfessionPropertyType.cs', 'ProfessionUI/PropUpgradeEffectAgent.cs',
                     'ProfessionUI/ProfessionImageResourceBridge.cs'):
        path = ROOT/'unity/Assets/Game/Runtime/Official'/relative
        inputs[path.relative_to(ROOT).as_posix()] = sha(path)
    for name in ('HomeHangUpWindowServices', 'IHomeFurnitureOutputServices'):
        path = DEST/(name+'.cs')
        inputs[path.relative_to(ROOT).as_posix()] = sha(path)
    if selected:
        for name, previous in previous_outputs.items():
            if (ROOT/name).read_bytes() != previous:
                raise ValueError('Reproduction changed the existing window output: ' + name)
    proof = dict(manifest=code.parent.name, generator_sha256=sha(Path(__file__)), inputs=inputs, outputs=outputs,
        scope='Original bed window, output source/upgrade preview, quick-receive and blessing-rule controllers. Explicit typed player, property, transaction and presentation owners; native assets, real local binding and Player validation are separate.')
    if selected:
        proof['source_reuse'] = dict(code_prefix=code.relative_to(source_root).as_posix(), binding_ancestry=ancestry)
    receipt_path.write_text(json.dumps(proof,ensure_ascii=False,indent=2)+'\n','utf8',newline='\n')
    return proof


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source-root', type=Path, default=ROOT, help='Read-only official input repository; outputs stay in this checkout.')
    parser.add_argument('--reuse-selected-source', action='store_true', help='Reproduce the existing verified controller source without updating its migration.')
    arguments = parser.parse_args()
    build(arguments.source_root, arguments.reuse_selected_source)
