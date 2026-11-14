"""Recover the selected original home clocks, fast stage handlers and curve."""
import hashlib
import json
from pathlib import Path

from official_apk import ROOT
from prepare_current_partner_native import PartnerNativeRecovery


DESTINATION = ROOT / 'unity/Assets/Game/Runtime/Official/HomeEnvironmentTime'
PAYLOAD = ROOT / 'unity/Assets/Game/Resources/Home/home-environment-time.json'
RECEIPT = ROOT / 'assets/reference/home-fast-environment-source-manifest.json'
SOURCE_NAMES = ('HomeDateTime', 'HomeDayTime', 'IHomeDayStageHandler',
                'NaturalDayStageHandler', 'FastCycleDayStageHandler',
                'HomeDayStageHandleProxy', 'IHomeClock', 'HomeClock', 'HomeClockEntry')


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def metadata(path, importer='MonoImporter', folder=False):
    relative = path.relative_to(ROOT).as_posix()
    guid = hashlib.md5(('zjcs:home-environment-time:' + relative).encode()).hexdigest()
    fields = 'fileFormatVersion: 2\nguid: ' + guid + '\n'
    if folder:
        fields += 'folderAsset: yes\n'
    fields += importer + ':\n  externalObjects: {}\n'
    if importer == 'MonoImporter':
        fields += '  serializedVersion: 2\n  defaultReferences: []\n  executionOrder: 0\n'
    target = Path(str(path) + '.meta')
    if target.exists() and target.read_text('utf-8-sig').replace('\r\n', '\n') != fields:
        raise ValueError('Preserve different metadata: ' + relative)
    target.write_text(fields, 'utf-8', newline='\n')
    return target


def wrap_source(text):
    lines = text.replace('\r\n', '\n').splitlines(keepends=True)
    pos = 0
    while pos < len(lines) and (lines[pos].startswith('using ') or not lines[pos].strip()):
        pos += 1
    return ''.join(lines[:pos]) + 'namespace Zjcs.Official.HomeEnvironmentTime\n{\n' + ''.join(lines[pos:]) + '\n}\n'


def verify_visual_configuration(fields, legacy):
    def moment(value):
        return {name.lower(): value[name] for name in ('Hour', 'Minute', 'Second')}

    expected = dict(
        timeScale=fields['HomeTimeScale'], secondsPerMinute=fields['SecondsPerMinute'],
        minutesPerHour=fields['MinutesPerHour'], hoursPerDay=fields['HoursPerDay'],
        daysPerMonth=fields['DaysPerMonth'], monthsPerYear=fields['MonthsPerYear'],
        refreshDay=moment(fields['RefreshDayTime']),
        stages=[dict(stage=row['Stage'], time=moment(row['Time'])) for row in fields['DayStageList']],
        lightTimes=[dict(begin=moment(row['BeginTime']), lights=[dict(name=light['Name'], on=bool(light['IsOn']))
                     for light in row['DefaultLights']]) for row in fields['HomeLightTimeConfigs']],
        effectLightOn=moment(fields['EffectLightOnTime']), effectLightOff=moment(fields['EffectLightOffTime']),
        moon=fields['MoonConfig'], meteor=dict(begin=moment(fields['MeteorConfig']['BeginTime']),
            end=moment(fields['MeteorConfig']['EndTime']), probability=fields['MeteorConfig']['Probability'],
            interval=fields['MeteorConfig']['Interval']))
    if legacy['config'] != expected:
        raise ValueError('Selected environment values differ from installed visual projection')
    selected_templates = [row['mProxyRes']['mAssetGuid'] for row in fields['MeteorConfig']['Templates']]
    installed_templates = [row['assetGuid'] for row in legacy['meteorRuntime']['templates']]
    if selected_templates != installed_templates:
        raise ValueError('Bind the selected meteor templates before connecting its schedule')
    return expected


