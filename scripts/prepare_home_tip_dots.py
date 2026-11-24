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
)


def sha(raw: bytes) -> str:
    return hashlib.sha256(raw).hexdigest()


def replace(text: str, before: str, after: str) -> str:
    if text.count(before) != 1:
        raise ValueError('Original Home tip condition boundary differs: ' + before)
    return text.replace(before, after)


def prepare():
    source = OfficialSource()
    code = ROOT / '.tools/source-packages/current' / source.version / 'official-code'
    manifest = code.parent / 'code-manifest.json'
    identity = json.loads(manifest.read_bytes())
    assembly = code / 'managed/decoded/Assembly-CSharp.dll'
    if identity['manifest'] != source.version or sha(assembly.read_bytes()) != identity['assemblies']['Assembly-CSharp.dll']['decoded_sha256']:
        raise ValueError('Selected Home tip condition assembly differs')
    inputs = {p.relative_to(ROOT).as_posix(): sha(p.read_bytes()) for p in (manifest, assembly)}
    previous = json.loads(RECEIPT.read_bytes())['outputs'] if RECEIPT.exists() else {}
    outputs = {}

    def write(path: Path, raw: bytes):
        key = path.relative_to(ROOT).as_posix()
        if path.exists() and path.read_bytes() != raw and sha(path.read_bytes()) != previous.get(key):
            raise ValueError('Preserve another Home tip edit: ' + key)
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
        text = replace(text, 'public class ' + name + ' : TipDotConditionNodeBase',
            '[Serializable]\npublic class ' + name + ' : TipDotConditionNodeBase, IHomeTipDotNode')
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
        emit(name, text)

    inputs[TEMPLATE.relative_to(ROOT).as_posix()] = sha(TEMPLATE.read_bytes())
    write(DEST / TEMPLATE.name, TEMPLATE.read_bytes())
    metadata(DEST / TEMPLATE.name)
    definition = DEST / 'Zjcs.Official.HomeTipDots.asmdef'
    write(definition, (json.dumps(dict(
        name='Zjcs.Official.HomeTipDots',
        references=['Zjcs.Official.ProfessionUI', 'Zjcs.Official.HomeUI'],
    ), indent=2) + '\n').encode('utf-8'))
    metadata(definition)
    metadata(DEST, True)
    inputs[Path(__file__).relative_to(ROOT).as_posix()] = sha(Path(__file__).read_bytes())

    RECEIPT.parent.mkdir(parents=True, exist_ok=True)
    RECEIPT.write_text(json.dumps(dict(
        manifest=source.version,
        inputs=inputs,
        outputs=outputs,
        generator_sha256=sha(Path(__file__).read_bytes()),
        adaptations=[
            'Preserves official CheckShowTip formulas, HangUp unlock/item/new-day subscriptions, and numeric TipDotInfo.Data text formatting.',
            'Replaces MeManager/Parsers/MessagePipe with explicit IHomeTipDotServices and IHomeTipDotPlayer ports.',
        ],
        scope='Home fast-hangup TipDot condition nodes consumed by native HomeBed and FurnitureCart TipDotNodeGraph assets.',
    ), ensure_ascii=False, indent=2) + '\n', 'utf-8', newline='\n')
    print('Restored original Home tip conditions:', len(outputs), 'outputs')


if __name__ == '__main__':
    prepare()
