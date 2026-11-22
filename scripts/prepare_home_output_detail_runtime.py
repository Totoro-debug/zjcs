"""Port original output-addition popup controllers with explicit local services."""
import hashlib
import json
from pathlib import Path
import re

from official_apk import ROOT
from official_source import OfficialSource

DEST = ROOT / 'unity/Assets/Game/Runtime/Official/HomeOutputDetailUI'
RECEIPT = ROOT / 'assets/reference/home-output-detail-runtime-source-manifest.json'


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def metadata(path, folder=False):
    meta = Path(str(path) + '.meta')
    if not meta.exists():
        token = hashlib.md5(('zjcs:home-output-detail:' + path.relative_to(ROOT).as_posix()).encode()).hexdigest()
        kind = ('folderAsset: yes\nDefaultImporter' if folder else 'AssemblyDefinitionImporter' if path.suffix == '.asmdef'
                else 'NativeFormatImporter' if path.suffix == '.asset' else 'MonoImporter')
        meta.write_text('fileFormatVersion: 2\nguid: ' + token + '\n' + kind + ':\n  externalObjects: {}\n', 'utf8', newline='\n')
    return meta


def replace(text, old, new):
    if old not in text:
        raise ValueError('Original output detail source changed: ' + old[:100])
    return text.replace(old, new)