def prepare():
    recovery = PartnerNativeRecovery()
    source = ROOT / '.tools/source-packages/current' / recovery.apk.version / 'official-code/client-source'
    obj, = [item for item in recovery.apk.objects('defaultpackage_assets_scriptableconfig_modules_home.bundle')
             if item.type.name == 'MonoBehaviour' and item.read(check_read=False).m_Name == 'HomeEnvironmentConfig']
    fields = recovery.fields(obj)
    legacy_path = ROOT / 'unity/Assets/StreamingAssets/Home/environment.json'
    legacy = json.loads(legacy_path.read_bytes())
    matched = verify_visual_configuration(fields, legacy)
    proof = dict(manifest=recovery.apk.version, configuration_source=recovery.apk.provenance(obj),
                 configuration_object_sha256=sha(obj.get_raw_data()), inputs={}, outputs={},
                 verified_existing_visual_projection=dict(path=legacy_path.relative_to(ROOT).as_posix(),
                     version=legacy['version'], sha256=sha(legacy_path.read_bytes()), matched_values=matched),
                 adaptations=[
                     'Namespace isolates selected original types.',
                     'Stack.TryPeek ref is repaired to the CLR out signature.',
                     'HomeClock value equality is expressed by the original time-field comparison.',
                     'HomeClockEntry primary constructor becomes a C# version-compatible ordinary constructor.'])
    DESTINATION.mkdir(parents=True, exist_ok=True)
    for name in SOURCE_NAMES:
        path = source / (name + '.cs')
        raw = path.read_bytes()
        text = raw.decode('utf-8-sig').replace('\r\n', '\n')
        text = text.replace('TryPeek(ref homeDayStageHandler)', 'TryPeek(out homeDayStageHandler)')
        if name == 'HomeClock':
            text = text.replace('homeDayTime2 == homeDayTime', 'homeDayTime2.CompareTo(homeDayTime) == 0')
            text = text.replace('linkedListNode.Value.HomeDayTime == homeDayTime',
                                'linkedListNode.Value.HomeDayTime.CompareTo(homeDayTime) == 0')
        if name == 'HomeClockEntry':
            text = text.replace('public struct HomeClockEntry(HomeDayTime homeDayTime)', 'public struct HomeClockEntry')
            text = text.replace('public HomeDayTime HomeDayTime = homeDayTime;', 'public HomeDayTime HomeDayTime;')
            text = text.replace('public List<Action<HomeDayTime>> Callbacks = new List<Action<HomeDayTime>>();',
                'public List<Action<HomeDayTime>> Callbacks;\n\n\tpublic HomeClockEntry(HomeDayTime homeDayTime)\n\t{\n'
                '\t\tHomeDayTime = homeDayTime;\n\t\tCallbacks = new List<Action<HomeDayTime>>();\n\t}')
        destination = DESTINATION / path.name
        destination.write_text(wrap_source(text), 'utf-8', newline='\n')
        proof['inputs'][path.relative_to(ROOT).as_posix()] = sha(raw)
        for output in (destination, metadata(destination)):
            proof['outputs'][output.relative_to(ROOT).as_posix()] = sha(output.read_bytes())
    PAYLOAD.write_text(json.dumps(dict(manifest=recovery.apk.version, configuration=fields),
                                 ensure_ascii=False, indent=2) + '\n', 'utf-8', newline='\n')
    for output in (PAYLOAD, metadata(PAYLOAD, 'TextScriptImporter'), metadata(DESTINATION, 'DefaultImporter', True)):
        proof['outputs'][output.relative_to(ROOT).as_posix()] = sha(output.read_bytes())
    for path in (Path(__file__), ROOT / 'scripts/official_apk.py', ROOT / 'scripts/prepare_current_partner_native.py'):
        proof['inputs'][path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
    for name in ('HomeEnvironmentManage', 'HomeEnvFastHangUp', 'HomeMeteor', 'HomeEnvironmentConfig',
                 'HomeMeteorConfig', 'AllDayStageHandler', 'HomeEnvironmentUtils'):
        path = source / (name + '.cs')
        proof['inputs'][path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
    for name in ('GRandom', 'SystemRandom'):
        path = source.parent / 'framework-firstpass-source/Framework' / (name + '.cs')
        proof['inputs'][path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
    path = source.parent / 'common-source/Common/RandomUtils.cs'
    proof['inputs'][path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
    for relative in ('unity/Assets/Official/Home/home-rules-current.json',
                     'assets/reference/home-rules-current-source-manifest.json',
                     'unity/Assets/Game/Runtime/Official/HomeConditions/HomeConditionNotifications.cs',
                     'unity/Assets/Game/Runtime/Offline/HomeHudUI/OfflineHomeHudConditions.cs'):
        path = ROOT / relative
        proof['inputs'][relative] = sha(path.read_bytes())
    offline = ROOT / 'unity/Assets/Game/Runtime/Offline/HomeEnvironmentTime'
    for folder in (DESTINATION, offline):
        for path in sorted(folder.iterdir()):
            if path.suffix not in ('.cs', '.asmdef'):
                continue
            importer = 'AssemblyDefinitionImporter' if path.suffix == '.asmdef' else 'MonoImporter'
            for output in (path, metadata(path, importer)):
                proof['outputs'][output.relative_to(ROOT).as_posix()] = sha(output.read_bytes())
        path = metadata(folder, 'DefaultImporter', True)
        proof['outputs'][path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
    proof['runtime_bindings'] = {path: sha((ROOT / path).read_bytes()) for path in (
        'unity/Assets/Game/Runtime/Presentation/Home/HomeEnvironmentView.cs',
        'unity/Assets/Game/Runtime/Presentation/Home/HomeFeatureRuntime.cs',
        'unity/Assets/Game/Runtime/Presentation/Home/Zjcs.Presentation.Home.asmdef')}
    proof['adaptations'].extend([
        'HomeEnvironmentTimeState extracts original natural/stage/calendar/clock update order; frame delta and initial elapsed time are explicit inputs. No server-time or actor-command replacement is claimed.',
        'Original AllDayStageHandler uses selected function-90 condition identity and original registration callbacks. The local profile bridge forwards committed level, rank and FirstBattle count changes before the environment update.',
        'HomeMeteorSchedule retains original active-window, midnight-wrap, probability and natural-clock rescheduling branches; template play/reuse uses the installed Windows presentation owner.',
        'The visual random port follows the official GRandom fallback System.Random with an int DateTime.Now.Ticks seed; no reward or profile random state is consumed.'])
    proof['validation_inputs'] = {path: sha((ROOT / path).read_bytes()) for path in (
        'scripts/fixtures/home_fast_environment_validation.cs',
        'scripts/fixtures/home_income_formula_validation.cs',
        'scripts/prepare_home_income_formula_validation.py')}
    RECEIPT.write_text(json.dumps(proof, ensure_ascii=False, indent=2) + '\n', 'utf-8', newline='\n')
    print('Recovered selected original home time classes and fast-cycle configuration:', recovery.apk.version)


if __name__ == '__main__':
    prepare()
