"""Migrate the selected complete Bag graph's original condition-node bodies."""
import hashlib
import json
from pathlib import Path
import re

from official_apk import ROOT
from official_source import OfficialSource
from prepare_unity_profession_appearance_resolver import method


DEST = ROOT / 'unity/Assets/Game/Runtime/Official/BagTipDots'
RECEIPT = ROOT / 'assets/reference/bag-tip-dots-source-manifest.json'
TEMPLATE = ROOT / 'scripts/templates/bag_tip_dots/BagTipDotServices.cs'
NODES = ('TipDotBagCanUseItemConditionNode', 'TipDotCanRecycleGemConditionNode',
    'TipDotCanRecycleItemConditionNode', 'TipDotEquipPurifyCondtionNode',
    'TipDotFurnitureCanRepairConditionNode', 'TipDotHasFurnitureConditionNode')


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def replace(text, before, after):
    if text.count(before) != 1:
        raise ValueError('Original Bag tip boundary differs: ' + before)
    return text.replace(before, after)


def prepare():
    source = OfficialSource()
    code = ROOT / '.tools/source-packages/current' / source.version / 'official-code'
    manifest = code.parent / 'code-manifest.json'
    identity = json.loads(manifest.read_bytes())
    inputs = {manifest.relative_to(ROOT).as_posix(): sha(manifest.read_bytes())}
    if identity['manifest'] != source.version:
        raise ValueError('Selected Bag tip source manifest differs')
    for name in ('Assembly-CSharp.dll', 'Console.dll'):
        path = code / 'managed/decoded' / name
        if sha(path.read_bytes()) != identity['assemblies'][name]['decoded_sha256']:
            raise ValueError('Selected Bag tip assembly differs: ' + name)
        inputs[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
    previous = json.loads(RECEIPT.read_bytes())['outputs'] if RECEIPT.exists() else {}
    outputs = {}

    def write(path, raw):
        key = path.relative_to(ROOT).as_posix()
        if path.exists() and path.read_bytes() != raw and sha(path.read_bytes()) != previous.get(key):
            raise ValueError('Preserve another Bag tip change: ' + key)
        path.parent.mkdir(parents=True, exist_ok=True)
        if not path.exists() or path.read_bytes() != raw:
            path.write_bytes(raw)
        outputs[key] = sha(raw)

    def metadata(path, folder=False):
        token = hashlib.md5(('zjcs:bag-tip-dots:' + path.relative_to(ROOT).as_posix()).encode()).hexdigest()
        kind = 'DefaultImporter' if folder else 'AssemblyDefinitionImporter' if path.suffix == '.asmdef' else 'MonoImporter'
        text = 'fileFormatVersion: 2\nguid: ' + token + '\n' + ('folderAsset: yes\n' if folder else '')
        write(Path(str(path) + '.meta'), (text + kind + ':\n  externalObjects: {}\n').encode())

    def read(relative):
        path = code / relative
        inputs[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
        text = path.read_text('utf-8-sig').replace('\r\n', '\n')
        return re.sub(r'(?m)^using [^\n]+;\n|^namespace Common;\n|^\s*//IL_[^\n]*\n|^\s*\[(?:MenuPath|SearchableEnum|Inject|Description)[^\n]*\]\n', '', text)

    def emit(name, body):
        if any(token in body for token in ('MeManager', 'Parsers.', 'Framework.', 'ClonerResolver', 'CloneableHelper', '[Inject]', 'DisposableBag', 'Log.Warn')):
            raise ValueError('Unbound original Bag tip dependency: ' + name)
        header = '// Selected original Bag condition/identity body; explicit local owners.\nusing System;\nusing System.Collections.Generic;\nusing System.Linq;\nusing Zjcs.Official.ProfessionUI;\nusing Zjcs.Official.HomeUI;\nnamespace Zjcs.Official.BagTipDots\n{\n'
        path = DEST / (name + '.cs')
        text = '\n'.join(line.rstrip() for line in (header + body + '\n}\n').split('\n'))
        write(path, text.encode())
        metadata(path)

    for name in ('ItemType', 'EquipPos', 'RedDotType', 'SourceOperate', 'FunctionType'):
        emit(name, read('common-source/Common/' + name + '.cs'))
    for name in NODES:
        text = read('client-source/' + name + '.cs')
        text = text.replace(', IFillable<TipDotNodeBase>', '').replace(', IGemSubscriber', ', IBagGemSubscriber')
        text = replace(text, 'public class ' + name + ' : TipDotConditionNodeBase',
            '[Serializable]\npublic class ' + name + ' : TipDotConditionNodeBase, IBagTipDotNode')
        opening = text.index('{', text.index('public class ' + name)) + 1
        text = text[:opening] + '\n\tpublic IBagTipDotServices Services { get; set; }\n' + text[opening:]
        text = text.replace('MeManager.MainPlayer', 'Services.Player')
        text = text.replace('Parsers.GameSettings.', 'Services.Settings.')
        text = text.replace('Parsers.Instance.FurnitureInfoParser.Get(', 'Services.GetFurnitureInfo(')
        text = text.replace('MeManager.ConditionHandler.Subscribe(', 'Services.SubscribeCondition(')
        text = text.replace('Parsers.Instance.FunctionLimitInfoParser.IsLimit(', 'Services.IsFunctionLimit(')
        text = text.replace('Parsers.Instance.ItemTagInfoParser.GetKeys()', 'Services.ItemTagKeys')
        text = text.replace('Common.RedDotType', 'RedDotType')
        text = text.replace('PlayerItemDataWrap', 'BagTipItem').replace('AwardSourceData', 'BagTipAward')
        text = re.sub(r'\bFurnitureInfo\b(?=\s+mFurnitureInfo|\s+FurnitureInfo)', 'BagTipFurnitureInfo', text)
        text = text.replace('ConditionHandlerRegistration', 'IDisposable')
        text = text.replace('SubRank obj', 'int obj').replace('Player mainPlayer', 'IBagTipDotPlayer mainPlayer')
        text = text.replace('CloneableHelper.ThrowException(source == null, GetType().Name);',
            'throw new ArgumentException(GetType().Name, nameof(source));')
        if name == 'TipDotBagCanUseItemConditionNode':
            text = text.replace('private DisposableBag mEventDisposable;', 'private IDisposable mEventDisposable;')
            text = replace(text, 'private ISubscriber<IEnterNewDay> EnterNewDaySubscriber { get; set; }\n', '')
            text = replace(text, 'Disposable.AddTo<IDisposable>(SubscriberExtensions.Subscribe<IEnterNewDay>(EnterNewDaySubscriber, (Action<IEnterNewDay>)OnEnterNewDay, Array.Empty<MessageHandlerFilter<IEnterNewDay>>()), ref mEventDisposable);',
                'mEventDisposable = Services.SubscribeEnterNewDay(OnEnterNewDay);')
            text = text.replace('OnEnterNewDay(IEnterNewDay _)', 'OnEnterNewDay()')
            text = text.replace('TipDotBagUtils.IsExcludedFromShowCanUseRedDot(',
                'TipDotBagUtils.IsExcludedFromShowCanUseRedDot(Services.Settings, ')
            text = text.replace('ClonerResolver.Instance.GetClonerWithVerify<List<ItemType>>().Fill(ref CheckTypes, obj.CheckTypes);',
                'CheckTypes = obj.CheckTypes == null ? null : new List<ItemType>(obj.CheckTypes);')
        elif name == 'TipDotCanRecycleGemConditionNode':
            text = text.replace('IGemMergeManager', 'IBagGemMergeManager')
            text = text.replace('NodeManager.ScopeDataCollector?.GemMergeManager', 'Services.GemMergeManager')
        elif name == 'TipDotCanRecycleItemConditionNode':
            begin = text.index('\t\t\tGLoggerWarnInterpolatedStringHandler message =')
            end = text.index('\n\t\t}', begin)
            literal = re.search(r'message\.AppendLiteral\(("(?:[^"\\]|\\.)*")\)', text[begin:end])
            if literal is None:
                raise ValueError('Preserve the original Bag warning text')
            text = text[:begin] + '\t\t\tUnityEngine.Debug.LogWarning(' + literal[1] + ');' + text[end:]
        elif name == 'TipDotEquipPurifyCondtionNode':
            text = text.replace('ClonerResolver.Instance.GetClonerWithVerify<List<EquipPos>>().Fill(ref EquipPosList, obj.EquipPosList);',
                'EquipPosList = obj.EquipPosList == null ? null : new List<EquipPos>(obj.EquipPosList);')
        emit(name, text)
    text = read('client-source/Common/TipDotBagUtils.cs')
    text = replace(text, 'IsExcludedFromShowCanUseRedDot(ItemType itemType)',
        'IsExcludedFromShowCanUseRedDot(IBagTipDotSettings settings, ItemType itemType)')
    emit('TipDotBagUtils', text.replace('Parsers.GameSettings.', 'settings.'))
    text = read('client-source/EquipSlotNodeInstanceInfo.cs')
    # Graph evaluation consumes only this instance identity, not its debug label.
    text = text.replace(method(text, 'public override string GetInstanceDesc()'), '')
    emit('EquipSlotNodeInstanceInfo', text)
    inputs[TEMPLATE.relative_to(ROOT).as_posix()] = sha(TEMPLATE.read_bytes())
    write(DEST / TEMPLATE.name, TEMPLATE.read_bytes()); metadata(DEST / TEMPLATE.name)
    definition = DEST / 'Zjcs.Official.BagTipDots.asmdef'
    write(definition, (json.dumps(dict(name='Zjcs.Official.BagTipDots', references=[
        'Zjcs.Official.ProfessionUI', 'Zjcs.Official.HomeUI']), indent=2) + '\n').encode())
    metadata(definition); metadata(DEST, True)
    inputs[Path(__file__).relative_to(ROOT).as_posix()] = sha(Path(__file__).read_bytes())
    validations = [ROOT / 'scripts/fixtures/bag_tip_dot_validation.cs', ROOT / 'scripts/test_bag_tip_dots.ps1']
    RECEIPT.write_text(json.dumps(dict(manifest=source.version, inputs=inputs, outputs=outputs,
        validation_inputs={p.relative_to(ROOT).as_posix(): sha(p.read_bytes()) for p in validations},
        adaptations=[
            'Original six condition bodies, event ordering, dirty notifications and release branches are retained.',
            'Global player/configuration/message owners become explicit query/event ports; player predicates remain their owners responsibility.',
            'Managed reference types retain serialized field names and original enum values; native graph links remain separate assets.',
            'EquipSlotNodeInstanceInfo retains its constructor/equality/hash identity; its editor debug description is outside this selected surface.',
            'Cloneable collection copies use independent local lists; logging uses Unity.'
        ], scope='Original condition component migration. Local player ownership, complete Bag graph and playable UI integration require separate verification.'),
        ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    print('Restored original Bag tip conditions:', len(outputs), 'outputs')


if __name__ == '__main__':
    prepare()
