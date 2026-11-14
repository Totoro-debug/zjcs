"""Migrate selected LevelUpNotifyTip with explicit scene-owned service ports."""
import json
import re

from prepare_home_auto_increase_runtime import ROOT, CODE, metadata, sha

DEST = ROOT/'unity/Assets/Game/Runtime/Official/HomeLevelUpUI'


def build():
    source = CODE/'client-source/LevelUpNotifyTip.cs'
    code_manifest = ROOT/'assets/reference/official-client-code/source-manifest.json'
    provenance = json.loads(code_manifest.read_bytes())
    assembly = CODE/'managed/decoded/Assembly-CSharp.dll'
    proof = next(row for row in provenance['projects'] if row['assembly'] == assembly.name and row['folder'] == 'client-source')
    assert provenance['manifest'] == '884_156692' and sha(assembly) == proof['source']['decoded_sha256']
    original = source.read_text('utf-8-sig').replace('\r\n', '\n')
    text = re.sub(r'^using [^\n]+;\n', '', original, flags=re.M)
    text = re.sub(r'^\[UIWindow\([^\n]+\n', '', text, flags=re.M)
    text = re.sub(r'^\s*//IL_[^\n]*\n', '', text, flags=re.M)
    text = text.replace('public class LevelUpNotifyTip : UIBase\n{',
        'public class LevelUpNotifyTip : UIBase\n{\n    public IHomeLevelUpServices Services { private get; set; }')
    text = text.replace('\t[Inject]\n\tprivate IResourceLoader ResourceLoader { get; set; }', '')
    text = text.replace('DisposableBag mDisposableBag', 'HomeWindowResourceBag mDisposableBag')
    text = text.replace('mDisposableBag = default;', 'mDisposableBag = new HomeWindowResourceBag();')
    text = text.replace('\t\tPlayer mainPlayer = MeManager.MainPlayer;\n', '')
    for value in ('mFromLevel', 'mToLevel'):
        before = 'BattleFormula.Instance.CalcCharacterBattleProps(mainPlayer.Profession, mainPlayer.GetAllProfessionData(), '+value+', mainPlayer.SubRank)'
        assert text.count(before) == 1
        text = text.replace(before, 'Services.CalcCharacterBattleProps('+value+')')
    text = text.replace('\t\tPropCfgInfoParser propCfgInfoParser = Parsers.Instance.PropCfgInfoParser;\n', '')
    text = text.replace('BattleFormula.Instance.GetRawProp(', 'Services.GetRawProp(')
    text = text.replace('PropCfgInfo propCfgInfo = propCfgInfoParser.Get(mPropDatum.PropType);',
        'ProfessionPropertyDefinition propCfgInfo = Services.GetProperty(mPropDatum.PropType);')
    before = 'Disposable.AddTo<AssetOperationHandle<Sprite>>(ResourceLoader.LoadSprite(propCfgInfo.SmallIcon), ref mDisposableBag).Result'
    assert text.count(before) == 1
    text = text.replace(before, 'mDisposableBag.Add(Services.Resources.LoadAsset<Sprite>("Assets/Image/" + propCfgInfo.SmallIcon)).Result')
    text = text.replace('WindowManager.Instance.Close(', 'Services.Close(')
    text = text.replace('public static bool TryPopup(int startLevel)', 'public static bool TryPopup(IHomeLevelUpServices services, int startLevel)')
    text = text.replace('int currentLevel = MeManager.MainPlayer.Level;', 'int currentLevel = services.CurrentLevel;')
    text = text.replace('WindowManager.Instance.Popup((LevelUpNotifyTip go) =>', 'services.Popup((LevelUpNotifyTip go) =>')
    header = '''// Migrated selected official client; formula, resources and window service ports only.
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.HomeHangUpWindowsUI;
using PropType = Zjcs.Official.ProfessionUI.ProfessionPropertyType;
using PlayerPropItem = Zjcs.Official.ProfessionUI.ProfessionPlayerPropItem;
using MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;
using PopupWindow = Zjcs.Official.WorldUI.WorldPopupWindow;
using Object = UnityEngine.Object;
namespace Zjcs.Official.HomeLevelUpUI
{
'''
    DEST.mkdir(parents=True, exist_ok=True)
    (DEST/'LevelUpNotifyTip.cs').write_text(header+text+'\n}\n', 'utf8', newline='\n')
    definition = dict(name='Zjcs.Official.HomeLevelUpUI', references=[
        'Unity.ugui', 'Zjcs.Official.ProfessionUI', 'Zjcs.Official.WorldUI', 'Zjcs.Official.HomeHangUpWindowsUI'])
    (DEST/'Zjcs.Official.HomeLevelUpUI.asmdef').write_text(json.dumps(definition, indent=2)+'\n', 'utf8', newline='\n')
    outputs = {}
    for path in sorted(DEST.iterdir()):
        if path.suffix not in ('.cs', '.asmdef'): continue
        for item in (path, metadata(path)): outputs[item.relative_to(ROOT).as_posix()] = sha(item)
    folder = metadata(DEST, True); outputs[folder.relative_to(ROOT).as_posix()] = sha(folder)
    helper = CODE/'framework-firstpass-source/ResourceLoaderExtensions.cs'
    assert 'resourceLoader.LoadAsset<Sprite>("Assets/Image/" + spriteName)' in helper.read_text('utf-8-sig')
    return dict(version='884_156692', inputs={p.relative_to(ROOT).as_posix(): sha(p)
        for p in (source, helper, assembly, code_manifest, ROOT/'scripts/prepare_home_level_up_runtime.py')}, outputs=outputs,
        adaptations=['Scene-owned formula/player/property/resource/window ports',
                     'Original init, comparison, visibility and close-on-show ordering retained'])


if __name__ == '__main__':
    print(json.dumps(build(), ensure_ascii=False, indent=2))
