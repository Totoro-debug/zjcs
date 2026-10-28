"""Port original portrait queue/cache orchestration with explicit external services."""
import hashlib
import json
from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[1]
CODE = ROOT / '.tools/source-packages/current/871_156598/official-code/client-source'
RUNTIME = ROOT / 'unity/Assets/Game/Runtime/Official/ProfessionUI'


def record(path):
    return dict(path=path.relative_to(ROOT).as_posix(), sha256=hashlib.sha256(path.read_bytes()).hexdigest())


ASYNC = '''
    private async UniTask CaptureAgent(ICharacterPortraitRequestor requestor, CancellationToken cancellationToken)
    {
        try { await CaptureCharacterPortrait(requestor, cancellationToken); }
        catch (OperationCanceledException) { Debug.Log("任务被取消"); }
        catch (Exception exception) { Debug.LogException(exception); }
    }

    private async UniTask CaptureCharacterPortrait(ICharacterPortraitRequestor requestor, CancellationToken cancellationToken)
    {
        long requestId = requestor.GetRequestId();
        PlayerPortraitSettings setting = mEnvironment.Settings;
        requestor.GetDetailRequestData(setting, this, mEnvironment.AppearanceReader, out var data);
        CharacterPortraitCache cache = mCaches[requestId];
        if (cache.IsVaildCacha(in data)) return;
        if (mEnvironment.Persister.TryGetPersistentTexture(data, out var imageData) &&
            TryLoadTextureForBytes(requestor, imageData, out var stored))
        {
            UpdateCachaTexture(cache, stored, data.AppearanceHashCode, needRecycle: true);
            return;
        }
        if (!requestor.TryGetCancellationToken(out var token)) return;
        using (var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, token))
        {
            CancellationToken linkedToken = linkedCts.Token;
            var completion = new UniTaskCompletionSource();
            mWaitingQueue.Enqueue(completion);
            await completion.Task.AttachExternalCancellation(linkedToken);
            linkedToken.ThrowIfCancellationRequested();
        }
        requestor.GetDetailRequestData(setting, this, mEnvironment.AppearanceReader, out data);
        CharacterPortraitTexture2D texture = mPhotographer.CapturePortraitTexture(requestor, out var bytes);
        if (texture == null)
        {
            UpdateCachaTexture(cache, setting.DisableTexture, -1L, needRecycle: false);
            Debug.LogError("请求 " + data + " 拍摄失败");
        }
        else
        {
            UpdateCachaTexture(cache, texture, data.AppearanceHashCode, needRecycle: true);
            if (requestor.NeedPersist()) mEnvironment.Persister.SetPersistentTexture(data, bytes);
        }
    }
'''


