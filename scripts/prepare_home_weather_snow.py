"""Port the original shared snow lifecycle and appearance transition."""
import hashlib
import json
from pathlib import Path
import re

from official_apk import ROOT
from official_source import OfficialSource
from prepare_unity_profession_appearance_resolver import method


DEST = ROOT / 'unity/Assets/Game/Runtime/Official/HomeWeather'
RECEIPT = ROOT / 'assets/reference/home-weather-snow-source-manifest.json'


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def prepare():
    selected = OfficialSource()
    code = ROOT / '.tools/source-packages/current' / selected.version / 'official-code'
    description_path = code.parent / 'code-manifest.json'
    description = json.loads(description_path.read_bytes())
    assembly = code / 'managed/decoded/Assembly-CSharp.dll'
    if description['manifest'] != selected.version or sha(assembly.read_bytes()) != description['assemblies']['Assembly-CSharp.dll']['decoded_sha256']:
        raise ValueError('Selected original weather code input differs')
    source_path = code / 'client-source/Home.Skin/HomeWeatherSnow.cs'
    source = source_path.read_text('utf-8-sig')
    # The decompiler emits the async state machine. Verify every operation used
    # to recover its readable async body; retain the original await timing.
    operations = (
        'private float mAppearEffectDuration = 10f;',
        'private ParticleSystem[] mSnowParticleSystems;',
        'private AnimationCurve mParticleAppearCurve;',
        'private static int sSnowTimeId = Shader.PropertyToID("_SnowTime");',
        '_003CelapsedTime_003E5__2 = 0f;',
        'homeWeatherSnow.HomeEnvironmentManager.Config.Value.HomeTimeScaleRuntime * homeWeatherSnow.mAppearEffectDuration',
        '_003CelapsedTime_003E5__2 < _003Cduration_003E5__3 && !cancellationToken.IsCancellationRequested',
        '_003CelapsedTime_003E5__2 += homeWeatherSnow.HomeEnvironmentManager.DeltaTime;',
        'Mathf.Clamp01(_003CelapsedTime_003E5__2 / _003Cduration_003E5__3)',
        'homeWeatherSnow.SetSnowParticleSystemProgress(num2);',
        'Shader.SetGlobalFloat(sSnowTimeId, num2);',
        'UniTask.Yield(cancellationToken, false)',
    )
    for operation in operations:
        if source.count(operation) != 1:
            raise ValueError('Verify original snow async operation: ' + operation)
    def body(signature):
        return re.sub(r'(?m)^\s*//IL_[^\n]*\n', '', method(source, signature))
    header = '''// Selected original shared snow controller; see its source receipt.
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using MainModule = UnityEngine.ParticleSystem.MainModule;
using Zjcs.Official.HomeEnvironmentTime;
using Zjcs.Official.HomeEntityCreation;
using Zjcs.Official.HomeFurniture;
namespace Zjcs.Official.HomeWeather
{
public class HomeWeatherSnow : MonoBehaviour, IHomeEntityChangeCreateNotify
{
    [SerializeField] private float mAppearEffectDuration = 10f;
    [SerializeField] private ParticleSystem[] mSnowParticleSystems;
    [SerializeField] private AnimationCurve mParticleAppearCurve;
    private static int sSnowTimeId = Shader.PropertyToID("_SnowTime");
    private CancellationTokenSource mCancellationDisposable;
    private int[] mSnowParticleSystemInitialMaxParticles;
    public IHomeEnvironmentManager HomeEnvironmentManager { get; set; }
    public IHomeFurnitureViewModes HomeData { get; set; }
'''
    text = header + '\n\n'.join(body(signature) for signature in (
        'private void Awake()', 'private void OnEnable()', 'private void OnDisable()',
        'public void OnChangeCreate()'))
    text = text.replace('new CancellationDisposable()', 'new CancellationTokenSource()')
    text += '''
    private async UniTaskVoid PlayAppearEffectAsync(CancellationToken cancellationToken)
    {
        float elapsedTime = 0f;
        float duration = HomeEnvironmentManager.Config.Value.HomeTimeScaleRuntime * mAppearEffectDuration;
        while (elapsedTime < duration && !cancellationToken.IsCancellationRequested)
        {
            elapsedTime += HomeEnvironmentManager.DeltaTime;
            float progress = Mathf.Clamp01(elapsedTime / duration);
            SetSnowParticleSystemProgress(progress);
            Shader.SetGlobalFloat(sSnowTimeId, progress);
            await UniTask.Yield(cancellationToken, false);
        }
    }
'''
    cancel = body('private void CancelAppearEffect()').replace(
        'mCancellationDisposable.Dispose();', 'mCancellationDisposable.Cancel();\n            mCancellationDisposable.Dispose();')
    text += cancel + '\n\n' + body('private void SetSnowParticleSystemProgress(float progress)') + '\n}\n}\n'
    text = '\n'.join(line.rstrip() for line in text.split('\n'))
    previous = json.loads(RECEIPT.read_bytes())['outputs'] if RECEIPT.exists() else {}
    outputs = {}
    def write(path, raw):
        key = path.relative_to(ROOT).as_posix()
        if path.exists() and path.read_bytes() != raw and sha(path.read_bytes()) != previous.get(key):
            raise ValueError('Preserve another weather edit: ' + key)
        path.parent.mkdir(parents=True, exist_ok=True)
        if not path.exists() or path.read_bytes() != raw:
            path.write_bytes(raw)
        outputs[key] = sha(raw)
    def metadata(path, folder=False):
        token = hashlib.md5(('zjcs:home-weather:' + path.relative_to(ROOT).as_posix()).encode()).hexdigest()
        importer = 'DefaultImporter' if folder else 'AssemblyDefinitionImporter' if path.suffix == '.asmdef' else 'MonoImporter'
        write(Path(str(path) + '.meta'), ('fileFormatVersion: 2\nguid: ' + token + '\n'
            + ('folderAsset: yes\n' if folder else '') + importer + ':\n  externalObjects: {}\n').encode())
    metadata(DEST, True)
    target = DEST / 'HomeWeatherSnow.cs'
    write(target, text.encode()); metadata(target)
    definition = DEST / 'Zjcs.Official.HomeWeather.asmdef'
    write(definition, (json.dumps(dict(name='Zjcs.Official.HomeWeather', references=[
        'Zjcs.Official.HomeEnvironmentTime', 'Zjcs.Official.HomeEntityCreation',
        'Zjcs.Official.HomeFurniture', 'UniTask']), indent=2) + '\n').encode()); metadata(definition)
    inputs = {path.relative_to(ROOT).as_posix(): sha(path.read_bytes()) for path in (
        description_path, assembly, source_path, Path(__file__))}
    validation_inputs = {path.relative_to(ROOT).as_posix(): sha(path.read_bytes()) for path in (
        ROOT / 'scripts/fixtures/home_weather_snow_validation.cs', ROOT / 'scripts/test_home_weather_snow.ps1')}
    receipt = dict(manifest=selected.version, inputs=inputs, outputs=outputs, validation_inputs=validation_inputs, adaptations=[
        'Original particle capacity capture, enabled state, photo-mode gate, appearance curve, time-scale duration, delta-time order and shader progression are retained.',
        'Decompiler async state machine is recovered as its readable UniTaskVoid method, with original UniTask.Yield cancellation and timing.',
        'Global dependency injection uses existing environment/view-mode ports; CancellationDisposable uses a cancelling and disposing CancellationTokenSource; the decompiled nested MainModule type is qualified.'
    ], scope='Shared snow controller. Native weather prefab binding and full scene snow rendering are separate integration work.')
    RECEIPT.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    print('Restored original shared snow controller:', selected.version)


if __name__ == '__main__':
    prepare()
