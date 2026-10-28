"""Port the original portrait requester/cache lifecycle, without a Spine runtime.

Appearance data is an opaque external payload at this boundary. The original
photographer, persistence and native animation backend are separate dependencies.
"""
import hashlib
import json
from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[1]
CODE = ROOT / '.tools/source-packages/current/871_156598/official-code/client-source'
RUNTIME = ROOT / 'unity/Assets/Game/Runtime/Official/ProfessionUI'


def record(path):
    return dict(path=path.relative_to(ROOT).as_posix(), sha256=hashlib.sha256(path.read_bytes()).hexdigest())


def build():
    ports = []
    for name in ('ICharacterPortraitRequestor', 'ICharacterPortraitManager',
                 'CharacterPortraitRequestor', 'CharacterPortraitCache',
                 'CharacterPortraitTexture2D', 'CharacterPortraitTexturePool',
                 'CharacterPortraitBase', 'ProfessionCharacterPortrait'):
        source = CODE / (name + '.cs')
        text = source.read_text(encoding='utf-8')
        text = re.sub(r'^using (?:Common|MessagePipe|Framework[^;]*|Microsoft.Extensions[^;]*|GFrameCore[^;]*|ZLogger|R3);\n', '', text, flags=re.M)
        text = re.sub(r'^\s*//IL_[^\n]*\n', '\n', text, flags=re.M)
        text = text.replace('\t[Inject]\n', '')
        text = text.replace('PlayerPortraitSettings.EPortraitType', 'ProfessionPortraitKind')
        text = re.sub(r'\bAppearanceData\b', 'object', text)
        text = re.sub(r'\bSex\b', 'int', text)
        text = re.sub(r'\bProfession\b', 'int', text)
        text = text.replace('ProfessionPortraitKind.int', 'ProfessionPortraitKind.Profession')
        text = text.replace('CancellationTokenDisposable', 'ProfessionPortraitCancellation')
        # Remove logger-only spans; preserve every surrounding branch and return.
        pattern = r'(?m)^([ \t]*)(?:(?:GLogger\w+InterpolatedStringHandler|ZLoggerErrorInterpolatedStringHandler) )?\w+ = new (?:GLogger\w+InterpolatedStringHandler|ZLoggerErrorInterpolatedStringHandler)\([^\n]*\);\n.*?^[ \t]*(?:Log\.\w+|GZLoggerExtensions\.Error)\([^\n]*\);'
        def log(match):
            block = match.group(0)
            level = 'LogError' if ('Log.Error(' in block or 'GZLoggerExtensions.Error(' in block) else 'LogWarning' if 'Log.Warn(' in block else 'Log'
            literal = re.search(r'AppendLiteral\("([^"\n]*)"\)', block)
            message = literal[1] if literal else 'Original portrait lifecycle diagnostic.'
            if name == 'CharacterPortraitTexture2D' and 'ex.Message' in block:
                return match[1] + 'Debug.LogException(ex);'
            return match[1] + 'Debug.' + level + '(' + json.dumps(message, ensure_ascii=False) + ');'
        text = re.sub(pattern, log, text, flags=re.S)
        text = re.sub(r'^\s*(?:GLogger\w+InterpolatedStringHandler \w+;|ZLoggerErrorInterpolatedStringHandler \w+;|bool enabled;|bool flag = default;|ILogger \w+ = [^\n]*;)\n', '\n', text, flags=re.M)
        if name == 'CharacterPortraitBase':
            text = re.sub(r'\n\tprotected IScriptableObjectConfig<PlayerPortraitSettings> Settings \{ get; set; \}\n', '\n', text)
            text = re.sub(r'\n\tprotected ILogger<CharacterPortraitManager> Logger \{ get; set; \}\n', '\n', text)
            needle = 'protected ICharacterPortraitManager CharacterPortraitManager { get; set; }'
            assert text.count(needle) == 1
            text = text.replace(needle, needle + '\n\n\tpublic void Bind(ICharacterPortraitManager manager) => CharacterPortraitManager = manager ?? throw new ArgumentNullException(nameof(manager));')
        if name == 'ProfessionCharacterPortrait':
            text = text.replace('CharacterPortraitUtils.GetProfessionCode', 'ProfessionPortraitRequestKey.GetProfessionCode')
            text = text.replace('CharacterPortraitUtils.GetLongHashCode', 'ProfessionPortraitRequestKey.GetLongHashCode')
        if name == 'CharacterPortraitTexturePool':
            text = text.replace('\tprivate IApplicationService mApplicationService;\n', '')
            text = text.replace('private DisposableBag mDisposableBag;', 'private readonly ProfessionResourceOperationBag mDisposableBag = new ProfessionResourceOperationBag();')
            text = text.replace('public CharacterPortraitTexturePool()', 'public CharacterPortraitTexturePool(GAction lowMemory)')
            old = '\t\tmApplicationService = ServiceProviderServiceExtensions.GetService<IApplicationService>(ApplicationScope.Current);\n\t\tDisposable.AddTo<IDisposable>(mApplicationService.OnLowMemory.Subscribe(ForceReleaseAllResourceCache), ref mDisposableBag);'
            assert text.count(old) == 1
            text = text.replace(old, '\t\tmDisposableBag.Add(lowMemory.Subscribe(ForceReleaseAllResourceCache));')
            text = text.replace('TryPop(ref ', 'TryPop(out ')
        if name == 'CharacterPortraitTexture2D':
            text = text.replace('textureFormat - 4', '(int)textureFormat - 4').replace('(int)(int)', '(int)')
        remaining = re.findall(r'\b(?:GLogger\w*|ZLogger\w*|ILogger|IScriptableObjectConfig|ServiceProviderServiceExtensions|DisposableBag)\b', text)
        if remaining:
            raise ValueError('Unadapted portrait dependency in ' + name + ': ' + repr(remaining))
        text = re.sub(r'\n(?:[ \t]*\n){2,}', '\n\n', text)
        unity_using = '' if 'using UnityEngine;\n' in text else 'using UnityEngine;\n'
        text = 'namespace Zjcs.Official.ProfessionUI\n{\nusing Object = UnityEngine.Object;\n' + unity_using + text + '\n}\n'
        target = RUNTIME / (name + '.cs')
        target.write_text(text, encoding='utf-8', newline='\n')
        meta = target.with_name(target.name + '.meta')
        meta.write_text('fileFormatVersion: 2\nguid: ' + hashlib.md5(('zjcs-profession-ui-' + name).encode()).hexdigest() + '\n', encoding='utf-8')
        ports.append(dict(source=record(source), runtime=record(target), meta=record(meta)))
    extras = [RUNTIME / (name + '.cs') for name in ('ProfessionPortraitCancellation', 'ProfessionPortraitRequestKey', 'ProfessionPortraitKind')]
    for path in extras:
        path.with_name(path.name + '.meta').write_text('fileFormatVersion: 2\nguid: ' + hashlib.md5(('zjcs-profession-ui-' + path.stem).encode()).hexdigest() + '\n', encoding='utf-8')
    from read_official_native import NativeMethods
    image = ROOT / '.tools/source-packages/commerce-native/libil2cpp-restored.bin'
    metadata = ROOT / '.tools/source-packages/official-code/global-metadata.dat'
    native = NativeMethods(image, metadata)
    methods = []
    for name in ('MessagePipe.CancellationTokenDisposable..ctor', 'MessagePipe.CancellationTokenDisposable.Dispose'):
        address, index = native.by_name[name]
        end = min(a for a in native.by_address if a > address)
        methods.append(dict(name=name, metadata_index=index, address=hex(address), size=end-address,
            sha256=hashlib.sha256(native.image[address:end]).hexdigest()))
    proof = dict(format=1, manifest='871_156598', ports=ports,
        cancellation_native_inputs=[record(image), record(metadata)], cancellation_native_methods=methods,
        adapters=[dict(record(p), meta=record(p.with_name(p.name + '.meta'))) for p in extras],
        assemblies=[record(ROOT / 'unity/Assets/Game' / folder / 'ProfessionUI' / assembly) for folder, assembly in (
            ('Runtime/Official', 'Zjcs.Official.ProfessionUI.asmdef'),
            ('Editor', 'Zjcs.Editor.ProfessionUI.asmdef'))],
        validation=record(ROOT / 'unity/Assets/Game/Editor/ProfessionUI/ProfessionPortraitLifecycleValidation.cs'),
        validation_workspace=record(ROOT / 'unity/Assets/Game/Editor/ProfessionUI/ProfessionValidationWorkspace.cs'),
        hash_source=record(CODE / 'CharacterPortraitUtils.cs'),
        kind_source=record(CODE / 'PlayerPortraitSettings.cs'), generator=record(Path(__file__)),
        operation='Copy original requester, cache, texture pair/pool, base lifecycle and profession component. Preserve original request/hash/notification/expiry/recycling/cancellation order. Bind manager and low-memory signal explicitly; carry external appearance data opaquely and retain profession/sex integer values. Replace only logging and MessagePipe token ownership with a cancel-then-dispose adapter.',
        validation_scope='Actual isolated Unity Play Mode verifies GPU RGBA readback and byte-exact reload, request identity and opaque appearance payload, request replacement/disable cancellation and notification order, cache subscription/60-second expiry, LIFO reuse, one-per-bucket shrinking and low-memory/disposal behavior. The rendered red texture is a test fixture, not character art or profession UI.',
        limitations=['Photographer, persistence, settings/appearance binding and actual native character capture remain unimplemented. This module does not establish profession UI rendering or completion.'])
    target = ROOT / 'assets/reference/profession-ui/portrait-lifecycle-source-manifest.json'
    target.write_text(json.dumps(proof, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print('Ported eight original portrait lifecycle types; native photographer integration pending.')


if __name__ == '__main__':
    build()
