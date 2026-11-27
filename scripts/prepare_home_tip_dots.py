"""Migrate official Home furniture hang-up TipDot condition nodes with explicit local ports."""
import hashlib
import json
from pathlib import Path
import re

from official_apk import ROOT
from official_source import OfficialSource


DEST = ROOT / 'unity/Assets/Game/Runtime/Official/HomeTipDots'
RECEIPT = ROOT / 'assets/reference/home-tip-dots-source-manifest.json'
TEMPLATE = Path(__file__).parent / 'templates/home_tip_dots/HomeTipDotServices.cs'
NODES = (
    'TipDotHasFreeFastHangUpTimesConditionNode',
    'TipDotHasItemFastHangUpTimesConditionNode',
    'TipDotHangUpItemCanUnlockConditionNode',
    'TipDotHangUpItemCanLevelUpConditionNode',
    'TipDotTreasureElementTypeHasTipConditionNode',
    'TipDotTreasurePieceElementTypeHasTipConditionNode',
    'TipDotTreasureStateHasTipConditionNode',
    'TipDotLinkTreasureElementItemNode',
)


def sha(raw: bytes) -> str:
    return hashlib.sha256(raw).hexdigest()


def replace(text: str, before: str, after: str) -> str:
    if text.count(before) != 1:
        raise ValueError('Original Home tip condition boundary differs: ' + before)
    return text.replace(before, after)


