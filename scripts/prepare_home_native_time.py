"""Port the original shared native home animation clock and registration elements."""
import hashlib
import json
from pathlib import Path
import re

from official_apk import ROOT
from official_source import OfficialSource


DEST = ROOT / 'unity/Assets/Game/Runtime/Official/HomeNativeTime'
RECEIPT = ROOT / 'assets/reference/home-native-time-source-manifest.json'
NAMES = ('HomeEnvDrive', 'IHomeEnvDriveUpdatable', 'IHomeEnvDriveInjectable', 'BaseTimeDriver',
         'HomeTimeDriveSkeletonData', 'HomeTimeDriveAnimationData', 'HomeTimeDriveAnimatorData',
         'HomeTimeDriveSkeletonElement', 'HomeTimeDriveAnimationElement')


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def prepare():
    source = OfficialSource()
    code = ROOT / '.tools/source-packages/current' / source.version / 'official-code'
    code_receipt = code.parent / 'code-manifest.json'
    if json.loads(code_receipt.read_bytes())['manifest'] != source.version:
        raise ValueError('Selected home animation code source differs')
    inputs = {code_receipt.relative_to(ROOT).as_posix(): sha(code_receipt.read_bytes())}
    outputs = {}

    def write(path, raw):
        previous = json.loads(RECEIPT.read_bytes())['outputs'] if RECEIPT.exists() else {}
        relative = path.relative_to(ROOT).as_posix()
        if path.exists() and path.read_bytes() != raw and sha(path.read_bytes()) != previous.get(relative):
            raise ValueError('Preserve another session\'s native time file: ' + relative)
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(raw)
        outputs[relative] = sha(raw)

    def meta(path, folder=False):
        identity = hashlib.md5(('zjcs:home-native-time:' + path.name).encode()).hexdigest()
        body = 'fileFormatVersion: 2\nguid: ' + identity + '\n'
        body += 'folderAsset: yes\nDefaultImporter:\n' if folder else (
            'MonoImporter:\n' if path.suffix == '.cs' else 'AssemblyDefinitionImporter:\n')
        body += '  externalObjects: {}\n'
        write(Path(str(path) + '.meta'), body.encode())

    for name in NAMES:
        path = code / 'client-source' / (name + '.cs')
        raw = path.read_bytes()
        inputs[path.relative_to(ROOT).as_posix()] = sha(raw)
        text = raw.decode('utf-8-sig').replace('\r\n', '\n')
        text = re.sub(r'(?m)^using (?:Framework(?:\.[\w.]+)?|MessagePipe|R3);\s*\n', '', text)
        text = re.sub(r'(?m)^\s*//IL_[^\n]*\n', '', text)
        text = re.sub(r'(?m)^\s*\[Inject\]\s*\n', '', text)
        text = text.replace('IHomeEnvironmentManager', 'IHomeNativeTimeManager')
        text = re.sub(r'I(?:Publisher|Subscriber)<HomeTimeDrive\w+Data>', 'IHomeTimeDriveEvents', text)
        if name == 'BaseTimeDriver':
            text, count = re.subn(r'public IScriptableObjectConfig<HomeEnvironmentConfig> Config \{ get; set; \}', '', text)
            if count != 1:
                raise ValueError('Original unused configuration injection changed')
            text = text.replace('private DisposableBag mDisposable;', 'private List<IDisposable> mDisposable;')
            text = text.replace('mDisposable = default;', 'mDisposable = new List<IDisposable>();')
            for kind in ('Skeleton', 'Animator', 'Animation'):
                old = ('Disposable.AddTo<IDisposable>(SubscriberExtensions.Subscribe<HomeTimeDrive' + kind
                    + 'Data>(HomeTimeDrive' + kind + 'Subscriber, (Action<HomeTimeDrive' + kind
                    + 'Data>)OnHandleHomeTime' + kind + 'DriveData, Array.Empty<MessageHandlerFilter<HomeTimeDrive'
                    + kind + 'Data>>()), ref mDisposable);')
                new = ('mDisposable.Add(HomeTimeDrive' + kind
                    + 'Subscriber.Subscribe(OnHandleHomeTime' + kind + 'DriveData));')
                if text.count(old) != 1:
                    raise ValueError('Original time registration changed: ' + kind)
                text = text.replace(old, new)
            text = text.replace('mDisposable.Dispose();',
                'if (mDisposable != null) { foreach (var subscription in mDisposable) subscription.Dispose(); mDisposable = null; }')
        text = '// Original client home animation clock; local message boundaries only.\n' + (
            'namespace Zjcs.Official.HomeNativeTime\n{\nusing Object = UnityEngine.Object;\n' + text + '\n}\n')
        text = '\n'.join(line.rstrip() for line in text.split('\n'))
        target = DEST / (name + '.cs')
        write(target, text.encode('utf8'))
        meta(target)

    ports = '''using System;
namespace Zjcs.Official.HomeNativeTime
{
    public interface IHomeNativeTimeManager
    {
        float PlayableDeltaTime { get; }
        float PlayableTimeScale { get; }
    }
    public interface IHomeTimeDriveEvents
    {
        IDisposable Subscribe(Action<HomeTimeDriveSkeletonData> handler);
        IDisposable Subscribe(Action<HomeTimeDriveAnimationData> handler);
        IDisposable Subscribe(Action<HomeTimeDriveAnimatorData> handler);
        void Publish(HomeTimeDriveSkeletonData data);
        void Publish(HomeTimeDriveAnimationData data);
        void Publish(HomeTimeDriveAnimatorData data);
    }
}
'''
    target = DEST / 'HomeNativeTimePorts.cs'
    write(target, ports.encode())
    meta(target)
    definition = dict(name='Zjcs.Official.HomeNativeTime', rootNamespace='Zjcs.Official.HomeNativeTime',
        references=['spine-unity', 'spine-csharp'], autoReferenced=True)
    target = DEST / 'Zjcs.Official.HomeNativeTime.asmdef'
    write(target, (json.dumps(definition, indent=2) + '\n').encode())
    meta(target)
    meta(DEST, True)
    bindings = ROOT / 'unity/Assets/Game/Runtime/Offline/HomeNativeTime'
    runtime_bindings = {path.relative_to(ROOT).as_posix(): sha(path.read_bytes())
        for path in ([Path(str(bindings) + '.meta'), *sorted(bindings.rglob('*'))]) if path.is_file()}
    receipt = dict(manifest=source.version, inputs=inputs, generator_sha256=sha(Path(__file__).read_bytes()),
        runtime_bindings=runtime_bindings,
        outputs=outputs, adaptations=[
            'Original static/dynamic skeleton update order, time-scale threshold, animation-state speeds, registration and reset behavior retained.',
            'MessagePipe publishers/subscribers use an explicit synchronous local event boundary; R3 disposal uses local subscription handles.',
            'Unused nonserialized configuration injection omitted; the runtime manager exposes the same playable delta and scale values.'],
        scope='Shared native model animation clock and registration components; no building completion assertion.')
    RECEIPT.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    print('Restored original native home clock:', len(NAMES), 'source types')


if __name__ == '__main__':
    prepare()
