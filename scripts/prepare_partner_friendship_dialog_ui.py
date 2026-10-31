"""Stage the complete selected official friendship/rules dialog controller."""
import hashlib
import json
from pathlib import Path
import re

from validation_workspace import validation_directory

ROOT = Path(__file__).resolve().parents[1]
DEST = Path('unity/Assets/Game/Runtime/Official/PartnersUI/FriendshipDialog')


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def build(work):
    version = json.loads((ROOT / 'official-source.json').read_bytes())['active']
    inputs, outputs = {}, {}
    folder = work / 'dialog-code'
    folder.mkdir(exist_ok=True)

    def source(relative):
        path = ROOT / '.tools/source-packages/official-code' / relative
        raw = path.read_bytes()
        if raw != (ROOT / '.tools/source-packages/current' / version / 'official-code' / relative).read_bytes():
            raise ValueError('Selected dialog source differs: ' + relative)
        inputs[path.relative_to(ROOT).as_posix()] = sha(raw)
        return raw.decode('utf-8-sig').replace('\r\n', '\n')

    def write(name, text):
        path = folder / name
        path.write_text(text, 'utf8', newline='\n')
        target = DEST / name
        guid = hashlib.md5(('zjcs-partner-ui:' + target.as_posix()).encode()).hexdigest()
        importer = 'MonoImporter' if path.suffix == '.cs' else 'AssemblyDefinitionImporter'
        metadata = Path(str(path) + '.meta')
        metadata.write_text('fileFormatVersion: 2\nguid: ' + guid + '\n' + importer
                            + ':\n  externalObjects: {}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n', 'utf8', newline='\n')
        for entry in (path, metadata):
            outputs[(DEST / entry.name).as_posix()] = sha(entry.read_bytes())

    text = source('client-source/NpcFriendshipDialog.cs')
    source('client-source/INpcFriendshipDialog.cs')
    source('common-source/Common/NpcInfo.cs')
    source('common-source/Common/NpcFriendshipLevelInfo.cs')
    source('common-source/Common/ConditionType.cs')
    text = re.sub(r'^using (Common|Framework(?:\.[\w.]+)?);\n', '', text, flags=re.M)
    text = re.sub(r'^using ZLinq;\n', '', text, flags=re.M)
    text = re.sub(r'^\[UIWindow\([^\n]+\)\]\n', '', text, flags=re.M)
    text = re.sub(r'^\s*//IL_[^\n]*\n', '', text, flags=re.M)
    text, count = re.subn(r'\t\[Inject\]\n\tprivate IItemIconManager ItemIconManager \{ get; set; \}', '''    private IPartnerFriendshipDialogUiServices services;
    public void Configure(IPartnerFriendshipDialogUiServices value)
    {
        services = value ?? throw new System.ArgumentNullException(nameof(value));
    }''', text)
    if count != 1:
        raise ValueError('Original friendship dialog injection declaration changed')
    replacements = {
        'INpcFriendshipDialog': 'IPartnerFriendshipDialog',
        'StateSwitcher<': 'WorldStateSwitcher<',
        '\t\tItemInfoParser itemInfoParser = Parsers.Instance.ItemInfoParser;\n': '',
        'mNpcInfo.GiftPreferenceDict.Keys.AsValueEnumerable().ToList()': 'mNpcInfo.GiftPreferenceDict.Keys.ToList()',
        'PlayerTaskDataUtils.sRewardItemComparer.RewardItemComparer(itemInfoParser.Get(a), itemInfoParser.Get(b))': 'services.CompareRewardItems(a, b)',
        'ItemIconManager.GetIcon': 'services.BorrowGiftIcon',
        'ItemIconManager.RecycleIcon': 'services.RecycleGiftIcon',
        'mNpcInfo.CanMeetCondition.Type == ConditionType.NpcFriendshipLevel': 'mNpcInfo.AssistFriendshipLevel.HasValue',
        'NpcFriendshipLevelCondition npcFriendshipLevelCondition = mNpcInfo.CanMeetCondition as NpcFriendshipLevelCondition;': 'int friendshipMeetLevel = mNpcInfo.AssistFriendshipLevel.Value;',
        'npcFriendshipLevelCondition.FriendshipLevel': 'friendshipMeetLevel',
        '!MeManager.MainPlayer.IsFunctionLimit(FunctionType.NpcAdventure)': '!services.IsTravelFunctionLimited',
        'Parsers.Instance.NpcFriendshipLevelInfoParser.Get(mNpcInfo.Id)': 'services.GetFriendshipLevels(mNpcInfo.Properties.Id)',
        'MeManager.MainPlayer.SubRank': 'services.SubRank',
        '.Init(mNpcInfo, item2.Key)': '.Init(mNpcInfo.Properties, item2.Key)',
        'bool bgVisible = mPropItems.Count % 2 > 0;': 'npcFriendshipPreviewPropItem.Configure(services);\n\t\t\tbool bgVisible = mPropItems.Count % 2 > 0;'
    }
    for old, new in replacements.items():
        if old not in text:
            raise ValueError('Original friendship dialog expression changed: ' + old)
        text = text.replace(old, new)
    if any(name in text for name in ('Parsers.', 'MeManager.', 'ItemIconManager', '[Inject]')):
        raise ValueError('Original friendship dialog still has unbound global services')
    aliases = '''using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
using MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;
using NpcInfo = Zjcs.Official.PartnersUI.PartnerFriendshipDialogNpc;
using NpcFriendshipLevelInfo = Zjcs.Official.PartnersUI.PartnerFriendshipDialogLevel;
'''
    write('NpcFriendshipDialog.cs', '// Selected official dialog; see partner-friendship-dialog-ui-source-manifest.json.\n'
          + 'namespace Zjcs.Official.PartnersUI\n{\n' + aliases + text + '\n}\n')
    write('PartnerFriendshipDialogUiServices.cs', '''using System.Collections.Generic;
using UnityEngine;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Official.PartnersUI
{
    // Read-only projections of the fields consumed by the original dialog.
    public sealed class PartnerFriendshipDialogNpc
    {
        public PartnerFriendshipNpcInfo Properties;
        public IReadOnlyDictionary<int, double> GiftPreferenceDict;
        public bool IsStarGodNpc;
        public object Name;
        // Present exactly when CanMeetCondition is NpcFriendshipLevelCondition.
        public int? AssistFriendshipLevel;
    }
    public sealed class PartnerFriendshipDialogLevel
    {
        public int SubRankLimit;
    }
    public interface IPartnerFriendshipDialog
    {
        void Init(PartnerFriendshipDialogNpc npcInfo);
    }
    public interface IPartnerFriendshipDialogUiServices : IPartnerFriendshipUiServices
    {
        int SubRank { get; }
        IReadOnlyDictionary<int, PartnerFriendshipDialogLevel> GetFriendshipLevels(int npcId);
        int CompareRewardItems(int firstId, int secondId);
        ItemIcon BorrowGiftIcon(int itemId, Transform slot);
        void RecycleGiftIcon(ItemIcon icon);
    }
}
''')
    write('Zjcs.Official.PartnerFriendshipDialogUI.asmdef', json.dumps(dict(
        name='Zjcs.Official.PartnerFriendshipDialogUI', rootNamespace='Zjcs.Official.PartnersUI',
        references=['Unity.ugui', 'Zjcs.Official.PartnerGiftUI', 'Zjcs.Official.WorldUI',
                    'Zjcs.Official.ProfessionUI', 'Zjcs.Official.World.NativeLabelUI']), indent=2) + '\n')
    receipt = dict(official_source=version, source_sha256=inputs, output_sha256=outputs,
                   generator_sha256=sha(Path(__file__).read_bytes()),
                   adaptations=['Explicit existing friendship/property, local player/configuration, reward-item comparison and icon-pool ports.',
                                'Original interface and consumed NPC/level/condition projections; no new reward or property rules.',
                                'Configure each original pooled preview before its existing initialization; preserve pool and callback order.',
                                'Window address registration is explicit in the eventual native window host.'],
                   scope='Complete official friendship/rules controller staged; native window host/local provider not yet installed.')
    (work / 'partner-friendship-dialog-ui-source-manifest.json').write_text(
        json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    print('Staged complete official friendship dialog:', len(outputs), 'outputs')


if __name__ == '__main__':
    build(validation_directory())
