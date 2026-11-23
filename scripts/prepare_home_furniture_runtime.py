"""Migrate the selected FurnitureBase, HomeNotice and storage controller bodies."""
import hashlib
import json
from pathlib import Path
import re

from official_apk import ROOT
from official_source import OfficialSource


DEST = ROOT / 'unity/Assets/Game/Runtime/Official/HomeFurniture'
RECEIPT = ROOT / 'assets/reference/home-furniture-controller-source-manifest.json'
TEMPLATES = Path(__file__).parent / 'templates/home_furniture'


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def replace(text, old, new):
    if text.count(old) != 1:
        raise ValueError('Original furniture boundary changed: ' + old)
    return text.replace(old, new)


def prepare():
    source = OfficialSource()
    code = ROOT / '.tools/source-packages/current' / source.version / 'official-code'
    receipt = code.parent / 'code-manifest.json'
    identity = json.loads(receipt.read_bytes())
    assembly = code / 'managed/decoded/Assembly-CSharp.dll'
    if identity['manifest'] != source.version or sha(assembly.read_bytes()) != identity['assemblies']['Assembly-CSharp.dll']['decoded_sha256']:
        raise ValueError('Selected furniture assembly differs')
    inputs = {p.relative_to(ROOT).as_posix(): sha(p.read_bytes()) for p in (receipt, assembly)}
    previous = json.loads(RECEIPT.read_bytes())['outputs'] if RECEIPT.exists() else {}
    outputs = {}

    def write(path, raw):
        name = path.relative_to(ROOT).as_posix()
        if path.exists() and path.read_bytes() != raw and sha(path.read_bytes()) != previous.get(name):
            raise ValueError('Preserve another furniture change: ' + name)
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(raw)
        outputs[name] = sha(raw)

    def metadata(path, folder=False):
        token = hashlib.md5(('zjcs:home-furniture-runtime:' + path.relative_to(ROOT).as_posix()).encode()).hexdigest()
        kind = 'DefaultImporter' if folder else 'AssemblyDefinitionImporter' if path.suffix == '.asmdef' else 'MonoImporter'
        raw = 'fileFormatVersion: 2\nguid: ' + token + '\n'
        if folder:
            raw += 'folderAsset: yes\n'
        raw += kind + ':\n  externalObjects: {}\n'
        write(Path(str(path) + '.meta'), raw.encode())

    def read(name):
        path = code / 'client-source' / (name + '.cs')
        raw = path.read_bytes()
        inputs[path.relative_to(ROOT).as_posix()] = sha(raw)
        text = raw.decode('utf-8-sig').replace('\r\n', '\n')
        return re.sub(r'(?m)^using [^\n]+;\n|^\s*//IL_[^\n]*\n|^\s*\[Inject\]\n', '', text)

    header = '''// Selected original controller body; local services replace global client owners.
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;
using Zjcs.Official.HomeUI;
using Zjcs.Official.HomeSceneInput;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
namespace Zjcs.Official.HomeFurniture
{
using Object = UnityEngine.Object;
'''

    def emit(name, text):
        text = re.sub(r'\bStateSwitcher<', 'WorldStateSwitcher<', text)
        text = re.sub(r'\bMultiLanguageText\b', 'WorldMultiLanguageText', text)
        text = re.sub(r'\bConditionHandlerRegistration\b', 'IDisposable', text).replace('IConditionNotice', 'IHomeFurnitureConditionNotice')
        text = re.sub(r'\bICondition\b', 'IHomeFurnitureCondition', text)
        text = text.replace('IStoryPlayContext', 'IHomeFurnitureStoryContext')
        text = '\n'.join(line.rstrip() for line in (header + text + '\n}\n').split('\n'))
        if any(token in text for token in ('MeManager', 'Parsers.Instance', 'WindowManager', 'AwardManager', 'Framework.', '[Inject]', 'ServiceProvider', 'PlayerPlayState', 'StoryPlayEntry', 'Log.Debug', 'Log.Error')):
            raise ValueError('Unbound furniture service: ' + name)
        path = DEST / (name + '.cs')
        write(path, text.encode())
        metadata(path)

    text = read('FurnitureBase')
    text = replace(text, '''\tprivate IServiceProvider ServiceProvider { get; set; }
\tprivate IPlayerPlayStateModule PlayerPlayStateModule { get; set; }
\tprivate IStoryPlayEntry StoryPlayEntry { get; set; }
\tprotected IHomeData HomeData { get; set; }''', '''    public IHomeFurnitureServices Services { get; set; }

    // Bind the recovered original fields before activation/Start.
    public void Bind(EFurniture furniture, WorldStateSwitcher<EFurnitureState> states,
        HomeNotice notice, GameObject repairEffect, float showTime, float fullTime,
        IHomeFurnitureServices services)
    {
        mFurniture = furniture;
        mStateSwitcher = states;
        mHomeNotice = notice;
        mRepairEffect = repairEffect;
        mRepairEffectShowTime = showTime;
        mRepairEffectFullTime = fullTime;
        Services = services ?? throw new ArgumentNullException(nameof(services));
    }''')
    text = text.replace('Parsers.Instance.FurnitureInfoParser.Get(mFurniture)', 'Services.GetFurnitureInfo(mFurniture)')
    text = text.replace('MeManager.ConditionHandler.Subscribe(', 'Services.SubscribeCondition(')
    text = text.replace('AwardManager.Instance.RegisterHandler(', 'Services.RegisterAwardHandler(')
    text = text.replace('AwardManager.Instance.RemoveHandler(', 'Services.RemoveAwardHandler(')
    text = text.replace('AwardManager.Instance != null', 'Services != null')
    text = text.replace('MeManager.MainPlayer', 'Services.Player')
    text = text.replace('HomeData.OnPostHomeChange.Subscribe(', 'Services.SubscribePostHomeChange(')
    text = text.replace('StoryPlayEntry.Dispose(', 'Services.DisposeStory(')
    text = text.replace('IHomePlayerData playerData = HomeData.PlayerData;', 'IHomeFurniturePlayerData playerData = Services.PlayerData;')
    text = replace(text, 'ServerInfoManager.Instance.CheckFunctionDisabled(Services.Player, ServerFunctionType.Furniture, new CommonFunctionsDisabledKey((long)mFurniture))', 'Services.CheckFunctionDisabled(mFurniture)')
    text = text.replace('BroadcastManager.ShowErrText(53851, null, false)', 'Services.ShowError(53851)')
    text = replace(text, 'private bool HandleReward(PlayerAwardMsg msg)', 'private bool HandleReward(HomeFurnitureAward msg)')
    text = text.replace('msg.SourceData.SourceOperate != SourceOperate.RepairFurniture', '!msg.IsRepairFurniture')
    begin = text.index('\t\tGLoggerDebugInterpolatedStringHandler message =')
    end = text.index('\n\t\treturn true;', begin)
    text = text[:begin] + '\t\tDebug.Log(GetType().Name + " HandleAward RepairFurniture", this);' + text[end:]
    text = text.replace('Services.Player.GetStoryStatus(storyName) == StoryStatus.None', '!Services.Player.HasStartedStory(storyName)')
    text = text.replace('PlayerPlayStateClientUtils.DoStoryAction(PlayerPlayStateModule, () =>', 'Services.DoStoryAction(() =>')
    text = text.replace('new StoryPlayCommonContext("FurnitureBase", Services.Player, null, null)', 'Services.CreateStoryContext("FurnitureBase")')
    text = text.replace('StoryPlayEntry.Play(', 'Services.PlayStory(')
    text = replace(text, '''\t\t\tWindowManager.Instance.Popup((FurnitureRepairDialog d) =>
\t\t\t{
\t\t\t\td.Init(Furniture, isBuild: false);
\t\t\t});''', '\t\t\tServices.PopupRepair(Furniture, isBuild: false);')
    emit('FurnitureBase', text)

    text = read('HomeNotice')
    text = replace(text, '''\tprivate IServiceProvider ServiceProvider { get; set; }
\tprivate IHomeData HomeData { get; set; }''', '''    public IHomeFurnitureServices Services { get; set; }

    public void Bind(WorldMultiLanguageText text, WorldStateSwitcher<bool> states,
        string customRepairNotice, IHomeFurnitureServices services)
    {
        mRepairNoticeTxt = text;
        mShowStateSwitcher = states;
        mCustomRepairNotice = customRepairNotice;
        Services = services ?? throw new ArgumentNullException(nameof(services));
    }''')
    text = text.replace('MeManager.MainPlayer', 'Services.Player')
    text = text.replace('MeManager.ConditionHandler.Subscribe(', 'Services.SubscribeCondition(')
    text = text.replace('HomeData.InMyHome', 'Services.InMyHome')
    text = text.replace('.IsMet(Services.Player)', '.IsMet()')
    text = text.replace('ConditionNoticeFactory.Create(ServiceProvider, mUnlockCondition)', 'Services.CreateConditionNotice(mUnlockCondition)')
    text = text.replace('BroadcastManager.Instance.SystemMsg(', 'Services.SystemMessage(')
    emit('HomeNotice', text)
    emit('StoreBoxFurniture', read('StoreBoxFurniture').replace('WindowManager.Instance.Popup<PlayerBagPanel>()', 'Services.PopupPlayerBag()'))

    port = TEMPLATES / 'HomeFurnitureServices.cs'
    inputs[port.relative_to(ROOT).as_posix()] = sha(port.read_bytes())
    write(DEST / port.name, port.read_bytes())
    metadata(DEST / port.name)
    definition = DEST / 'Zjcs.Official.HomeFurniture.asmdef'
    write(definition, (json.dumps(dict(name='Zjcs.Official.HomeFurniture', references=[
        'Zjcs.Official.HomeUI', 'Zjcs.Official.HomeSceneInput', 'Zjcs.Official.ProfessionUI',
        'Zjcs.Official.WorldUI']), indent=2) + '\n').encode())
    metadata(definition)
    metadata(DEST, True)
    RECEIPT.parent.mkdir(parents=True, exist_ok=True)
    validation = ROOT / 'scripts/fixtures/home_furniture_controller_validation.cs'
    shared = [ROOT / path for path in (
        'unity/Assets/Game/Runtime/Official/HomeSceneInput/ClickableHomeObj.cs',
        'unity/Assets/Game/Runtime/Official/ProfessionUI/UIBase.cs',
        'unity/Assets/Game/Runtime/Official/WorldUI/WorldStateSwitcher.cs',
        'unity/Assets/Game/Runtime/Official/WorldUI/WorldMultiLanguageText.cs',
        'unity/Assets/Game/Runtime/Official/HomeUI/EFurniture.cs')]
    RECEIPT.write_text(json.dumps(dict(manifest=source.version, inputs=inputs, outputs=outputs,
        validation_inputs={validation.relative_to(ROOT).as_posix(): sha(validation.read_bytes())},
        shared_dependencies={path.relative_to(ROOT).as_posix(): sha(path.read_bytes()) for path in shared},
        generator_sha256=sha(Path(__file__).read_bytes()), adaptations=[
            'Original four-state priority, click branches, condition subscriptions and teardown are retained.',
            'Original notice transitions, time-offset callbacks, notice text keys and arguments are retained.',
            'Original repair effect delays and story-context reuse/disposal are retained.',
            'Client globals and injected owners are explicit local ports; no fallback rules or story content are introduced.',
            'Original selected native click dispatcher, state switcher, UI lifecycle and localization component are reused.',
            'Decompiler casts are retained; logging calls use Unity logging instead of the client logging framework.'
        ], scope='Shared original furniture controllers and storage click action. Native prefab dependencies and local host binding are validated separately.'), ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    print('Restored original furniture runtime:', len(outputs), 'outputs')


if __name__ == '__main__':
    prepare()
