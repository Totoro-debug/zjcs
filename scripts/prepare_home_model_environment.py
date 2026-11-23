"""Migrate original home light schedules, model lights and Spine day clocks."""
import hashlib
import inspect
import json
from pathlib import Path
import re

from official_apk import ROOT
from official_source import OfficialSource
from prepare_current_partner_native import PartnerNativeRecovery, identity
from prepare_home_entity_runtime import replace
from prepare_unity_profession_appearance_resolver import method


DEST = ROOT / 'unity/Assets/Game/Runtime/Official/HomeModelEnvironment'
PAYLOAD = ROOT / 'unity/Assets/Game/Resources/Home/ModelEnvironment/catalog.json'
RECEIPT = ROOT / 'assets/reference/home-model-environment-source-manifest.json'
TEMPLATE = ROOT / 'scripts/templates/home_model_environment/HomeModelEnvironmentServices.cs'


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def prepare():
    source = OfficialSource()
    code = ROOT / '.tools/source-packages/current' / source.version / 'official-code'
    identity_path = code.parent / 'code-manifest.json'
    description = json.loads(identity_path.read_bytes())
    assembly = code / 'managed/decoded/Assembly-CSharp.dll'
    if description['manifest'] != source.version or sha(assembly.read_bytes()) != description['assemblies']['Assembly-CSharp.dll']['decoded_sha256']:
        raise ValueError('Selected original model environment assembly differs')
    inputs = {p.relative_to(ROOT).as_posix(): sha(p.read_bytes()) for p in (identity_path, assembly)}
    previous = json.loads(RECEIPT.read_bytes())['outputs'] if RECEIPT.exists() else {}
    outputs = {}

    def write(path, raw):
        key = path.relative_to(ROOT).as_posix()
        if path.exists() and path.read_bytes() != raw and sha(path.read_bytes()) != previous.get(key):
            raise ValueError('Preserve another home model environment edit: ' + key)
        path.parent.mkdir(parents=True, exist_ok=True)
        if not path.exists() or path.read_bytes() != raw:
            path.write_bytes(raw)
        outputs[key] = sha(raw)

    def metadata(path, folder=False):
        token = hashlib.md5(('zjcs:home-model-environment:' + path.relative_to(ROOT).as_posix()).encode()).hexdigest()
        importer = 'DefaultImporter' if folder else 'AssemblyDefinitionImporter' if path.suffix == '.asmdef' else 'TextScriptImporter' if path.suffix == '.json' else 'MonoImporter'
        raw = 'fileFormatVersion: 2\nguid: ' + token + '\n' + ('folderAsset: yes\n' if folder else '')
        write(Path(str(path) + '.meta'), (raw + importer + ':\n  externalObjects: {}\n').encode())

    def read(relative):
        path = code / (relative + '.cs')
        inputs[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
        text = path.read_text('utf-8-sig').replace('\r\n', '\n')
        return re.sub(r'(?m)^using [^\n]+;\n|^namespace [^\n]+;\n|^\s*//IL_[^\n]*\n|^\s*\[Inject\]\n', '', text)

    def emit(name, text):
        text = text.replace('.Deconstruct(ref ', '.Deconstruct(out ').replace(', ref furnitureBindInfo)', ', out furnitureBindInfo)')
        if any(token in text for token in ('Framework.', 'GLogger', 'MessagePipe', 'DisposableBag', '[Inject]', 'HomeEnvironmentManager', 'HomeEnvironmentUtils')):
            raise ValueError('Unbound original light/clock dependency: ' + name)
        header = '// Selected original home model light/clock implementation; see its source receipt.\n'
        header += 'using System;\nusing System.Collections.Generic;\nusing UnityEngine;\nusing UnityEngine.Rendering.Universal;\nusing Spine;\nusing Spine.Unity;\n'
        header += 'using Zjcs.Official.HomeEnvironmentTime;\nusing Zjcs.Official.HomeEntityRuntime;\nusing Zjcs.Official.ProfessionUI;\n'
        header += 'using IHomeEntityManager = Zjcs.Official.HomeEntityRuntime.IHomeEntityManager;\nnamespace Zjcs.Official.HomeModelEnvironment\n{\nusing Object = UnityEngine.Object;\n'
        target = DEST / (name + '.cs')
        write(target, '\n'.join(line.rstrip() for line in (header + text.strip() + '\n}\n').split('\n')).encode()); metadata(target)

    metadata(DEST, True); metadata(PAYLOAD.parent, True)
    for name in ('HomeLight', 'HomeLightOverride', 'HomeLightBehaviourOverride', 'HomeLightTimeConfig', 'BehaviourActTiming', 'PlayerBehaviourNotifyData'):
        text = read('client-source/' + name)
        if name == 'HomeLightBehaviourOverride':
            text = replace(text, '\t[Dropdown("BehaviourList")]\n', '')
        emit(name, text)

    # Reuse the complete original time calculation, rather than duplicating it.
    original_time = read('client-source/HomeDayTime')
    installed_time = ROOT / 'unity/Assets/Game/Runtime/Official/HomeEnvironmentTime/HomeDayTime.cs'
    installed = installed_time.read_text('utf-8-sig')
    normalize = lambda value: re.sub(r'\s+', '', value)
    for signature in ('public float GetDayProgress(HomeEnvironmentConfig config)',
        'public int GetSecondsSinceStartOfDay(HomeEnvironmentConfig config)',
        'public int GetSecondsSinceStartOfDay(int secondsPerMinute, int minutesPerHour)'):
        if normalize(method(original_time, signature)) != normalize(method(installed, signature)):
            raise ValueError('Verify current original day-progress calculation before reuse')
    inputs[installed_time.relative_to(ROOT).as_posix()] = sha(installed_time.read_bytes())
    config = read('client-source/HomeEnvironmentConfig')
    emit('HomeModelLightConfiguration', '[Serializable]\npublic sealed class HomeModelLightConfiguration\n{\n'
        'public List<HomeLightTimeConfig> HomeLightTimeConfigs;\n' + method(config, 'public IEnumerable<HomeLight> GetHomeLightStatus(') + '\n}')

    text = read('client-source/HomeLightController')
    text = replace(text, 'private DisposableBag mDisposableBag;', 'private HomeEntityRuntimeLifetime mLifetime;')
    text = replace(text, 'private ISubscriber<PlayerBehaviourNotifyData> Subscriber { get; set; }',
        'public IHomeModelEnvironmentServices Services { get; set; }')
    text = replace(text, 'public IHomeEnvironmentManager HomeEnvironmentManager { get; set; }', '')
    text = replace(text, 'mDisposableBag = default;', 'mLifetime = new HomeEntityRuntimeLifetime();')
    text = replace(text, 'Disposable.AddTo<IDisposable>(SubscriberExtensions.Subscribe<PlayerBehaviourNotifyData>(Subscriber, (Action<PlayerBehaviourNotifyData>)OnNotify, Array.Empty<MessageHandlerFilter<PlayerBehaviourNotifyData>>()), ref mDisposableBag);',
        'mLifetime.Add(Services.SubscribePlayerBehaviour(OnNotify));')
    text = replace(text, 'mDisposableBag.Dispose();', 'mLifetime.Dispose();')
    text = text.replace('HomeEnvironmentManager.CurrentPlayerBehaviour', 'Services.CurrentPlayerBehaviour')
    text = text.replace('HomeEnvironmentManager.PlayableDeltaTime', 'Services.PlayableDeltaTime')
    text = text.replace('HomeEnvironmentManager.StageHandleProxy.HomeDayTime', 'Services.HomeDayTime')
    text = text.replace('HomeEnvironmentManager.Config.Value.GetHomeLightStatus', 'Services.LightConfiguration.GetHomeLightStatus')
    text = replace(text, 'HomeEnvironmentUtils.IsHomeTimeLimit()', 'Services.IsHomeTimeLimit')
    # Preserve the exact original function-limit identity at the local input port.
    utils = read('client-source/HomeEnvironmentUtils')
    if normalize(method(utils, 'public static bool IsHomeTimeLimit()')) != normalize('public static bool IsHomeTimeLimit() { return MeManager.MainPlayer.IsFunctionLimit(FunctionType.HomeNaturalTime); }'):
        raise ValueError('Verify the original natural-time function limit before binding it locally')
    emit('HomeLightController', text)

    text = read('client-source/Home.EnvDrive/HomeTimeDriveClock')
    text = replace(text, 'private IHomeEnvironmentManager HomeEnvironmentManager { get; set; }',
        'public IHomeModelEnvironmentServices Services { get; set; }')
    text = text.replace('HomeEnvironmentManager.StageHandleProxy.HomeDayTime', 'Services.HomeDayTime')
    text = text.replace('HomeEnvironmentManager.Config.Value', 'Services.TimeConfiguration')
    emit('HomeTimeDriveClock', text)

    inputs[TEMPLATE.relative_to(ROOT).as_posix()] = sha(TEMPLATE.read_bytes())
    write(DEST / TEMPLATE.name, TEMPLATE.read_bytes()); metadata(DEST / TEMPLATE.name)
    definition = DEST / 'Zjcs.Official.HomeModelEnvironment.asmdef'
    write(definition, (json.dumps(dict(name='Zjcs.Official.HomeModelEnvironment', references=[
        'Zjcs.Official.HomeEnvironmentTime', 'Zjcs.Official.HomeEntityRuntime', 'Zjcs.Official.HomeEntityBindings', 'Zjcs.Official.ProfessionUI',
        'Unity.RenderPipelines.Universal.Runtime', 'spine-unity', 'spine-csharp']), indent=2) + '\n').encode()); metadata(definition)
    recovery = PartnerNativeRecovery(native_cache=ROOT / '.tools/source-packages/current' / source.version / 'home-storage-native',
        export_name='native-export', bundle='defaultpackage_assets_prefabs_home_dynamic_furniture_storagebox.bundle')
    try:
        if recovery.apk.version != source.version:
            raise ValueError('Selected model configuration and code inputs differ')
        obj, = [obj for obj in recovery.apk.objects('defaultpackage_assets_scriptableconfig_modules_home.bundle')
            if obj.type.name == 'MonoBehaviour' and obj.read(check_read=False).m_Name == 'HomeEnvironmentConfig']
        fields = recovery.fields(obj)
        rows = fields['HomeLightTimeConfigs']
        for row in rows:
            for light in row['DefaultLights']:
                light['IsOn'] = bool(light['IsOn'])
            for override in row['Overrides']:
                for light in override['LightOverrides']:
                    light['IsOn'] = bool(light['IsOn'])
        payload = dict(Lights=dict(HomeLightTimeConfigs=rows),
            Clock={key: fields[key] for key in ('SecondsPerMinute', 'MinutesPerHour', 'HoursPerDay')})
        write(PAYLOAD, (json.dumps(payload, ensure_ascii=False, indent=2) + '\n').encode()); metadata(PAYLOAD)
        selection = dict(source=recovery.apk.provenance(obj), object=identity(obj), sha256=sha(obj.get_raw_data()),
            consumed_fields=['HomeLightTimeConfigs', 'SecondsPerMinute', 'MinutesPerHour', 'HoursPerDay'])
    finally:
        recovery.apk.reader.close()
    for path in (Path(__file__), Path(inspect.getsourcefile(method)), Path(inspect.getsourcefile(replace)),
        ROOT / 'scripts/prepare_current_partner_native.py', ROOT / 'scripts/templates/home_entity_runtime/HomeEntityRuntimeServices.cs'):
        inputs[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
    validation = [ROOT / p for p in ('scripts/fixtures/home_model_environment_validation.cs', 'scripts/test_home_model_environment.ps1')]
    receipt = dict(manifest=source.version, inputs=inputs, configuration=selection, outputs=outputs,
        validation_inputs={p.relative_to(ROOT).as_posix(): sha(p.read_bytes()) for p in validation if p.is_file()},
        adaptations=[
            'Original schedule selection, reusable light-array cache and first matching behavior/wildcard override remain intact; duplicate entries and empty schedule intervals are preserved.',
            'Original initialization, furniture light lookup, staged transition queue, delta-time gating, on-count intensity ratio and lifecycle ordering remain intact.',
            'Original natural-time function limit, current stage/behavior and notification publisher use explicit local owner ports.',
            'Original clock forces manual Spine updates and samples animation duration at the complete verified original day-progress calculation.',
            'Only consumed current configuration fields are projected; declared Boolean values use JSON Boolean literals.',
            'Editor-only dropdown metadata and dependency-injection attributes are omitted; decompiler ref/out syntax is repaired.'
        ], scope='Original model light/clock components and full consumed current configuration. Native house integration, actor behavior notifications and full main-scene/Player play verification remain separate.')
    RECEIPT.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    print('Restored original home model environment:', len(outputs), 'outputs')


if __name__ == '__main__':
    prepare()