def build():
    source = CODE / 'CharacterPortraitManager.cs'
    text = source.read_text(encoding='utf-8')
    # ILSpy emitted UniTask state machines. Their original execution order is
    # retained in ASYNC, including linked-token disposal before the second read.
    for marker in ('UniTaskExtensions.AttachExternalCancellation', 'ThrowIfCancellationRequested',
                   'mWaitingQueue.Enqueue', 'requestor.GetDetailRequestData', 'requestor.NeedPersist()',
                   '((IDisposable)_003ClinkedCts_003E5__4).Dispose();'):
        if marker not in text:
            raise ValueError('Inspect original portrait async state machine: ' + marker)
    body = text[text.index('\tprivate const float kMemoryClearTimeDelay'):]
    a = body.index('\t[Inject]')
    b = body.index('\tprotected override void Initialize()')
    body = body[:a] + '''    private readonly IProfessionPortraitManagerBindings mEnvironment;

    public CharacterPortraitManager(IProfessionPortraitManagerBindings environment)
    { mEnvironment = environment ?? throw new ArgumentNullException(nameof(environment)); }

''' + body[b:]
    body = body.replace('private CharacterPortraitPhotographer mPhotographer;', 'private IProfessionPortraitStudio mPhotographer;')
    body = body.replace('protected override void Initialize()', 'public void Initialize()').replace('protected override void Dispose()', 'public void Dispose()')
    body = body.replace('new CharacterPortraitTexturePool()', 'new CharacterPortraitTexturePool(mEnvironment.LowMemory)')
    body = body.replace('ServiceProviderExtensions.Instantiate<CharacterPortraitPhotographer>(ServiceProvider, ConfigProxy.Value.CharacterPortraitStudioPrefab)', 'mEnvironment.InstantiateStudio()')
    body = body.replace('(Object)(object)mPhotographer', 'mPhotographer.Instance')
    body = body.replace('CancellationTokenDisposable', 'ProfessionPortraitCancellation')
    body = body.replace('Settings.Value', 'mEnvironment.Settings').replace('Persister', 'mEnvironment.Persister')
    a = body.index('\t[AsyncStateMachine(')
    b = body.index('\tprivate bool TryLoadTextureForBytes', a)
    body = body[:a] + ASYNC + '\n' + body[b:]
    body = body.replace('requestor.GetParam(mEnvironment.Settings)', 'mEnvironment.Settings.GetParameters(requestor.GetPortraitType())')
    body = body.replace('void IUpdatable.Update()', 'public void Update()').replace('TryDequeue(ref ', 'TryDequeue(out ')
    body = body.replace('\t\tSpineSkinInfoParser spineSkinInfoParser = Parsers.Instance.SpineSkinInfoParser;\n', '')
    body = body.replace('IReadOnlyList<AppearanceHashValue> appearanceToHash = AppearanceConfig.Value.AppearanceToHash;', 'IReadOnlyList<ProfessionPortraitSkinHash> appearanceToHash = mEnvironment.AppearanceHashes;')
    body = body.replace('AppearanceHashValue item', 'ProfessionPortraitSkinHash item')
    body = body.replace('SpineSkinInfo[] all = spineSkinInfoParser.GetAll();', 'IReadOnlyList<ProfessionPortraitSkinName> all = mEnvironment.SkinNames;')
    body = body.replace('SpineSkinInfo spineSkinInfo', 'ProfessionPortraitSkinName spineSkinInfo')
    body = re.sub(r'^\s*//IL_[^\n]*\n', '\n', body, flags=re.M)
    # Replace complete diagnostic-only spans; never remove a branch or return.
    pattern = r'(?m)^([ \t]*)ILogger (?:logger|val) = [^\n]+;\n.*?^[ \t]*GZLoggerExtensions\.(Error|Trace)\([^\n]+;'
    body = re.sub(pattern, lambda m: m[1] + ('Debug.LogError' if m[2] == 'Error' else 'Debug.Log') + '("Original portrait cache diagnostic.");', body, flags=re.S)
    pattern = r'(?m)^([ \t]*)GLogger(?:Warn|Error)InterpolatedStringHandler message = [^\n]+;\n.*?^[ \t]*Log\.(Warn|Error)\([^\n]+;'
    body = re.sub(pattern, lambda m: m[1] + ('Debug.LogWarning' if m[2] == 'Warn' else 'Debug.LogError') + '("Original portrait skin hash lookup failed.");', body, flags=re.S)
    body = re.sub(r'^\s*(?:ZLoggerErrorInterpolatedStringHandler val2;|bool flag = default;)\n', '\n', body, flags=re.M)
    if any(value in body for value in ('ILogger', 'ZLogger', 'Log.Error(', 'Settings.Value', '[Inject]', 'Parsers.Instance', 'ConfigProxy', 'IUpdatable', '_003C')):
        raise ValueError('Unresolved original portrait manager dependency')
    body = re.sub(r'\n(?:[ \t]*\n){2,}', '\n\n', body)
    header = '''namespace Zjcs.Official.ProfessionUI
{
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Object = UnityEngine.Object;

public class CharacterPortraitManager : ICharacterPortraitManager, ICharacterPortraitSkinHashProvider, IDisposable
{
'''
    target = RUNTIME / 'CharacterPortraitManager.cs'
    target.write_text(header + body + '\n}\n', encoding='utf-8', newline='\n')
    interface_source = CODE / 'ICharacterPortraitSkinHashProvider.cs'
    interface = RUNTIME / interface_source.name
    interface.write_text('namespace Zjcs.Official.ProfessionUI\n{\n' + interface_source.read_text(encoding='utf-8') + '\n}\n', encoding='utf-8', newline='\n')
    bindings = RUNTIME / 'ProfessionPortraitManagerBindings.cs'
    for path in (target, interface, bindings):
        path.with_name(path.name + '.meta').write_text('fileFormatVersion: 2\nguid: ' + hashlib.md5(('zjcs-profession-ui-' + path.stem).encode()).hexdigest() + '\n', encoding='utf-8', newline='\n')
    proof = dict(format=1, manifest='871_156598', source=record(source),
        interface_source=record(interface_source),
        runtime=[dict(record(p), meta=record(p.with_name(p.name + '.meta'))) for p in (target, interface, bindings)],
        original_detail_helper=record(CODE / 'CharacterPortraitUtils.cs'), generator=record(Path(__file__)),
        validation=record(ROOT / 'unity/Assets/Game/Editor/ProfessionUI/ProfessionPortraitManagerValidation.cs'),
        validation_meta=record(ROOT / 'unity/Assets/Game/Editor/ProfessionUI/ProfessionPortraitManagerValidation.cs.meta'),
        source_code_manifest=record(CODE.parent.parent / 'code-manifest.json'),
        dependencies=[record(ROOT / 'assets/reference/profession-ui' / (name + '-source-manifest.json'))
            for name in ('portrait-lifecycle', 'portrait-persistence', 'portrait-settings')],
        operation='Copy original queue/cache manager methods and reconstruct the two emitted UniTask async state machines in their original branch and disposal order. Keep linked cancellation, same-frame queue draining, second appearance read, persistence read regardless of NeedPersist, selective writes, cache replacement/recycling, 10-second cleanup and fourth-pass pool shrinking. Bind background-service lifecycle, original studio factory, appearance details, hash tables, settings and low-memory signal explicitly.',
        validation_scope='Actual isolated Unity Play Mode verifies the original detail/XOR helper against public appearance field fixtures, positive/negative/zero weights and a null skipped skin, cache hash-only validity, queue release and second read, same-frame draining, actual original disk persister reuse, NeedPersist write gating, cancellation/no-token/failure branches and default/loading/disable art ownership. Private fixture timestamps force subscriber-protected expiry and four cleanup passes; no claim is made of waiting forty real seconds. Verifies studio component destruction and cancellation on dispose. The real GPU-rendered red texture is a capture fixture, not character art or a replacement profession UI.',
        detail_helpers=record(ROOT / 'assets/reference/profession-ui/portrait-details-source-manifest.json'),
        limitations=['Production bindings for original studio prefab, appearance objects, skin tables and game lifecycle remain unimplemented. Fixture capture does not establish live character rendering or profession UI completion.'])
    path = ROOT / 'assets/reference/profession-ui/portrait-manager-source-manifest.json'
    path.write_text(json.dumps(proof, ensure_ascii=False, indent=2) + '\n', encoding='utf-8', newline='\n')
    print('Ported original portrait manager queue/cache methods; production studio bindings pending.')


if __name__ == '__main__':
    build()
