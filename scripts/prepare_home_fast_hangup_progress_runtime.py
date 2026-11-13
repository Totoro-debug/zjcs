"""Migrate selected acceleration presentation and its original appearance overrides."""
import json
import re
from pathlib import Path
from prepare_home_auto_increase_runtime import ROOT, CODE, metadata, sha

DEST = ROOT/'unity/Assets/Game/Runtime/Official/HomeFastHangUpProgressUI'


def build():
    inputs, outputs = {}, {}

    def read(name, resolved=False):
        path = CODE/('client-source-resolved' if resolved else 'client-source')/(name+'.cs')
        inputs[path.relative_to(ROOT).as_posix()] = sha(path)
        if resolved:
            receipt = path.with_suffix('.decompilation.json')
            proof = json.loads(receipt.read_bytes())
            assert proof['sha256'] == sha(path)
            for relative, expected in proof['inputs'].items():
                assert sha(ROOT/relative) == expected, relative
                inputs[relative] = expected
            inputs[receipt.relative_to(ROOT).as_posix()] = sha(receipt)
        return path.read_text('utf-8-sig').replace('\r\n', '\n')

    def emit(name, text):
        path = DEST/(name+'.cs'); path.parent.mkdir(parents=True, exist_ok=True)
        header = '''// Selected original client; local ownership and native animation endpoints.
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using TMPro;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.HomeUI;
using Zjcs.Official.HomeHangUpWindowsUI;
using Zjcs.Official.WorldUI;
using PlayerAppearance = Zjcs.Official.CombatAppearance.PlayerAppearance;
using AppearanceUI = Zjcs.Official.CombatAppearance.AppearanceUI;
using ItemInfo = Zjcs.Official.ProfessionUI.IProfessionItemInfo;
namespace Zjcs.Official.HomeFastHangUpProgressUI
{
'''
        path.write_text(header+text+'\n}\n',encoding='utf8',newline='\n')
        for item in (path,metadata(path)): outputs[item.relative_to(ROOT).as_posix()] = sha(item)

    def clean(text):
        text = re.sub(r'^using [^\n]+;\n', '', text, flags=re.M)
        text = re.sub(r'^namespace [^\n]+;\n', '', text, flags=re.M)
        text = re.sub(r'^\[UIWindow\([^\n]+\n', '', text, flags=re.M)
        text = re.sub(r'\s*\[Inject\]\s*(private|public) [^\n]+ \w+ \{ get; set; \}', '', text)
        return text

    text = clean(read('FastHangUpProgressBarDialog', True))
    text = text.replace('public class FastHangUpProgressBarDialog : UIBase\n{',
        'public class FastHangUpProgressBarDialog : UIBase\n{\n    public IHomeFastHangUpProgressServices Services { protected get; set; }')
    text = text.replace('StateSwitcher<', 'WorldStateSwitcher<')
    text = text.replace('private R3.DisposableBag mDisposableBag;', 'private HomeWindowResourceBag mDisposableBag;')
    text = text.replace('mDisposableBag = default;', 'mDisposableBag = new HomeWindowResourceBag();')
    text = text.replace('mDisposableBag.Dispose();', 'mDisposableBag?.Dispose();')
    text = text.replace('Parsers.Instance.FastHangUpInfoParser.Get(', 'Services.FastHangUp.GetFastHangUpInfo(')
    text = text.replace('HangUpUtils.GetFastHangUpRewardItems(', 'Services.GetFastHangUpRewardItems(')
    old = 'ResourceLoaderExtensions.LoadSprite(spriteName: items[RandomUtils.Range(0, count)].Icon, resourceLoader: ResourceLoader).AddTo(ref mDisposableBag).Result'
    assert old in text
    helper = CODE/'framework-firstpass-source/ResourceLoaderExtensions.cs'
    inputs[helper.relative_to(ROOT).as_posix()] = sha(helper)
    assert 'resourceLoader.LoadAsset<Sprite>("Assets/Image/" + spriteName)' in helper.read_text('utf-8-sig')
    text = text.replace(old, 'mDisposableBag.Add(Services.Resources.LoadAsset<Sprite>("Assets/Image/" + items[Services.RandomRange(0, count)].Icon)).Result')
    text = text.replace('FastHangUpPublisher.Publish(default);', 'Services.PublishBedFastHangUp();')
    text = text.replace('UIRootManager.SetActiveUIVisible(', 'Services.SetActiveUIVisible(')
    text = text.replace('WindowManager.Instance.Close(', 'Services.Close(')
    emit('FastHangUpProgressBarDialog', text)

    text = clean(read('AppearanceSystem/AppearanceRuntimeDataOverride'))
    text = text.replace('private List<int> mMaleOverrideList;', 'private List<int> mMaleOverrideList;\n    private readonly IHomeFastHangUpProgressServices services;')
    text = text.replace('AppearanceRuntimeDataOverride(List<int> maleOverrideList, List<int> femaleOverrideList)',
        'AppearanceRuntimeDataOverride(IHomeFastHangUpProgressServices services, List<int> maleOverrideList, List<int> femaleOverrideList)')
    text = text.replace('AppearanceRuntimeDataOverride(IEnumerable<int> overrideList)', 'AppearanceRuntimeDataOverride(IHomeFastHangUpProgressServices services, IEnumerable<int> overrideList)')
    text = text.replace('\n\t\tUpdateOverride(', '\n        this.services = services;\n\t\tUpdateOverride(')
    text = text.replace('AppearanceData appearanceData,', 'object appearanceData,')
    text = text.replace('AppearanceUtils.ResolveAppendAppearanceMutex(appearanceData,', 'services.AppearanceResolver.ResolveAppendAppearanceMutex((AppearanceData)appearanceData,')
    text = text.replace('Parsers.Instance.SpineSkinInfoParser', 'services.AppearanceTables.SpineSkinInfoParser')
    text = text.replace('AppearanceUtils.IsFilterSkinId(', 'services.AppearanceResolver.IsFilterSkinId(')
    text = text.replace('CreateAppearanceOverride(List<int> maleOverrideList, List<int> femaleOverrideList)',
        'CreateAppearanceOverride(IHomeFastHangUpProgressServices services, List<int> maleOverrideList, List<int> femaleOverrideList)')
    text = text.replace('new AppearanceRuntimeDataOverride(maleOverrideList, femaleOverrideList)', 'new AppearanceRuntimeDataOverride(services, maleOverrideList, femaleOverrideList)')
    emit('AppearanceRuntimeDataOverride', text)

    text = clean(read('AppearanceSystem/PlayerAppearanceOverride'))
    text = text.replace('public class PlayerAppearanceOverride : MonoBehaviour\n{',
        'public class PlayerAppearanceOverride : MonoBehaviour\n{\n    public IHomeFastHangUpProgressServices Services { protected get; set; }')
    text = text.replace('AppearanceConfig.Value.GetOverrideConfig(', 'Services.AppearanceConfig.GetOverrideConfig(')
    text = text.replace('CreateAppearanceOverride(overrideConfig.', 'CreateAppearanceOverride(Services, overrideConfig.')
    emit('PlayerAppearanceOverride', text)

    text = clean(read('AppearanceSystem/MyAppearance'))
    text = text.replace('public class MyAppearance : MonoBehaviour\n{',
        'public class MyAppearance : MonoBehaviour\n{\n    public IHomeFastHangUpProgressServices Services { protected get; set; }')
    text = text.replace('Object.op_Implicit((Object)(object)mPlayerAppearance)', 'mPlayerAppearance')
    text = text.replace('TryGetComponent<PlayerAppearanceOverride>(ref playerAppearanceOverride)', 'TryGetComponent<PlayerAppearanceOverride>(out playerAppearanceOverride)')
    text = text.replace('MeManager.MainPlayer.Id', 'Services.MainPlayerId')
    emit('MyAppearance', text)

    text = clean(read('TMPFloatEffect'))
    text = re.sub(r'^\s*//IL_[^\n]*\n', '', text, flags=re.M)
    emit('TMPFloatEffect', text)

    for name in ('HomeFastHangUpProgressServices','OfficialHomeFastHangUpProgressAssetCatalog'):
        path=DEST/(name+'.cs')
        for item in (path,metadata(path)): outputs[item.relative_to(ROOT).as_posix()]=sha(item)
    path=DEST/'Zjcs.Official.HomeFastHangUpProgressUI.asmdef'
    path.write_text(json.dumps(dict(name='Zjcs.Official.HomeFastHangUpProgressUI',references=[
        'Unity.ugui','Unity.TextMeshPro','Zjcs.Official.HomeFastHangUpUI','Zjcs.Official.HomeHangUpWindowsUI',
        'Zjcs.Official.HomeUI','Zjcs.Official.ProfessionUI','Zjcs.Official.CombatAppearance','Zjcs.Official.WorldUI']),indent=2)+'\n',encoding='utf8',newline='\n')
    for item in (path,metadata(path),metadata(DEST,True)): outputs[item.relative_to(ROOT).as_posix()]=sha(item)
    offline = ROOT/'unity/Assets/Game/Runtime/Offline/HomeFastHangUpProgressUI'
    for path in offline.iterdir():
        if path.suffix not in ('.cs', '.asmdef'): continue
        for item in (path, metadata(path)): outputs[item.relative_to(ROOT).as_posix()] = sha(item)
    path = metadata(offline, True); outputs[path.relative_to(ROOT).as_posix()] = sha(path)
    # Independent Canvas endpoint: initialization keeps the serialized name but
    # only plays an animation present in the selected original skeleton.
    path = ROOT/'unity/Assets/Game/Runtime/Offline/ProfessionUI/ProfessionNativeUiGraphic.cs'
    outputs[path.relative_to(ROOT).as_posix()] = sha(path)
    for path in (CODE/'client-source/AppearanceSystem.Runtime/AppearanceUI.cs',
            CODE/'client-source/AppearanceSystem.Runtime/AppearanceAnimUtils.cs',
            ROOT/'unity/Assets/Spine/Runtime/spine-unity/Components/SkeletonGraphic.cs'):
        inputs[path.relative_to(ROOT).as_posix()] = sha(path)
    proof=dict(manifest='884_156692',generator_sha256=sha(Path(__file__)),inputs=inputs,outputs=outputs,
        adaptation='Original progress controller, self appearance initialization and sex-specific/mutex-aware overrides. Explicit local services replace global managers; existing native current UI actor remains the animation endpoint. Native hierarchy and runtime verification are recorded separately.')
    (ROOT/'assets/reference/home-fast-hangup-progress-runtime-source-manifest.json').write_text(json.dumps(proof,ensure_ascii=False,indent=2)+'\n',encoding='utf8',newline='\n')
    print('Original acceleration presentation:',len(outputs),'outputs',flush=True)


if __name__=='__main__': build()
