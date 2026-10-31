"""Port the selected official gift icon and friendship property widgets."""
import hashlib
import json
from pathlib import Path
import re

from prepare_partner_ui_helpers import meta

ROOT = Path(__file__).resolve().parents[1]
DEST = ROOT / 'unity/Assets/Game/Runtime/Official/PartnersUI/Gifts'


def metadata(path, folder=False):
    meta(path, folder=folder)
    entry = Path(str(path) + '.meta')
    entry.write_bytes(entry.read_bytes().replace(b'\r\n', b'\n'))


def main():
    version = json.loads((ROOT / 'official-source.json').read_bytes())['active']
    inputs, outputs = {}, {}
    DEST.mkdir(parents=True, exist_ok=True)
    metadata(DEST, folder=True)

    def source(name):
        path = ROOT / '.tools/source-packages/official-code' / name
        raw = path.read_bytes()
        if raw != (ROOT / '.tools/source-packages/current' / version / 'official-code' / name).read_bytes():
            raise ValueError('Official source differs from selection: ' + name)
        inputs[path.relative_to(ROOT).as_posix()] = hashlib.sha256(raw).hexdigest()
        return raw.decode('utf-8-sig').replace('\r\n', '\n')

    def write(name, text):
        path = DEST / name
        path.write_text(text, 'utf8', newline='\n')
        metadata(path)
        for entry in (path, Path(str(path) + '.meta')):
            outputs[entry.relative_to(ROOT).as_posix()] = hashlib.sha256(entry.read_bytes()).hexdigest()

    def port(name, text):
        text = re.sub(r'^using (Common|Framework(?:\.[\w.]+)?);\n', '', text, flags=re.M)
        text = re.sub(r'^\s*//IL_[^\n]*\n', '', text, flags=re.M)
        text = text.replace('StateSwitcher<', 'WorldStateSwitcher<')
        aliases = '''using Object = UnityEngine.Object;
using MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;
using PlayerPropItem = Zjcs.Official.ProfessionUI.ProfessionPlayerPropItem;
using PropType = Zjcs.Official.ProfessionUI.ProfessionPropertyType;
using PropConfigUtils = Zjcs.Official.ProfessionUI.ProfessionPropertyConfig;
using LanguageUtils = Zjcs.Official.ProfessionUI.ProfessionPropertyLanguage;
using NpcInfo = Zjcs.Official.PartnersUI.PartnerFriendshipNpcInfo;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
'''
        if any(value in text for value in ('MeManager.', 'Parsers.', 'GLogger', 'Log.Error')):
            raise ValueError('Unbound original dependency: ' + name)
        write(name + '.cs', '// Migrated selected official client; see partner-gift-ui-source-manifest.json.\n'
              + 'namespace Zjcs.Official.PartnersUI\n{\n' + aliases + text + '\n}\n')

    text = source('client-source/ItemIconNpcGift.cs')
    text, count = re.subn(r'(?ms)^\t{3}GLoggerErrorInterpolatedStringHandler message =.*?^\t{3}Log.Error\(ref message,[^\n]*\);',
                         '\t\t\tDebug.LogError("AttachReduceClickEvent mReduceBtn need Button component");', text)
    if count != 1:
        raise ValueError('Original gift icon diagnostic changed')
    port('ItemIconNpcGift', text)

    for name in ('NpcFriendshipPropItem', 'NpcFriendshipPreviewPropItem', 'NpcFriendshipPropItemGroup'):
        text = source('client-source/' + name + '.cs')
        start = text.index('\n{', text.index('public class ')) + 2
        configure = '''
    private IPartnerFriendshipUiServices services;
    public void Configure(IPartnerFriendshipUiServices value)
    {
        services = value ?? throw new System.ArgumentNullException(nameof(value));
'''
        if name == 'NpcFriendshipPropItemGroup':
            configure += '        foreach (var item in propItems) item.Configure(value);\n'
        else:
            configure += '        mPropItem.Configure(value.Properties);\n'
        text = text[:start] + configure + '    }\n' + text[start:]
        text = text.replace('Parsers.GameSettings.NpcAdventureSettings.TravelNpcPropTypeList', 'services.TravelNpcPropTypeList')
        text = text.replace('!MeManager.MainPlayer.IsFunctionLimit(FunctionType.NpcAdventure)', '!services.IsTravelFunctionLimited')
        text = text.replace('MeManager.MainPlayer.IsFunctionLimit(FunctionType.NpcAdventure);', '_ = services.IsTravelFunctionLimited;')
        text = text.replace('Parsers.Instance.NpcInfoParser.GetFriendshipLevelPropDiff', 'services.GetFriendshipLevelPropDiff')
        text = text.replace('Parsers.Instance.LevelPropParser.GetProps', 'services.GetLevelProps')
        if name == 'NpcFriendshipPreviewPropItem':
            pattern = r'(?ms)^(?P<indent>\t+)GLoggerErrorInterpolatedStringHandler message =.*?^(?P=indent)Log.Error\(ref message,[^\n]*\);'
            messages = iter([
                'Debug.LogError("level_prop_npc_friendship.csv: at most two property differences, CurrentLevel:" + (mLevel - 1));',
                'Debug.LogError("level_prop_npc_friendship.csv: no positive property change, NpcId:" + mNpcInfo.Id + ", Level:" + mLevel);'
            ])
            text, count = re.subn(pattern, lambda match: match['indent'] + next(messages), text)
            if count != 2:
                raise ValueError('Original friendship preview diagnostics changed')
        port(name, text)

    extension = source('framework-source/Framework/CanvasGroupExtensions.cs')
    port('PartnerCanvasGroupExtensions', extension.replace('namespace Framework;\n', '').replace('class CanvasGroupExtensions', 'class PartnerCanvasGroupExtensions'))
    source('common-source/Common/NpcInfo.cs')
    source('common-source/Common/NpcInfoParser.cs')
    source('common-source/Common/LanguageUtils.cs')
    write('Zjcs.Official.PartnerGiftUI.asmdef', json.dumps(dict(name='Zjcs.Official.PartnerGiftUI',
        rootNamespace='Zjcs.Official.PartnersUI', references=['Unity.ugui', 'Zjcs.Official.WorldUI',
            'Zjcs.Official.ProfessionUI', 'Zjcs.Official.World.NativeLabelUI']), indent=2) + '\n')
    adapter = DEST / 'PartnerFriendshipUiServices.cs'
    if not adapter.is_file():
        raise ValueError('Friendship UI service boundary is missing')
    metadata(adapter)
    for path in (adapter, Path(str(adapter) + '.meta'), Path(str(DEST) + '.meta')):
        outputs[path.relative_to(ROOT).as_posix()] = hashlib.sha256(path.read_bytes()).hexdigest()
    generator = Path(__file__)
    helper = ROOT / 'scripts/prepare_partner_ui_helpers.py'
    receipt = dict(format=1, official_source=version, source_sha256=inputs, output_sha256=outputs,
        generator_sha256=hashlib.sha256(generator.read_bytes()).hexdigest(),
        helper_sha256=hashlib.sha256(helper.read_bytes()).hexdigest(),
        adaptations=['Explicit configuration/function service boundary; original property value formatting and shared PlayerPropItem.',
                     'Existing official ItemIcon base; original reduction button subscription, clearing and preference/selection states.',
                     'Unity diagnostics replace decompiler logging handlers; original diagnostic branches and return order remain.'],
        scope='Official gift icon and friendship property widgets; full native gift/detail window and local data provider remain pending.')
    (ROOT / 'assets/reference/partner-gift-ui-source-manifest.json').write_text(
        json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    print('Migrated official partner gift/property UI:', len(outputs), 'files')


if __name__ == '__main__':
    main()
