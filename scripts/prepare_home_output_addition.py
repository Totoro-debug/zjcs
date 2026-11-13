"""Recover selected original output-addition queries and their APK configuration."""
import hashlib
import json
from pathlib import Path
import re

from official_apk import Apk, ROOT
from official_tables import Constructors
from prepare_home_auto_increase_runtime import method

CODE = ROOT / '.tools/source-packages/current/884_156692/official-code'
DEST = ROOT / 'unity/Assets/Game/Runtime/Official/HomeOutputAddition'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def build():
    inputs, outputs, methods = {}, {}, {}

    def read(name):
        path = CODE / 'common-source/Common' / (name + '.cs')
        inputs[path.relative_to(ROOT).as_posix()] = sha(path)
        return path.read_text('utf-8-sig').replace('\r\n', '\n')

    def write(path, text, folder=False):
        path.parent.mkdir(parents=True, exist_ok=True)
        if not folder:
            path.write_text(text, 'utf8', newline='\n')
            outputs[path.relative_to(ROOT).as_posix()] = sha(path)
        meta = Path(str(path) + '.meta')
        if not meta.exists():
            guid = hashlib.md5(('zjcs:home-output-addition:' + path.relative_to(ROOT).as_posix()).encode()).hexdigest()
            kind = 'folderAsset: yes\nDefaultImporter:\n' if folder else (
                'MonoImporter:\n  serializedVersion: 2\n' if path.suffix == '.cs' else
                'AssemblyDefinitionImporter:\n' if path.suffix == '.asmdef' else 'TextScriptImporter:\n')
            meta.write_text('fileFormatVersion: 2\nguid: ' + guid + '\n' + kind + '  externalObjects: {}\n', 'utf8', newline='\n')
        outputs[meta.relative_to(ROOT).as_posix()] = sha(meta)

    def emit(name, text):
        write(DEST / (name + '.cs'), '// Selected Console.dll; explicit data, clock, config and disabled-function ports.\n'
              'using System;\nusing System.Collections.Generic;\nnamespace Zjcs.Official.HomeOutputAddition\n{\n' + text + '\n}\n')

    proof_path = ROOT / 'assets/reference/official-client-code/source-manifest.json'
    proof = next(row for row in json.loads(proof_path.read_bytes())['projects']
                 if row['assembly'] == 'Console.dll' and row['folder'] == 'common-source')
    assembly = CODE / 'managed/decoded/Console.dll'
    assert sha(assembly) == proof['source']['decoded_sha256']
    for path in (assembly, proof_path, *(ROOT / 'scripts' / name for name in (
        'official_apk.py', 'official_tables.py', 'official_source.py', 'official_bundle_reader.py',
        'prepare_home_auto_increase_runtime.py'))):
        inputs[path.relative_to(ROOT).as_posix()] = sha(path)
    for name in ('OutputActionType', 'OutputGroupType', 'OutputAdditionPropType'):
        emit(name, read(name).split('namespace Common;')[1].strip())
    for name in ('PlayerOutputAdditionData', 'PlayerOutputAdditionPropTypeData',
                 'PlayerOutputAdditionDataWrap', 'PlayerOutputAdditionPropTypeDataWrap', 'OutputAdditionInfo',
                 'CommonFunctionsDisabledKey'):
        read(name)
    emit('OutputAdditionModels', '''public sealed class OutputAdditionInfo
{
    public int ClassId, OutputAdditionPropVal;
    public OutputActionType OutputActionType;
    public OutputGroupType OutputGroupType;
    public OutputAdditionPropType OutputAdditionPropType;
    public string ShowGroupKey, ShowKey;
}
public sealed class PlayerOutputAdditionPropTypeDataWrap
{
    public int ClassId;
    public DateTime StartTime, EndTime;
}
public sealed class PlayerOutputAdditionDataWrap
{
    public long PlayerId, Id;
    public OutputActionType OutputActionType;
    public DateTime StartTime, EndTime;
    public Dictionary<OutputAdditionPropType, PlayerOutputAdditionPropTypeDataWrap> OutputAdditionPropDict
        = new Dictionary<OutputAdditionPropType, PlayerOutputAdditionPropTypeDataWrap>();
}
public sealed class OutputAdditionInfoParser
{
    private readonly IReadOnlyDictionary<int, OutputAdditionInfo> rows;
    public OutputAdditionInfoParser(IReadOnlyDictionary<int, OutputAdditionInfo> rows)
    { this.rows = rows ?? throw new ArgumentNullException(nameof(rows)); }
    public OutputAdditionInfo Get(int id) => rows.TryGetValue(id, out var value) ? value : null;
}
public sealed class HomeOutputAdditionPlayer
{
    public readonly Dictionary<long, PlayerOutputAdditionDataWrap> mAllOutputAdditionDict;
    public readonly OutputAdditionInfoParser Configuration;
    public readonly Func<DateTime> GetDateTime;
    public readonly Func<int?, bool> CheckFunctionDisabled;
    public HomeOutputAdditionPlayer(Dictionary<long, PlayerOutputAdditionDataWrap> records,
        OutputAdditionInfoParser configuration, Func<DateTime> now, Func<int?, bool> disabled)
    {
        mAllOutputAdditionDict = records ?? throw new ArgumentNullException(nameof(records));
        Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        GetDateTime = now ?? throw new ArgumentNullException(nameof(now));
        CheckFunctionDisabled = disabled ?? throw new ArgumentNullException(nameof(disabled));
    }
}''')
    original = read('OutputAdditionUtils')
    body = original.split('namespace Common;')[1].strip()
    signatures = re.findall(r'\t(?:public|private) static [^\n]+\)', body)
    for signature in signatures:
        raw = method(original, signature.strip())
        methods[signature.strip()] = hashlib.sha256(raw.encode()).hexdigest()
    assert len(signatures) == 7, signatures
    body = re.sub(r'\bPlayer\b', 'HomeOutputAdditionPlayer', body)
    body = body.replace('TimeManager.Instance.GetDateTime()', 'player.GetDateTime()')
    body = body.replace('Parsers.Instance.OutputAdditionInfoParser', 'player.Configuration')
    body = body.replace('ServerInfoManager.Instance.CheckFunctionDisabled(player, ServerFunctionType.OutputAddition, key)',
                        'player.CheckFunctionDisabled(key)')
    body = body.replace('ServerInfoManager.Instance.CheckFunctionDisabled(player, ServerFunctionType.OutputAddition)',
                        'player.CheckFunctionDisabled(null)')
    body = body.replace('CommonFunctionsDisabledKey key = new CommonFunctionsDisabledKey(', 'int key = (')
    assert not any(token in body for token in ('Parsers.', 'ServerInfoManager', 'TimeManager', 'CommonFunctionsDisabledKey'))
    emit('OutputAdditionUtils', body)
    formula = method(read('Formula'), 'public static float Int2Float(int val)')
    emit('Formula', 'internal static class Formula\n{\n' + formula + '\n}')
    hang_up = read('PlayerHangUpDataExt')
    fragments = []
    for signature in (
        'public static OutputAdditionPropType GetOutputAdditionAddPercentType(this Player player, int hangUpId)',
        'public static OutputAdditionPropType GetOutputAdditionMaxStoreType(this Player player, int hangUpId)',
        'public static long CalcOutputAdditionExtStore(this Player player, int hangUpId, long maxCount)'):
        raw = method(hang_up, signature)
        methods[signature] = hashlib.sha256(raw.encode()).hexdigest()
        raw = raw.replace('this Player player', 'this HomeOutputAdditionPlayer player')
        raw = raw.replace('Parsers.GameSettings.OutputAdditionSetting.', 'player.Settings.')
        fragments.append(raw)
    signature = 'public static long CalcOutputAdditionExtCount(int hangUpId, double count)'
    raw = method(read('HangUpUtils'), signature)
    methods[signature] = hashlib.sha256(raw.encode()).hexdigest()
    raw = raw.replace('(int hangUpId, double count)', '(this HomeOutputAdditionPlayer player, int hangUpId, double count)')
    raw = raw.replace('\t\tPlayer mainPlayer = MeManager.MainPlayer;\n', '').replace('mainPlayer.', 'player.')
    fragments.append(raw)
    emit('HomeHangUpOutputAddition', 'public static class HomeHangUpOutputAddition\n{\n' + '\n'.join(fragments) + '\n}')
    models_path = DEST / 'OutputAdditionModels.cs'
    models = models_path.read_text('utf8')
    models = models.replace('public sealed class HomeOutputAdditionPlayer', '''public sealed class HomeOutputAdditionSettings
{
    public Dictionary<int, OutputAdditionPropType> HomeHangUpAddPercentTypeDict;
    public Dictionary<int, OutputAdditionPropType> HomeHangUpMaxStoreTypeDict;
}
public sealed class HomeOutputAdditionPlayer''')
    models = models.replace('public readonly OutputAdditionInfoParser Configuration;',
        'public readonly OutputAdditionInfoParser Configuration;\n    public readonly HomeOutputAdditionSettings Settings;')
    models = models.replace('OutputAdditionInfoParser configuration, Func<DateTime> now, Func<int?, bool> disabled)',
        'OutputAdditionInfoParser configuration, HomeOutputAdditionSettings settings, Func<DateTime> now, Func<int?, bool> disabled)')
    models = models.replace('Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));',
        'Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));\n        Settings = settings ?? throw new ArgumentNullException(nameof(settings));')
    write(models_path, models)

    apk = Apk.current()
    try:
        assert apk.version == '884_156692'
        constructors = Constructors(CODE)
        mapping = {name.rsplit('/', 1)[-1]: kind for kind, name in
                   re.findall(r'new (Tb\w+)\(loader\("([^"]+)"\)\)', constructors.source('Tables'))}
        tables, sources = {}, {}
        for obj in apk.objects('defaultpackage_assets_config_luban.bundle'):
            if obj.type.name != 'TextAsset':
                continue
            name = obj.read().m_Name.rsplit('/', 1)[-1].removesuffix('.g')
            if name not in ('output_addition', 'game_settings'):
                continue
            assert name not in tables
            raw = obj.read().m_Script.encode('utf8', 'surrogateescape')
            tables[name] = constructors.table(raw, mapping[name])
            sources[name] = dict(source=apk.provenance(obj), sha256=hashlib.sha256(raw).hexdigest(), constructor=mapping[name])
        assert set(tables) == {'output_addition', 'game_settings'}
        config = dict(manifest=apk.version, rows=tables['output_addition'], settings=tables['game_settings']['OutputAdditionSetting'])
        write(ROOT / 'unity/Assets/Game/Resources/Home/output-addition-rules.json',
              json.dumps(config, ensure_ascii=False, separators=(',', ':'), allow_nan=False) + '\n')
        for name in constructors.sources:
            path = CODE / 'common-source/Common' / (name + '.cs')
            if not path.is_file():
                path = CODE / 'common-source/Common.LubanGen' / (name + '.cs')
            inputs[path.relative_to(ROOT).as_posix()] = sha(path)
    finally:
        apk.reader.close()
    write(DEST / 'Zjcs.Official.HomeOutputAddition.asmdef', json.dumps(dict(
        name='Zjcs.Official.HomeOutputAddition', references=[], autoReferenced=False, noEngineReferences=True), indent=2) + '\n')
    write(DEST, '', folder=True)
    receipt = dict(manifest='884_156692', generator_sha256=sha(Path(__file__)), inputs=inputs, outputs=outputs,
                   methods=methods, configuration=sources, adaptations=[
                       'Original seven query/filter/time/percentage methods. Player records, current time, configuration lookup and OutputAddition disabled-function checks are explicit ports.',
                       'Original individual hang-up add-percent/max-store mappings and extra-count/extra-capacity formulas retain their float conversions and integer casts.',
                       'Data wrappers retain the original fields consumed by these methods; transport, online identity and database plumbing are excluded.',
                       'Preserve different original child-time handling between group display and property-value queries; do not normalize boundary behavior.',
                       'New-game activity records are empty by the user-selected offline policy; these methods do not schedule or invent activities.'])
    (ROOT / 'assets/reference/home-output-addition-source-manifest.json').write_text(
        json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    print('Migrated original output addition:', len(config['rows']), 'configuration rows and', len(methods), 'methods')


if __name__ == '__main__':
    build()