def build():
    version = OfficialSource().version
    code = ROOT / '.tools/source-packages/current' / version / 'official-code'
    inputs, outputs = {}, {}
    DEST.mkdir(parents=True, exist_ok=True)
    header = '''// Original selected output-addition UI; local data and input boundaries only.
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.HomeOutputAddition;
using Zjcs.Official.HomeHangUpWindowsUI;
using Zjcs.Official.SkillsUI;
using Zjcs.Official.PartnersUI;
using Zjcs.Official.WorldUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
using MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;
using OutputGroupType = Zjcs.Official.HomeOutputAddition.OutputGroupType;
namespace Zjcs.Official.HomeOutputDetailUI
{
'''
    for name in ('PopTipOutputAddition', 'OutputAdditionItem', 'OutputAdditionPropItem'):
        source = code / 'client-source' / (name + '.cs')
        raw = source.read_bytes()
        inputs[source.relative_to(ROOT).as_posix()] = sha(raw)
        text = raw.decode('utf-8-sig').replace('\r\n', '\n')
        text = re.sub(r'^using [^\n]+;\n|^\[UIWindow\([^\n]+\)\]\n|^\s*//IL_[^\n]*\n', '', text, flags=re.M)
        text = re.sub(r'\s*\[Inject\]\n\s*protected IInputOperationModule InputOperationModule \{ get; set; \}', '', text)
        text = re.sub(r'(public class \w+ : [^\n]+\n\{)', r'\1\n    public IOutputAdditionDetailServices Services { get; set; }', text, count=1)
        text = text.replace('DisposableBag mDisposableBag', 'HomeWindowResourceBag mDisposableBag')
        text = text.replace('mDisposableBag = default;', 'mDisposableBag = new HomeWindowResourceBag();')
        text = text.replace('mDisposableBag.Dispose();', 'mDisposableBag?.Dispose();')
        text = text.replace('StateSwitcher<', 'WorldStateSwitcher<')
        text = text.replace('LanguageUtils.GetEnumKey(', 'OutputDetailLanguage.EnumKey(')
        text = text.replace('LanguageManager.Instance.GetText(', 'Services.Text(')
        text = text.replace('OutputAdditionUtils.ToPercentage(mInfo.OutputAdditionPropVal)', '((float)mInfo.OutputAdditionPropVal / 10000f * 100f)')
        if name == 'PopTipOutputAddition':
            text = replace(text, 'InputOperationModule.HookOnInput<OnGlobalPointerDownResult>(OnGameObjectPointerDown);', 'Services.HookGlobalPointerDown(OnGameObjectPointerDown);')
            text = replace(text, 'InputOperationModule.UnhookOnInput<OnGlobalPointerDownResult>(OnGameObjectPointerDown);', 'Services.UnhookGlobalPointerDown(OnGameObjectPointerDown);')
            text = replace(text, 'OutputAdditionUtils.GetCurrOutputGroupPropList(MeManager.MainPlayer, mOutputGroupType)', 'Services.Groups(mOutputGroupType)')
            text = text.replace('UIUtils.GetRealCanvasSize(', 'PartnerAdventureUiUtils.GetRealCanvasSize(')
            text = text.replace('UIUtils.SetRectPopPosInScreen(', 'PartnerAdventureUiUtils.SetRectPopPosInScreen(')
            text = text.replace('Vector2.op_Implicit(Input.mousePosition)', 'Services.PointerPosition').replace('ref pos);', 'out pos);')
            text = replace(text, 'OnGameObjectPointerDown(OnGlobalPointerDownResult result)', 'OnGameObjectPointerDown(Vector2 screenPosition)')
            text = replace(text, 'UIUtils.CheckIfClickInsideUI(mRect, result.ScreenPos)', 'PartnerAdventureUiUtils.CheckIfClickInsideUI(mRect, screenPosition, mCanvas.worldCamera)')
            text = text.replace('WindowManager.Instance.Close(', 'Services.Close(')
        if name == 'OutputAdditionItem':
            text = replace(text, 'ETimeSpanFormatType.Brief', '1')
            text = replace(text, 'mTransform.DestroyChildren();', 'foreach (Transform child in mTransform) Destroy(child.gameObject);')
            text = replace(text, 'UIUtils.Instantiate<OutputAdditionPropItem>(mPropItemPrefab, mTransform, EUIInstantiateFlag.AutoActive)', 'Services.Instantiate(mPropItemPrefab.gameObject, mTransform).GetComponent<OutputAdditionPropItem>()')
        if name == 'OutputAdditionPropItem':
            text = replace(text, 'new CultureTextArg(OutputDetailLanguage.EnumKey(mInfo.OutputAdditionPropType))',
                'new CultureTextArg(Services.Text(OutputDetailLanguage.EnumKey(mInfo.OutputAdditionPropType)), Array.Empty<object>(), false)')
        path = DEST / (name + '.cs')
        path.write_text(header + text.strip() + '\n}\n', 'utf8', newline='\n')
        for item in (path, metadata(path)):
            outputs[item.relative_to(ROOT).as_posix()] = sha(item.read_bytes())
    path = DEST / 'Zjcs.Official.HomeOutputDetailUI.asmdef'
    path.write_text(json.dumps(dict(name='Zjcs.Official.HomeOutputDetailUI', references=[
        'Unity.ugui', 'Zjcs.Official.ProfessionUI', 'Zjcs.Official.HomeOutputAddition',
        'Zjcs.Official.HomeHangUpWindowsUI', 'Zjcs.Official.SkillsUI', 'Zjcs.Official.PartnerDetailUI',
        'Zjcs.Official.WorldUI', 'Zjcs.Official.World.NativeLabelUI']), indent=2) + '\n', 'utf8', newline='\n')
    for item in (path, metadata(path), metadata(DEST, True), DEST / 'IOutputAdditionDetailServices.cs', metadata(DEST / 'IOutputAdditionDetailServices.cs'),
                 DEST / 'OfficialHomeOutputDetailAssets.cs', metadata(DEST / 'OfficialHomeOutputDetailAssets.cs')):
        outputs[item.relative_to(ROOT).as_posix()] = sha(item.read_bytes())
    for name in ('OutputAdditionInfo', 'OutputGroupType', 'OutputActionType', 'OutputAdditionPropType', 'OutputAdditionUtils', 'LanguageUtils'):
        path = code / 'common-source/Common' / (name + '.cs')
        inputs[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
    for path in (code / 'client-source/UIUtils.cs', code / 'framework-firstpass-source/Framework/ETimeSpanFormatType.cs', Path(__file__)):
        inputs[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
    pool = ROOT / 'unity/Assets/Game/Runtime/Official/WorldUI/LoopScroll/LoopScrollSinglePrefabSource.cs'
    inputs[pool.relative_to(ROOT).as_posix()] = sha(pool.read_bytes())
    host_files = [ROOT / 'unity/Assets/Game/Runtime/Presentation/Home' / name for name in (
        'OfflineHomeOutputDetailServices.cs', 'OfflineHomeHangUpWindows.cs', 'HomeHudView.BedWindow.cs',
        'HomeHudView.cs', 'Zjcs.Presentation.Home.asmdef')]
    host_bindings = {path.relative_to(ROOT).as_posix(): sha(path.read_bytes()) for path in host_files}
    data = ROOT / 'unity/Assets/Game/Resources/Home/output-addition-rules.json'
    installed_rules = dict(path=data.relative_to(ROOT).as_posix(), sha256=sha(data.read_bytes()),
        usage='Reuse the existing migrated activity ledger/configuration; this UI migration does not replace it.')
    RECEIPT.write_text(json.dumps(dict(manifest=version, inputs=inputs, outputs=outputs,
        host_bindings=host_bindings, installed_rules=installed_rules,
        scope='Original popup, grouped source rows, countdown and percentage rows. Data, pointer, pool and window services are explicit; native assets and production verification are separate.'), ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')


if __name__ == '__main__':
    build()