def prepare(*, check=False):
    selected = json.loads(RECEIPT.read_bytes()) if RECEIPT.exists() else None
    source = OfficialSource(selected['manifest'] if selected else None)
    code = ROOT / '.tools/source-packages/current' / source.version / 'official-code'
    manifest = code.parent / 'code-manifest.json'
    identity = json.loads(manifest.read_bytes())
    assembly = code / 'managed/decoded/Assembly-CSharp.dll'
    if identity['manifest'] != source.version or sha(assembly.read_bytes()) != identity['assemblies']['Assembly-CSharp.dll']['decoded_sha256']:
        raise ValueError('Selected Home tip condition assembly differs')
    inputs = {p.relative_to(ROOT).as_posix(): sha(p.read_bytes()) for p in (manifest, assembly)}
    for enum_name, local_path in (
        ('ElementType', ROOT / 'unity/Assets/Game/Runtime/Official/BattleSkillPlayables/ElementType.cs'),
        ('TreasureRedDotEnum', TEMPLATE)):
        original_path = code / 'common-source/Common' / (enum_name + '.cs')
        values = []
        for path in (original_path, local_path):
            raw = path.read_bytes()
            inputs[path.relative_to(ROOT).as_posix()] = sha(raw)
            body = re.search(r'\benum\s+' + enum_name + r'\s*\{([^}]+)\}', raw.decode('utf-8-sig'))
            if not body:
                raise ValueError('Resolve the complete original Home tip enum: ' + enum_name)
            values.append(dict(re.findall(r'\b(\w+)\s*=\s*(-?\d+)', body[1])))
        if not values[0] or values[0] != values[1]:
            raise ValueError('Home tip enum differs from its original serialized values: ' + enum_name)
    previous = json.loads(RECEIPT.read_bytes())['outputs'] if RECEIPT.exists() else {}
    outputs = {}

    def write(path: Path, raw: bytes):
        key = path.relative_to(ROOT).as_posix()
        if path.exists():
            current = path.read_bytes()
            if current != raw and sha(current) != previous.get(key) and sha(current.replace(b'\r\n', b'\n')) != previous.get(key):
                raise ValueError('Preserve another Home tip edit: ' + key)
        if check:
            if not path.is_file() or path.read_bytes() != raw:
                raise ValueError('Generated Home tip source differs: ' + key)
        else:
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(raw)
        outputs[key] = sha(raw)

    def metadata(path: Path, folder: bool = False):
        token = hashlib.md5(('zjcs:home-tip-dots:' + path.relative_to(ROOT).as_posix()).encode()).hexdigest()
        kind = 'DefaultImporter' if folder else 'AssemblyDefinitionImporter' if path.suffix == '.asmdef' else 'MonoImporter'
        text = 'fileFormatVersion: 2\nguid: ' + token + '\n' + ('folderAsset: yes\n' if folder else '')
        write(Path(str(path) + '.meta'), (text + kind + ':\n  externalObjects: {}\n').encode())

    def read(name: str) -> str:
        path = code / 'client-source' / (name + '.cs')
        raw = path.read_bytes()
        inputs[path.relative_to(ROOT).as_posix()] = sha(raw)
        text = raw.decode('utf-8-sig').replace('\r\n', '\n')
        return re.sub(r'(?m)^using [^\n]+;\n|^\s*//IL_[^\n]*\n|^\s*\[(?:MenuPath|Inject)[^\n]*\]\n', '', text)

    def emit(name: str, body: str):
        code_only = re.sub(r'//[^\n]*', '', body)
        for token in ('MeManager', 'Parsers.', 'Framework.', 'Disposable.', 'DisposableBag', 'SubscriberExtensions', 'CloneableHelper', '[Inject]'):
            if token in code_only:
                raise ValueError(f'Unbound Home tip dependency in {name}: {token}')
        header = (
            '// Selected original Home TipDot condition body; explicit local ports.\n'
            'using System;\n'
            'using System.Collections.Generic;\n'
            'using UnityEngine;\n'
            'using Zjcs.Official.BattleSkillPlayables;\n'
            'using Zjcs.Official.HomeUI;\n'
            'using Zjcs.Official.ProfessionUI;\n'
            'namespace Zjcs.Official.HomeTipDots\n{\n'
        )
        path = DEST / (name + '.cs')
        text = '\n'.join(line.rstrip() for line in (header + body + '\n}\n').split('\n'))
        write(path, text.encode('utf-8'))
        metadata(path)

    for name in NODES:
        text = read(name)
        text = text.replace(', IFillable<TipDotNodeBase>', '')
        if ' : TipDotConditionNodeBase' in text:
            text = replace(text, 'public class ' + name + ' : TipDotConditionNodeBase',
                '[Serializable]\npublic class ' + name + ' : TipDotConditionNodeBase, IHomeTipDotNode')
        elif ' : TipDotDynamicLinkNodeBase' in text:
            text = replace(text, 'public class ' + name + ' : TipDotDynamicLinkNodeBase',
                '[Serializable]\npublic class ' + name + ' : TipDotDynamicLinkNodeBase, IHomeTipDotNode')
        opening = text.index('{', text.index('public class ' + name)) + 1
        text = text[:opening] + '\n\tpublic IHomeTipDotServices Services { get; set; }\n' + text[opening:]
        text = text.replace('List<FastHangUpInfo>', 'List<HomeTipFastHangUpInfo>')
        text = text.replace('foreach (FastHangUpInfo ', 'foreach (HomeTipFastHangUpInfo ')
        text = text.replace('FastHangUpInfo fastHangUpInfo', 'HomeTipFastHangUpInfo fastHangUpInfo')
        text = text.replace('Parsers.Instance.FastHangUpInfoParser.TryGetFastHangUpInfo(', 'Services.TryGetFastHangUpInfo(')
        text = text.replace('Parsers.Instance.FastHangUpInfoParser.TryGetFastHangUpItemInfo(itemData.Info.ClassId, out var _)', 'Services.IsFastHangUpItem(itemData.Info.ClassId)')
        text = text.replace('Parsers.Instance.HangUpInfoParser.Get(hangUpId).BindFurniture', 'Services.GetHangUpBindFurniture(hangUpId)')
        text = text.replace('MeManager.MainPlayer', 'Services.Player')
        text = text.replace('IHomeTipDotPlayer mainPlayer = Services.Player;', 'IHomeTipDotPlayer mainPlayer = Services.Player;')
        text = text.replace('Player mainPlayer = Services.Player;', 'IHomeTipDotPlayer mainPlayer = Services.Player;')
        text = text.replace('mainPlayer.TryGetUnlockSingleHangUpData(hangUpId, out var _)', 'mainPlayer.HasUnlockedHangUp(hangUpId)')
        text = text.replace('PlayerItemDataWrap itemData, long preCount, AwardSourceData sourceData', 'HomeTipItemData itemData, long preCount, object sourceData')
        text = text.replace('CloneableHelper.ThrowException(source == null, GetType().Name);',
            'throw new ArgumentException(GetType().Name, nameof(source));')
        if name == 'TipDotHasFreeFastHangUpTimesConditionNode':
            text = replace(text, '\tprivate DisposableBag mEventDisposable;\n\tprivate ISubscriber<IEnterNewDay> mEnterNewDaySubscriber { get; set; }\n',
                '\tprivate IDisposable mEventDisposable;\n')
            text = replace(text,
                '\t\t\tmEventDisposable = default;\n\t\t\tDisposable.AddTo<IDisposable>(SubscriberExtensions.Subscribe<IEnterNewDay>(mEnterNewDaySubscriber, (Action<IEnterNewDay>)OnEnterNewDay, Array.Empty<MessageHandlerFilter<IEnterNewDay>>()), ref mEventDisposable);',
                '\t\t\tmEventDisposable = Services.SubscribeEnterNewDay(OnEnterNewDay);')
            text = text.replace('mEventDisposable.Dispose();', 'mEventDisposable?.Dispose();\n\t\tmEventDisposable = null;')
            text = text.replace('OnEnterNewDay(IEnterNewDay _)', 'OnEnterNewDay()')
        elif name == 'TipDotHangUpItemCanUnlockConditionNode':
            text = replace(text, 'FurnitureUtils.CheckHangUpUnlockMet(Services.Player, Furniture)',
                'Services.CheckHangUpUnlockMet(Furniture)')
        elif name == 'TipDotHangUpItemCanLevelUpConditionNode':
            text = re.sub(r'\bHangUpInfo\b', 'HomeTipHangUpInfo', text)
            text = replace(text, """\t\tif (NodeKey.InstanceInfo is HangUpNodeInstanceInfo hangUpNodeInstanceInfo)
\t\t{
\t\t\tHomeTipHangUpInfo item = Parsers.Instance.HangUpInfoParser.Get(hangUpNodeInstanceInfo.HangUpId);
\t\t\tif (mHangUpInfos == null)
\t\t\t{
\t\t\t\tmHangUpInfos = new List<HomeTipHangUpInfo>(1);
\t\t\t}
\t\t\tmHangUpInfos.Clear();
\t\t\tmHangUpInfos.Add(item);
\t\t}
\t\telse
\t\t{
\t\t\tParsers.Instance.HangUpInfoParser.RecordHangUpInfoDict.TryGetValue(Furniture, out mHangUpInfos);
\t\t}""", """\t\tif (NodeKey.InstanceInfo is HangUpNodeInstanceInfo hangUpNodeInstanceInfo)
\t\t{
\t\t\tif (mHangUpInfos == null)
\t\t\t{
\t\t\t\tmHangUpInfos = new List<HomeTipHangUpInfo>(1);
\t\t\t}
\t\t\tmHangUpInfos.Clear();
\t\t\tif (Services.TryGetHangUpInfo(hangUpNodeInstanceInfo.HangUpId, out HomeTipHangUpInfo item) && item != null)
\t\t\t{
\t\t\t\tmHangUpInfos.Add(item);
\t\t\t}
\t\t}
\t\telse
\t\t{
\t\t\tServices.TryGetHangUpInfos(Furniture, out mHangUpInfos);
\t\t}""")
            text = replace(text, 'mainPlayer.OnActiveableEntUpdate += MainPlayer_OnActiveableEntUpdate;',
                'mainPlayer.OnGoddessUpdate += MainPlayer_OnGoddessUpdate;')
            text = replace(text, 'mainPlayer.OnActiveableEntUpdate -= MainPlayer_OnActiveableEntUpdate;',
                'mainPlayer.OnGoddessUpdate -= MainPlayer_OnGoddessUpdate;')
            text = replace(text, """\tprotected virtual void MainPlayer_OnActiveableEntUpdate(MapActiveableEntityInfo info)
\t{
\t\tif (info.ActiveEntType == MapWorldActiveableEntityType.Goddess)
\t\t{
\t\t\tRefreshLevelUpInfo();
\t\t\tSetDirty();
\t\t}
\t}""", """\tprotected virtual void MainPlayer_OnGoddessUpdate()
\t{
\t\tRefreshLevelUpInfo();
\t\tSetDirty();
\t}""")
            text = replace(text, """\t\tHangUpUpgradeMaterialInfoParser hangUpUpgradeMaterialInfoParser = Parsers.Instance.HangUpUpgradeMaterialInfoParser;
\t\tmHangUpCanLevelUpItemCostDict.Clear();
\t\tint hangUpMaxLevel = Services.Player.GetHangUpMaxLevel();
\t\tforeach (HomeTipHangUpInfo mHangUpInfo in mHangUpInfos)
\t\t{
\t\t\tif (!mHangUpInfo.CanLevelUp)
\t\t\t{
\t\t\t\tcontinue;
\t\t\t}
\t\t\tint id = mHangUpInfo.Id;
\t\t\tif (Services.Player.TryGetUnlockSingleHangUpData(id, out var singleHangUpData))
\t\t\t{
\t\t\t\tint maxLevel = hangUpUpgradeMaterialInfoParser.GetMaxLevel(id);
\t\t\t\tif (singleHangUpData.Level < hangUpMaxLevel && singleHangUpData.Level != maxLevel)
\t\t\t\t{
\t\t\t\t\tmHangUpCanLevelUpItemCostDict[id] = hangUpUpgradeMaterialInfoParser.Get(id, singleHangUpData.Level + 1);
\t\t\t\t}
\t\t\t}
\t\t}""", """\t\tmHangUpCanLevelUpItemCostDict.Clear();
\t\tint hangUpMaxLevel = Services.Player.GetHangUpMaxLevel();
\t\tforeach (HomeTipHangUpInfo mHangUpInfo in mHangUpInfos)
\t\t{
\t\t\tif (!mHangUpInfo.CanLevelUp)
\t\t\t{
\t\t\t\tcontinue;
\t\t\t}
\t\t\tint id = mHangUpInfo.Id;
\t\t\tif (Services.Player.TryGetUnlockSingleHangUpData(id, out int currentLevel))
\t\t\t{
\t\t\t\tint maxLevel = Services.GetHangUpUpgradeMaxLevel(id);
\t\t\t\tif (currentLevel < hangUpMaxLevel && currentLevel != maxLevel)
\t\t\t\t{
\t\t\t\t\tDictionary<int, long> cost = Services.GetHangUpUpgradeCosts(id, currentLevel + 1);
\t\t\t\t\tif (cost != null)
\t\t\t\t\t{
\t\t\t\t\t\tmHangUpCanLevelUpItemCostDict[id] = cost;
\t\t\t\t\t}
\t\t\t\t}
\t\t\t}
\t\t}""")
        elif name in ('TipDotTreasureElementTypeHasTipConditionNode', 'TipDotTreasureStateHasTipConditionNode'):
            text = text.replace('private DisposableBag mDisposableBag;', 'private IDisposable mSettingSub;')
            text = text.replace('private ISubscriber<TreasureClientSetting> Subscriber { get; set; }', '')
            text = text.replace('private IPersistentState<TreasureClientSetting> PersistentState { get; set; }', '')
            text = text.replace(
                '\t\tmDisposableBag = default;\n\t\tDisposable.AddTo<IDisposable>(SubscriberExtensions.Subscribe<TreasureClientSetting>(Subscriber, (Action<TreasureClientSetting>)OnSettingUpdate, Array.Empty<MessageHandlerFilter<TreasureClientSetting>>()), ref mDisposableBag);',
                '\t\tmSettingSub = Services.SubscribeTreasureSettingUpdate(OnSettingUpdate);')
            text = text.replace('\t\tmDisposableBag.Dispose();', '\t\tmSettingSub?.Dispose();\n\t\tmSettingSub = null;')
            text = text.replace('OnSettingUpdate(TreasureClientSetting settings)', 'OnSettingUpdate()')
            text = text.replace('PersistentState.State.AutoMergeWhenLevelUp', 'Services.AutoMergeWhenLevelUp')
            text = text.replace('Services.Player.TreasureSlotData.AwakeAutoMergeEnable', 'Services.Player.AwakeAutoMergeEnable')
            text = text.replace('PlayerItemDataWrap itemData', 'HomeTipItemData itemData')
            text = text.replace('TreasureHelper.IsItemEffectTreasurePromote(itemData)', 'Services.IsItemEffectTreasurePromote(itemData)')
            text = text.replace('ClonerResolver.Instance.GetClonerWithVerify<List<TreasureRedDotEnum>>().Fill(ref TipType, obj.TipType);',
                'TipType = obj.TipType != null ? new List<TreasureRedDotEnum>(obj.TipType) : null;')
            text = text.replace('ClonerResolver.Instance.GetClonerWithVerify<List<TreasureRedDotEnum>>().Fill(ref mRedDots, obj.mRedDots);',
                'mRedDots = obj.mRedDots != null ? new List<TreasureRedDotEnum>(obj.mRedDots) : null;')
        elif name == 'TipDotTreasurePieceElementTypeHasTipConditionNode':
            text = text.replace('TreasureHelper.IsItemEffectTreasurePromote(itemData)', 'Services.IsItemEffectTreasurePromote(itemData)')
            text = text.replace('ClonerResolver.Instance.GetClonerWithVerify<List<TreasureRedDotEnum>>().Fill(ref TipType, obj.TipType);', '')
        elif name == 'TipDotLinkTreasureElementItemNode':
            text = text.replace('NodeManager.ConnectNodes(NodeKey, list);',
                '((IDynamicTipDotNodeManager)NodeManager).ConnectNodes(NodeKey, list);')
            text = text.replace('ClonerResolver.Instance.GetClonerWithVerify<List<ElementType>>().Fill(ref Elements, obj.Elements);',
                'Elements = obj.Elements != null ? new List<ElementType>(obj.Elements) : null;')
            text = text.replace('CloneableHelper.Fill<TipDotGraphNodeSelector, TipDotGraphNodeSelector>(ref mNodeSelector, obj.mNodeSelector);',
                'mNodeSelector = obj.mNodeSelector?.Clone();')
        emit(name, text)

    emit('HangUpNodeInstanceInfo', read('HangUpNodeInstanceInfo'))
    emit('TreasureElementNodeInstanceInfo', read('TreasureElementNodeInstanceInfo'))
    out_link = read('TipDotOutLinkNode')
    out_link = re.sub(r'^\s*\[(?:DisplayNodeJump|IgnoreMember)[^\n]*\]\n', '', out_link, flags=re.M)
    out_link = out_link.replace(', IFillable<TipDotNodeBase>', '')
    out_link = out_link.replace('CloneableHelper.ThrowException(source == null, GetType().Name);',
        'throw new ArgumentException(GetType().Name, nameof(source));')
    out_link = out_link.replace('CloneableHelper.Fill<TipDotGraphNodeSelector, TipDotGraphNodeSelector>(ref mNodeSelector, obj.mNodeSelector);',
        'mNodeSelector = obj.mNodeSelector?.Clone();')
    emit('TipDotOutLinkNode', out_link)
    dyn_link = read('TipDotDynamicLinkNodeBase')
    dyn_link = dyn_link.replace('ITipDotNodeManager ', 'IDynamicTipDotNodeManager ')
    dyn_link = dyn_link.replace('= NodeManager;', '= (IDynamicTipDotNodeManager)NodeManager;')
    emit('TipDotDynamicLinkNodeBase', dyn_link)

    inputs[TEMPLATE.relative_to(ROOT).as_posix()] = sha(TEMPLATE.read_bytes())
    for relative in ('common-source/Common/PlayerItemData.cs', 'common-source/Common/PlayerItemDataWrap.cs'):
        path = code / relative
        inputs[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
    write(DEST / TEMPLATE.name, TEMPLATE.read_bytes())
    metadata(DEST / TEMPLATE.name)
    definition = DEST / 'Zjcs.Official.HomeTipDots.asmdef'
    write(definition, (json.dumps(dict(
        name='Zjcs.Official.HomeTipDots',
        references=['Zjcs.Official.ProfessionUI', 'Zjcs.Official.HomeUI', 'Zjcs.Official.BattleSkillPlayables'],
    ), indent=2) + '\n').encode('utf-8'))
    metadata(definition)
    metadata(DEST, True)
    inputs[Path(__file__).relative_to(ROOT).as_posix()] = sha(Path(__file__).read_bytes())
    for relative in (
        'unity/Assets/Game/Runtime/Official/ProfessionUI/IDynamicTipDotNodeManager.cs',
        'unity/Assets/Game/Runtime/Official/ProfessionUI/IDynamicTipDotNodeManager.cs.meta',
        'unity/Assets/Game/Runtime/Official/PartnersUI/Roster/TipDots/PartnerTipDotServices.cs',
        'unity/Assets/Game/Runtime/Offline/HomeTipDots/OfflineHomeTipDotServices.cs',
        'unity/Assets/Game/Runtime/Offline/HomeTipDots/Zjcs.Offline.HomeTipDots.asmdef',
        'scripts/fixtures/home_desk_runtime_validation.cs', 'scripts/prepare_home_desk_validation.py',
        'scripts/test_home_desk_runtime.ps1'):
        path = ROOT / relative
        inputs[relative] = sha(path.read_bytes())

    raw = (json.dumps(dict(
        manifest=source.version,
        inputs=inputs,
        outputs=outputs,
        generator_sha256=sha(Path(__file__).read_bytes()),
        adaptations=[
            'Preserves official CheckShowTip formulas, HangUp unlock/item/new-day subscriptions, and numeric TipDotInfo.Data text formatting.',
            'Replaces MeManager/Parsers/MessagePipe with explicit IHomeTipDotServices and IHomeTipDotPlayer ports.',
        ],
        scope='Original Home fast-hangup and treasure condition/link nodes; dynamic graph operations share the manager contract used by the native Home graph host. Gameplay evaluators belong to their local feature owners.',
    ), ensure_ascii=False, indent=2) + '\n').encode('utf8')
    if check:
        if RECEIPT.read_bytes() != raw:
            raise ValueError('Home tip source receipt differs')
    else:
        RECEIPT.parent.mkdir(parents=True, exist_ok=True)
        RECEIPT.write_bytes(raw)
    print('Restored original Home tip conditions:', len(outputs), 'outputs')


if __name__ == '__main__':
    import argparse
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    prepare(check=parser.parse_args().check)
