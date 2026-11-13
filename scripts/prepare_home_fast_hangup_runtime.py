"""Migrate original item acceleration selection and response presentation order."""
import json
import re
from pathlib import Path
from prepare_home_auto_increase_runtime import ROOT, CODE, metadata, method, sha

DEST = ROOT/'unity/Assets/Game/Runtime/Official/HomeFastHangUpUI'


def build():
    inputs, outputs = {}, {}

    def read(name, folder='client-source-resolved'):
        path = CODE/folder/(name+'.cs')
        inputs[path.relative_to(ROOT).as_posix()] = sha(path)
        if folder == 'client-source-resolved':
            receipt = path.with_suffix('.decompilation.json')
            proof = json.loads(receipt.read_bytes())
            assert proof['sha256'] == sha(path)
            for relative, digest in proof['inputs'].items():
                assert sha(ROOT/relative) == digest, relative
                inputs[relative] = digest
            inputs[receipt.relative_to(ROOT).as_posix()] = sha(receipt)
        return path.read_text('utf-8-sig').replace('\r\n', '\n')

    def emit(name, body):
        path = DEST/(name+'.cs'); path.parent.mkdir(parents=True, exist_ok=True)
        header = '''// Selected original acceleration UI; explicit local services only.
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Official.HomeUI;
using Zjcs.Official.HomeHangUpWindowsUI;
using Zjcs.Official.HomeCountControlsUI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
using MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;
using Player = Zjcs.Official.HomeFastHangUpUI.IHomeFastHangUpPlayer;
using FastHangUpInfo = Zjcs.Official.HomeFastHangUpUI.HomeFastHangUpInfo;
using HangUpInfo = Zjcs.Official.HomeHangUpWindowsUI.HomeWindowHangUpInfo;
using ItemInfo = Zjcs.Official.ProfessionUI.IProfessionItemInfo;
namespace Zjcs.Official.HomeFastHangUpUI
{
'''
        path.write_text(header+body+'\n}\n', 'utf8', newline='\n')
        for item in (path, metadata(path)): outputs[item.relative_to(ROOT).as_posix()] = sha(item)

    def clean(text):
        text = re.sub(r'^using [^\n]+;\n', '', text, flags=re.M)
        text = re.sub(r'^\[UIWindow\([^\n]*\n', '', text, flags=re.M)
        text = re.sub(r'\s*\[Inject\]\s*(private|public) [^\n]+ \w+ \{ get; set; \}', '', text)
        text = re.sub(r'(public class \w+ : [^\n]+\n\{)', r'\1\n    public IHomeFastHangUpServices Services { protected get; set; }', text, count=1)
        text = text.replace('StateSwitcher<', 'WorldStateSwitcher<')
        text = text.replace('MeManager.MainPlayer', 'Services.Player')
        text = text.replace('Parsers.Instance.FastHangUpInfoParser.Get(', 'Services.GetFastHangUpInfo(')
        text = text.replace('Parsers.Instance.HangUpInfoParser.Get(', 'Services.Window.GetHangUpInfo(')
        text = text.replace('HomeSettings.Value.', 'Services.')
        text = text.replace('LanguageManager.Instance.GetTextFormated(', 'Services.Window.TextFormatted(')
        text = text.replace('LanguageManager.Instance.GetText(', 'Services.Window.Text(')
        text = text.replace('LanguageUtils.GetItemNameKey(', 'Services.Window.GetItemNameKey(')
        text = text.replace('BroadcastManager.Instance.SystemMsg(', 'Services.Window.SystemMessage(')
        text = text.replace('WindowManager.Instance.Close(', 'Services.Window.Close(')
        return text

    text = clean(read('SetUseCountSlider', 'client-source'))
    # Original long Clamp implementation, rather than a float clamp.
    text = text.replace('MathUtils.Clamp(currentValue, minValue, mSingleMaxUseCount)',
                        'HomeFastHangUpMath.Clamp(currentValue, minValue, mSingleMaxUseCount)')
    emit('SetUseCountSlider', text)
    emit('HomeFastHangUpMath', 'internal static class HomeFastHangUpMath\n{\n'
         +method(read('MathUtils', 'common-source/Common'), 'public static long Clamp(long val, long min, long max)')+'}\n')

    text = clean(read('FurnitureUseItemSliderGroup'))
    text = text.replace('ItemIconManager.GetIcon(itemId, mItemSlot)', 'Services.GetIcon(itemId, mItemSlot)')
    text = text.replace('ItemIconManager.RecycleIcon(', 'Services.RecycleIcon(')
    emit('FurnitureUseItemSliderGroup', text)

    text = clean(read('FurnitureFastHangUpDialog'))
    text = text.replace('if (ItemIconManager != null)', 'if (Services != null)')
    text = text.replace('ItemIconManager.RecycleIcon(', 'Services.RecycleIcon(')
    text = text.replace('mainPlayer.TryGetUnlockSingleHangUpData(hangUpId, out var _)', 'mainPlayer.HasUnlockedHangUp(hangUpId)')
    text = text.replace('HangUpUtils.GetHangUpRewardItem(', 'Services.GetHangUpRewardItem(')
    text = text.replace('ItemIconManager.GetIcon<ItemIcon>(', 'Services.GetIcon(')
    text = text.replace('HangUpUtils.GetHangUpReward(', 'Services.Window.GetHangUpReward(')
    text = text.replace('HangUpUtils.GetHangUpUnsettledRewardCount(', 'Services.GetHangUpUnsettledRewardCount(')
    text = text.replace('TimeUtils.SecondToHour(remainderSeconds)', '(remainderSeconds / 3600.0)')
    old = '''WindowManager.Instance.Popup((ConfirmDialog panel) =>
\t\t\t{
\t\t\t\tpanel.Init(key, () =>
\t\t\t\t{
\t\t\t\t\tFastBedHangUp();
\t\t\t\t});
\t\t\t\tpanel.SetTitleText(titleKey);
\t\t\t});'''
    assert old in text
    text = text.replace(old, 'Services.Window.Confirm(key, titleKey, FastBedHangUp);')
    text = text.replace('HangUpUIUtils.OperFastHangUp(mFastHangUpId,', 'HangUpUIUtils.OperFastHangUp(Services, mFastHangUpId,')
    text = text.replace('\t\tFastHangUpInfoParser fastHangUpInfoParser = Parsers.Instance.FastHangUpInfoParser;\n', '')
    text = text.replace('fastHangUpInfoParser.TryGetFastHangUpItemInfo(mItem.ItemId, out var fastHangUpItem)',
                        'Services.TryGetFastHangUpItemHours(mItem.ItemId, out var hangUpHour)')
    text = text.replace('fastHangUpItem.HangUpHour', 'hangUpHour')
    # Only original diagnostics are removed; the missing-item branch continues.
    text = re.sub(r'\t\t\tGLoggerWarnInterpolatedStringHandler message =.*?\t\t\tLog.Warn\(ref message,[^\n]+\n', '', text, flags=re.S)
    emit('FurnitureFastHangUpDialog', text)

    text = method(read('HangUpUIUtils'), 'public static void OperFastHangUp(')
    text = text.replace('OperFastHangUp(int ', 'OperFastHangUp(IHomeFastHangUpServices services, int ')
    text = text.replace('PlayerFastHangUpRequest', 'HomeFastHangUpRequest').replace('PlayerFastHangUpResponse', 'HomeFastHangUpResponse')
    text = text.replace('ServiceProxyBase<PlayerServiceProxyImpl>.Instance.OperPlayerFastHangUp(request).OnComplete += (HomeFastHangUpResponse rep, int err) =>',
                        'services.RequestFastHangUp(request, (HomeFastHangUpResponse rep, int err) =>')
    text = text.replace('''WindowManager.Instance.Popup((FastHangUpProgressBarDialog go) =>
\t\t\t\t{
\t\t\t\t\tgo.Init(fastHangUpId, () =>''', 'services.OpenProgress(fastHangUpId, () =>')
    text = text.replace('PlayerAwardMsg', 'IHomeWindowAwardMessage')
    text = text.replace('Formula.Instance.MergeAward(', 'services.Window.MergeAward(')
    text = text.replace('MeManager.MainPlayer.Award(', 'services.Player.Award(')
    text = text.replace('''WindowManager.Instance.Popup((AwardPanel awardPanel) =>
\t\t\t\t\t\t\t{
\t\t\t\t\t\t\t\tawardPanel.Init(awardMsg);
\t\t\t\t\t\t\t});''', 'services.OpenAward(awardMsg);')
    text = text.replace('\t\t\t\t\t});\n\t\t\t\t});', '\t\t\t\t});')
    end = text.rindex('\t\t};\n')
    text = text[:end]+text[end:].replace('\t\t};\n', '\t\t});\n', 1)
    assert 'WindowManager' not in text and 'ServiceProxy' not in text
    emit('HangUpUIUtils', 'public static class HangUpUIUtils\n{\n'+text+'}\n')

    for name in ('HomeFastHangUpServices', 'OfficialHomeFastHangUpAssetCatalog'):
        path = DEST/(name+'.cs')
        for item in (path, metadata(path)): outputs[item.relative_to(ROOT).as_posix()] = sha(item)
    path = DEST/'Zjcs.Official.HomeFastHangUpUI.asmdef'
    path.write_text(json.dumps(dict(name='Zjcs.Official.HomeFastHangUpUI', references=[
        'Unity.ugui', 'Zjcs.Official.HomeUI', 'Zjcs.Official.HomeHangUpWindowsUI',
        'Zjcs.Official.HomeCountControlsUI', 'Zjcs.Official.ProfessionUI',
        'Zjcs.Official.WorldUI', 'Zjcs.Official.World.NativeLabelUI']), indent=2)+'\n', 'utf8', newline='\n')
    for item in (path, metadata(path), metadata(DEST, True)): outputs[item.relative_to(ROOT).as_posix()] = sha(item)
    for relative in ('HomeCountControlsUI/SetCountSlider.cs', 'ProfessionUI/GameObjectPool.cs',
                     'ProfessionUI/GameObjectPoolBase.cs', 'ProfessionUI/ProfessionGameObjectPoolBridge.cs'):
        path = ROOT/'unity/Assets/Game/Runtime/Official'/relative
        inputs[path.relative_to(ROOT).as_posix()] = sha(path)
    proof = dict(manifest='884_156692', generator_sha256=sha(Path(__file__)), inputs=inputs, outputs=outputs,
        scope='Original item acceleration selection, use-count slider, icon ownership and response/presentation ordering. Diagnostics only removed. Original progress/appearance window, native assets, actual local transactions and runtime validation remain separate.')
    (ROOT/'assets/reference/home-fast-hangup-runtime-source-manifest.json').write_text(json.dumps(proof, ensure_ascii=False, indent=2)+'\n', 'utf8', newline='\n')
    print('Original item acceleration:', len(outputs), 'outputs', flush=True)
    return proof


if __name__ == '__main__': build()
