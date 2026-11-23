"""Migrate original home entity registration, slot ownership and timeline runtime."""
import hashlib
import inspect
import json
from pathlib import Path
import re

from official_apk import ROOT
from official_source import OfficialSource
from prepare_home_entity_bindings import block_end, remove_logging
from prepare_unity_profession_appearance_resolver import method


DEST = ROOT / 'unity/Assets/Game/Runtime/Official/HomeEntityRuntime'
RECEIPT = ROOT / 'assets/reference/home-entity-runtime-source-manifest.json'
TEMPLATE = ROOT / 'scripts/templates/home_entity_runtime/HomeEntityRuntimeServices.cs'


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def replace(text, before, after):
    if text.count(before) != 1:
        raise ValueError('Original home entity runtime boundary differs: ' + before)
    return text.replace(before, after)


def prepare():
    source = OfficialSource()
    code = ROOT / '.tools/source-packages/current' / source.version / 'official-code'
    code_receipt = code.parent / 'code-manifest.json'
    identity = json.loads(code_receipt.read_bytes())
    if identity['manifest'] != source.version:
        raise ValueError('Selected home entity runtime source differs')
    inputs = {code_receipt.relative_to(ROOT).as_posix(): sha(code_receipt.read_bytes())}
    for name in ('Assembly-CSharp.dll', 'Console.dll', 'Framework.FirstPass.Runtime.dll'):
        path = code / 'managed/decoded' / name
        if sha(path.read_bytes()) != identity['assemblies'][name]['decoded_sha256']:
            raise ValueError('Selected home entity runtime assembly differs: ' + name)
        inputs[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
    previous = json.loads(RECEIPT.read_bytes())['outputs'] if RECEIPT.exists() else {}
    outputs = {}

    def write(path, raw):
        key = path.relative_to(ROOT).as_posix()
        if path.exists() and path.read_bytes() != raw and sha(path.read_bytes()) != previous.get(key):
            raise ValueError('Preserve another home entity runtime edit: ' + key)
        path.parent.mkdir(parents=True, exist_ok=True)
        if not path.exists() or path.read_bytes() != raw:
            path.write_bytes(raw)
        outputs[key] = sha(raw)

    def metadata(path, folder=False):
        meta = Path(str(path) + '.meta')
        if folder and path == DEST and meta.exists():
            # Preserve the identity assigned while this new module was emitted.
            # Unity may expand its initial two-line metadata without changing it.
            existing = meta.read_bytes()
            normalized = existing.replace(b'\r\n', b'\n')
            match = re.fullmatch(rb'fileFormatVersion: 2\nguid: ([0-9a-f]{32})\n'
                rb'(?:folderAsset: yes\nDefaultImporter:\n  externalObjects: \{\}\n'
                rb'(?:  userData: *\n  assetBundleName: *\n  assetBundleVariant: *\n)?)?', normalized)
            if not match:
                raise ValueError('Resolve the existing new module folder metadata')
            old = previous.get(meta.relative_to(ROOT).as_posix())
            minimal = b'fileFormatVersion: 2\nguid: ' + match[1] + b'\n'
            if old and old not in (sha(existing), sha(minimal), sha(minimal.rstrip(b'\n'))):
                raise ValueError('Preserve a changed module folder identity or custom metadata')
            canonical = b'\n'.join(line.rstrip() for line in normalized.splitlines()) + b'\n'
            write(meta, canonical)
            return
        token = hashlib.md5(('zjcs:home-entity-runtime:' + path.relative_to(ROOT).as_posix()).encode()).hexdigest()
        kind = 'DefaultImporter' if folder else 'AssemblyDefinitionImporter' if path.suffix == '.asmdef' else 'MonoImporter'
        raw = 'fileFormatVersion: 2\nguid: ' + token + '\n' + ('folderAsset: yes\n' if folder else '')
        write(Path(str(path) + '.meta'), (raw + kind + ':\n  externalObjects: {}\n').encode())

    def read(relative):
        path = code / (relative + '.cs')
        inputs[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
        text = path.read_text('utf-8-sig').replace('\r\n', '\n')
        return re.sub(r'(?m)^using [^\n]+;\n|^namespace [^\n]+;\n|^\s*//IL_[^\n]*\n|^\s*\[Inject\]\n', '', text)

    def emit(name, body):
        body = remove_logging(body, name)
        body = body.replace('.Deconstruct(ref ', '.Deconstruct(out ').replace(', ref ', ', out ')
        body = re.sub(r'TryGetComponent<([^>]+)>\(ref ', r'TryGetComponent<\1>(out ', body)
        if re.search(r'\bDisposableBag\b', body) or any(token in body for token in ('Parsers.', 'MeManager', 'Framework.', 'MessagePipe', 'R3.', '[Inject]', 'GLogger', 'Log.')):
            raise ValueError('Unbound original home entity runtime dependency: ' + name)
        header = '// Selected original home entity registration/timeline implementation; see its source receipt.\nusing System;\nusing System.Collections.Generic;\nusing System.Linq;\nusing System.Threading;\nusing UnityEngine;\nusing UnityEngine.Animations;\nusing UnityEngine.Playables;\nusing UnityEngine.Timeline;\nusing Cysharp.Threading.Tasks;\nusing Zjcs.Official.HomeEntityBindings;\nusing Zjcs.Official.HomeEntityCreation;\nusing Zjcs.Official.HomeUI;\nusing Zjcs.Official.ProfessionUI;\nnamespace Zjcs.Official.HomeEntityRuntime\n{\nusing Object = UnityEngine.Object;\n'
        text = '\n'.join(line.rstrip() for line in (header + body.strip() + '\n}\n').split('\n'))
        path = DEST / (name + '.cs'); write(path, text.encode()); metadata(path)

    # The reused event retains the linked observer/version semantics, including
    # registration during invocation and per-observer SafeInvoke exception handling.
    original = read('framework-firstpass-source/Framework/GAction')
    start = original.index('public sealed class GAction<T>')
    end = block_end(original, original.index('{', start)) + 1
    original = original[start:end]
    begin = original.index('GLoggerErrorInterpolatedStringHandler message =')
    finish = original.index('\n', original.index('Log.Error(exception, ref message', begin))
    original = original[:begin] + 'UnityEngine.Debug.LogException(exception);' + original[finish:]
    action_path = ROOT / 'unity/Assets/Game/Runtime/Official/StoryPlayables/GAction.cs'
    installed = action_path.read_text('utf-8-sig')
    start = installed.index('public sealed class GAction<T>'); end = block_end(installed, installed.index('{', start)) + 1
    normalize = lambda text: re.sub(r'\s+', '', text)
    if normalize(original) != normalize(installed[start:end]):
        raise ValueError('Verify the complete current generic entity event before reusing it')
    inputs[action_path.relative_to(ROOT).as_posix()] = sha(action_path.read_bytes())

    for name in ('HomeSlotEntityData', 'FurnitureBindInfo', 'ThemeBindInfo', 'IFurnitureProxy', 'HomeSkinPreviewData'):
        emit(name, read('client-source/' + name))
    enum = read('common-source/Common/ItemType')
    constants = []
    for name in ('FurnitureSkin', 'HomeThemeSkin', 'HomeEnvironmentSkin'):
        value, = re.findall(r'\b' + name + r'\s*=\s*(\d+)', enum)
        constants.append(name + ' = ' + value)
    emit('HomeEntitySkinItemType', 'public enum HomeEntitySkinItemType\n{\n' + ',\n'.join(constants) + '\n}')

    text = read('client-source/IHomeEntityManager')
    text = text.replace('IHomeEntityManager : IDisposable', 'IHomeEntityManager : Zjcs.Official.HomeEntityBindings.IHomeEntityManager, IDisposable')
    text = text.replace('IHomeEntityDataAgent EntityDataAgent', 'new IHomeEntityDataAgent EntityDataAgent')
    emit('IHomeEntityManager', text)
    emit('HomeEntityManager', read('client-source/HomeEntityManager'))

    text = read('client-source/HomeEntityManagerMono')
    text = replace(text, 'private DisposableBag mDisposableBag;', 'private HomeEntityRuntimeLifetime mDisposableBag;')
    begin = text.index('\tpublic IResourceLoader ResourceLoader { get; set; }')
    end = text.index('\n\tpublic IHomePlayerData PlayerData', begin)
    props = '''    public IHomeEntityRuntimeServices Services { get; set; }
    public IHomeEntityResourceLoader ResourceLoader => Services.ResourceLoader;
    public IHomeFurnitureChangePublisher FurnitureChangePublisher => Services.FurnitureChangePublisher;
    public IHomeEntityManager EntityManager => Services.EntityManager;
    public IHomeEntityPrefabTables Tables => Services.Tables;
    public HomeEntityPrefabPaths PrefabPaths => Services.PrefabPaths;
    public GameObject Instantiate(GameObject prefab, Transform parent, bool worldPositionStays)
        => Services.Instantiate(prefab, parent, worldPositionStays);
'''
    text = text[:begin] + props + text[end:]
    text = text.replace('IHomePlayerData PlayerData => HomeData.PlayerData', 'IHomeEntitySkinData PlayerData => Services.PlayerData')
    text = text.replace('GAction<HomeEntityData>', 'Zjcs.Official.StoryPlayables.GAction<HomeEntityData>')
    text = text.replace('mDisposableBag = default;', 'mDisposableBag = new HomeEntityRuntimeLifetime();')
    text = replace(text, 'Disposable.AddTo<IDisposable>(SubscriberExtensions.Subscribe<HomeSkinPreviewData>(HomeSkinPreviewSubscriber, (Action<HomeSkinPreviewData>)OnHomeSkinPreview, Array.Empty<MessageHandlerFilter<HomeSkinPreviewData>>()), ref mDisposableBag);',
        'mDisposableBag.Add(Services.SubscribeSkinPreview(OnHomeSkinPreview));')
    text = text.replace('Disposable.AddTo<IDisposable>(HomeData.OnHomeChange.Subscribe(OnHomeChange), ref mDisposableBag);', 'mDisposableBag.Add(Services.SubscribeHomeChange(OnHomeChange));')
    for name, method_name in [('Furniture', 'OnFurnitureSkinChanged'), ('Theme', 'OnHomeThemeSkinChanged'), ('Environment', 'OnPlayerEnvironmentSkinChanged')]:
        text = text.replace('Disposable.AddTo<IDisposable>(MeManager.MainPlayer.OnHome' + name + 'SkinChanged.Subscribe(' + method_name + '), ref mDisposableBag);',
            'mDisposableBag.Add(Services.Subscribe' + name + 'SkinChanged(' + method_name + '));')
    text = text.replace('HomeBehaviourServiceCenter.RuntimeBindingCacheService.', 'Services.RuntimeBindingCacheService.')
    text = text.replace('TipDotSystem.SetShowTipDot(HomeData.InMyHome);', 'Services.SetShowTipDot(Services.InMyHome);')
    text = replace(text, '''SetHomeSceneUIPublisher.Publish(new SetHomeSceneUIActiveData
\t\t{
\t\t\tIsActive = HomeData.InMyHome
\t\t});''', 'Services.SetHomeSceneUIActive(Services.InMyHome);')
    text = text.replace('ItemInfo itemInfo = Parsers.Instance.ItemInfoParser.Get(', 'HomeEntityItemInfo itemInfo = Services.GetItemInfo(')
    text = text.replace('ItemType.', 'HomeEntitySkinItemType.')
    text = text.replace('Parsers.Instance.FurnitureSkinInfoParser.Get(', 'Services.GetFurnitureSkin(')
    text = text.replace('Parsers.Instance.HomeThemeSkinInfoParser.Get(', 'Services.GetThemeSkin(')
    text = text.replace('HomeEnvironmentSkinInfo homeEnvironmentSkinInfo = Parsers.Instance.HomeEnvironmentSkinInfoParser.Get(',
        'HomeEntityEnvironmentSkinInfo homeEnvironmentSkinInfo = Services.GetEnvironmentSkin(')
    text = text.replace('public TimelinePlayable HomeTimelinePlayable;', 'public HomeTimelinePlayable HomeTimelinePlayable;')
    emit('HomeEntityManagerMono', text)

    emit('HomePlayableBinding', read('client-source/HomePlayable/PlayableBinding').replace('class PlayableBinding', 'class HomePlayableBinding'))
    emit('PlayableAnimatorBinding', read('client-source/HomePlayable/PlayableAnimatorBinding'))
    emit('IHomeTimelinePlayable', read('client-source/HomePlayable/ITimelinePlayable').replace('interface ITimelinePlayable', 'interface IHomeTimelinePlayable'))
    text = read('client-source/HomePlayable/TimelinePlayable')
    begin = text.index('\t[StructLayout(')
    end = text.index('\tpublic TimelineAsset DefaultTimelineAsset;')
    text = text[:begin] + text[end:]
    text = text.replace('class TimelinePlayable : MonoBehaviour, ITimelinePlayable', 'class HomeTimelinePlayable : MonoBehaviour, IHomeTimelinePlayable')
    text = text.replace('public PlayableBinding PlayableBinding;', 'public HomePlayableBinding PlayableBinding;')
    text = re.sub(r'AnimationClipPlayable\.op_Implicit\((\w+)\)', r'(Playable)\1', text)
    text = re.sub(r'\s*\[AsyncStateMachine\([^\n]+\)\]\n', '\n', text)
    wrapper = method(text, 'private UniTaskVoid ExecuteTransition(')
    restored = '''private async UniTaskVoid ExecuteTransition(float duration, CancellationToken token, Action onComplete)
    {
        mTransitionTime = 0f;
        while (mTransitionTime < duration)
        {
            mTransitionTime += Time.deltaTime;
            float weight = Mathf.Clamp01(mTransitionTime / duration);
            SetMixPlayableInputWeight(mCurrentInputIndex, 1f - weight, mNextInputIndex, weight);
            await UniTask.Yield(token, false);
        }
        foreach (KeyValuePair<string, AnimationMixerPlayable> pair in mTimelineMixPlayableDict)
        {
            AnimationMixerPlayable mixer = pair.Value;
            Playable input = PlayableExtensions.GetInput<AnimationMixerPlayable>(mixer, mCurrentInputIndex);
            mClipPlayables.Remove(input);
            mPlayableGraph.DestroyPlayable<Playable>(input);
            mPlayableGraph.DestroyPlayable<AnimationMixerPlayable>(mixer);
        }
        SwapInputIndex();
        onComplete?.Invoke();
        InTransition = false;
    }'''
    text = replace(text, wrapper, restored)
    emit('HomeTimelinePlayable', text)

    inputs[TEMPLATE.relative_to(ROOT).as_posix()] = sha(TEMPLATE.read_bytes())
    write(DEST / TEMPLATE.name, TEMPLATE.read_bytes()); metadata(DEST / TEMPLATE.name)
    definition = DEST / 'Zjcs.Official.HomeEntityRuntime.asmdef'
    write(definition, (json.dumps(dict(name='Zjcs.Official.HomeEntityRuntime', references=[
        'Zjcs.Official.HomeEntityBindings', 'Zjcs.Official.HomeEntityCreation', 'Zjcs.Official.HomeUI',
        'Zjcs.Official.ProfessionUI', 'Zjcs.Official.StoryPlayables', 'Unity.Timeline', 'UniTask']), indent=2) + '\n').encode())
    metadata(definition); metadata(DEST, True)
    for helper in (block_end, remove_logging, method):
        path = Path(inspect.getsourcefile(helper)); inputs[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
    path = Path(__file__); inputs[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
    validations = [ROOT / path for path in ('scripts/fixtures/home_entity_runtime_validation.cs',
        'scripts/fixtures/home_timeline_runtime_validation.cs', 'scripts/fixtures/home_entity_runtime_probes.cs', 'scripts/test_home_entity_runtime.ps1',
        'scripts/prepare_home_entity_runtime_inputs.py')]
    proof = dict(manifest=source.version, inputs=inputs, outputs=outputs,
        validation_inputs={p.relative_to(ROOT).as_posix(): sha(p.read_bytes()) for p in validations if p.is_file()},
        adaptations=[
            'Original entity registration order, type-grouped data sets, dynamic slot replacement/restoration and recursive creation/release are retained.',
            'Original home/preview/skin/weather subscriptions and dirty-cache/tip/UI notification order are retained through explicit local owner ports.',
            'Original generic GAction observer implementation is reused only after complete normalized-body identity verification.',
            'Original timeline graph, track binding, normalized clip times, registration/removal and transition state machine ordering are retained.',
            'Timeline transition async syntax is restored from its verified generated state machine, including its original mixer destruction behavior.',
            'Decompiler ref/out and playable cast syntax are lowered; diagnostic I/O uses Unity logging.'
        ], scope='Original entity/slot registration and timeline components. Native scene/house hosts, player-owned data ports, full actor graphs and building play verification remain separate.')
    RECEIPT.write_text(json.dumps(proof, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    print('Restored original home entity runtime:', len(outputs), 'outputs')


if __name__ == '__main__':
    prepare()
