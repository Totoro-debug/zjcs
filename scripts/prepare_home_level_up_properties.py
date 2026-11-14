"""Migrate the selected original character battle properties used by level-up UI."""
import hashlib
import json
from pathlib import Path
import re

from official_apk import Apk, ROOT
from official_tables import Constructors
from prepare_home_auto_increase_runtime import CODE, metadata, method, sha

DEST = ROOT/'unity/Assets/Game/Runtime/Official/HomeLevelUpRules'
RECEIPT = ROOT/'assets/reference/home-level-up-properties-source-manifest.json'


def strip_logger(text):
    # Only remove interpolated logger formatting. Keep conditions, returns,
    # exception handling, dictionary operations and their order unchanged.
    for match in reversed(list(re.finditer(r'(?m)^(\t+)if \(enabled\)\n\1\{', text))):
        brace = text.index('{', match.start()); end = brace+1; depth = 1
        while depth:
            depth += (text[end] == '{')-(text[end] == '}'); end += 1
        body = text[brace+1:end-1]
        assert all(not line.strip() or re.match(r'message\.Append', line.strip()) for line in body.splitlines())
        text = text[:match.start()]+text[end:]
    result = []
    for line in text.splitlines():
        if re.search(r'GLogger\w+InterpolatedStringHandler|^\s*bool enabled;', line): continue
        if re.match(r'\s*Log\.Error\(', line):
            exception = ' + ": " + exception' if 'Log.Error(exception,' in line else ''
            line = re.match(r'\s*', line)[0]+'HomeLevelUpDiagnostics.Error("'+('LevelPropBinaryReader' if 'LevelPropBinaryReader.cs' in line else 'LevelPropParser' if 'LevelPropParser.cs' in line else 'BattleFormulaHandler')+' original input error"'+exception+');'
        result.append(line)
    return '\n'.join(result)+'\n'


def build():
    inputs, outputs = {}, {}
    manifest = ROOT/'assets/reference/official-client-code/source-manifest.json'
    provenance = json.loads(manifest.read_bytes())
    assert provenance['manifest'] == '884_156692'
    inputs[manifest.relative_to(ROOT).as_posix()] = sha(manifest)
    for folder in ('common-source','framework-firstpass-source'):
        record = next(row for row in provenance['projects'] if row['folder'] == folder)
        assembly = CODE/'managed/decoded'/record['assembly']
        assert sha(assembly) == record['source']['decoded_sha256']
        inputs[assembly.relative_to(ROOT).as_posix()] = sha(assembly)
    def read(path):
        if isinstance(path, str): path = CODE/'common-source/Common'/(path+'.cs')
        inputs[path.relative_to(ROOT).as_posix()] = sha(path)
        return path.read_text('utf-8-sig').replace('\r\n', '\n')
    def write(path, text):
        path.parent.mkdir(parents=True, exist_ok=True); path.write_text(text, 'utf8', newline='\n')
        for item in (path, metadata(path)): outputs[item.relative_to(ROOT).as_posix()] = sha(item)
    def emit(name, body):
        write(DEST/(name+'.cs'), '// Selected original 884 implementation; configuration and logging boundaries only.\n'
              +'using System;\nusing System.IO;\nusing System.Linq;\nusing System.Collections;\nusing System.Collections.Generic;\n'
              +'using Zjcs.Official.HomeIncomeRules;\nusing Zjcs.Official.HomeExperienceRules;\n'
              +'namespace Zjcs.Official.HomeLevelUpRules\n{\n'+body+'\n}\n')
    original = read('BattleFormulaHandler')
    formula = '\n'.join(method(original, signature) for signature in (
        'public Dictionary<PropType, long> CalcCharacterBattlePropsWithoutProfession(int level, SubRank subRank)',
        'public Dictionary<PropType, long> CalcCharacterBattleProps(Profession pro, IDictionary<Profession, int> professionLevels, int level, SubRank jingJie)',
        'public Dictionary<PropType, long> CalcTotalProfessionLevelProps(IDictionary<Profession, int> dict)',
        'public Dictionary<PropType, long> CalcProfessionFeatureProps(Profession pro)',
        'public long GetRawProp(Dictionary<PropType, long> props, PropType type)',
        'public Dictionary<PropType, long> AddProps(IReadOnlyDictionary<PropType, long> props, IReadOnlyDictionary<PropType, long> addProps)',
        'public void AddProp(Dictionary<PropType, long> props, PropType propType, long value)',
        'public void MergePropsInPlace(Dictionary<PropType, long> dest, IReadOnlyDictionary<PropType, long> add)'))
    formula = strip_logger(formula).replace('Parsers.Instance.', 'Tables.').replace('Parsers.GameSettings.', 'Tables.Settings.')
    emit('HomeCharacterBattleFormula', 'public sealed class HomeCharacterBattleFormula\n{\n'
         +'    private readonly HomeCharacterBattleTables Tables;\n    public HomeCharacterBattleFormula(HomeCharacterBattleTables tables) { Tables = tables ?? throw new ArgumentNullException(nameof(tables)); }\n'
         +formula+'}\n')
    emit('Profession', read('Profession').split('namespace Common;\n', 1)[1])
    for name in ('PropType', 'SubRank'):
        source = read(name); body = source[source.index('public enum '+name):].strip()
        existing = ROOT/'unity/Assets/Game/Runtime/Official'/('HomeIncomeRules' if name == 'PropType' else 'HomeExperienceRules')/(name+'.cs')
        migrated = read(existing)
        assert body in migrated, 'Reuse only the exact selected enum: '+name
    for name in ('LRUCollection', 'SortedValueDictionary'):
        text = read(CODE/'framework-firstpass-source/Framework.Collections'/(name+'.cs'))
        emit(name, text.split('namespace Framework.Collections;\n', 1)[1])
    emit('IPropDictionary', read('IPropDictionary').split('namespace Common;\n', 1)[1])
    emit('PropDictionaryForNormal', read(CODE/'common-source/Common.PropDict/PropDictionaryForNormal.cs').split('namespace Common.PropDict;\n', 1)[1])
    emit('PropDictionaryUtils', read('PropDictionaryUtils').split('namespace Common;\n', 1)[1])
    emit('Utils', 'internal static class Utils\n{\n'+method(read('Utils'), 'public static void AddRange<TKey, TValue>(this Dictionary<TKey, TValue> target, Dictionary<TKey, TValue> source)')+'}\n')
    emit('LevelPropBinaryReader', strip_logger(read('LevelPropBinaryReader').split('namespace Common;\n', 1)[1]))
    parser = read('LevelPropParser')
    members = '\n'.join(method(parser, signature) for signature in (
        'public IReadOnlyDictionary<PropType, long> GetProps(int classId, int level)',
        'private IReadOnlyDictionary<PropType, long> GetConfigProps(int classId, int level)',
        'public bool TryGetLevelRange(int classId, out (int, int) levelRange)',
        'private bool TryGetPropDict(int classId, int level, out IPropDictionary propDict)', 'public void Dispose()'))
    lazy = '\t\t\tif (mDefaultValueParser == null)\n\t\t\t{\n\t\t\t\tmDefaultValueParser = ((ParserBase)this).ConfigManager.GetParser<LevelPropDefaultValueParser>();\n\t\t\t}\n'
    assert members.count(lazy) == 1
    members = strip_logger(members.replace(lazy, ''))
    emit('LevelPropParser', '''public sealed class LevelPropParser : IDisposable
{
    private LevelPropBinaryReader mBinaryReader;
    private readonly LRUCollection<(int,int),IPropDictionary> mCache = new LRUCollection<(int,int),IPropDictionary>(1000);
    private readonly LevelPropDefaultValueParser mDefaultValueParser;
    public LevelPropParser(byte[] bytes, IEnumerable<LevelPropDefaultValue> defaults)
    { mBinaryReader = new LevelPropBinaryReader(bytes); mDefaultValueParser = new LevelPropDefaultValueParser(defaults); }
'''+members+'}\n')
    role = method(read('RolePropGroupInfo'), 'public Dictionary<PropType, long> GetProps(int level)')
    role = role.replace('Parsers.Instance.', 'Tables.').replace('BattleFormula.Instance.', 'Tables.Formula.')
    emit('RolePropGroupInfo', '''[Serializable] public sealed class RolePropGroupInfo
{
    public int ID, LevelPropID, ExtraLevelPropID;
    public SubRank SubRank;
    internal HomeCharacterBattleTables Tables;
'''+role+'}\n')
    # These DTOs contain only the original fields consumed by the methods above.
    read('ProfessionBaseInfo'); read('LevelPropDefaultValue'); read('RolePropGroupInfoParser'); read('ProfessionBaseInfoParser')
    emit('HomeCharacterBattleTables', '''[Serializable] public sealed class HomeBattleSettings
{ public int PlayerLevelBattlePropId, PlayerSubRankBattlePropId, PlayerFightRolePropGroupId; }
[Serializable] public sealed class ProfessionBaseInfo
{ public Profession Profession; public int LevelPropId; public Dictionary<PropType,long> FeatureProps; }
[Serializable] public sealed class LevelPropDefaultValue
{ public int ClassId; public List<PropType> PropTypes; public long DefaultValue; }
public sealed class LevelPropDefaultValueParser
{
    private readonly Dictionary<int,LevelPropDefaultValue> rows;
    public LevelPropDefaultValueParser(IEnumerable<LevelPropDefaultValue> rows) { this.rows = rows.ToDictionary(row=>row.ClassId); }
    public bool TryGet(int id, out LevelPropDefaultValue row) => rows.TryGetValue(id, out row);
}
public sealed class ProfessionBaseInfoParser
{
    private readonly Dictionary<Profession,ProfessionBaseInfo> rows;
    public ProfessionBaseInfoParser(IEnumerable<ProfessionBaseInfo> rows) { this.rows = rows.ToDictionary(row=>row.Profession); }
    public ProfessionBaseInfo Get(Profession id) => rows.TryGetValue(id,out var row) ? row : null;
}
public sealed class RolePropGroupInfoParser
{
    private readonly Dictionary<(int,SubRank),RolePropGroupInfo> rows;
    public RolePropGroupInfoParser(IEnumerable<RolePropGroupInfo> rows) { this.rows = rows.ToDictionary(row=>(row.ID,row.SubRank)); }
    public RolePropGroupInfo Get(int id, SubRank rank) => rows.TryGetValue((id,rank),out var row) ? row : null;
}
public sealed class HomeCharacterBattleTables : IDisposable
{
    public HomeBattleSettings Settings { get; }
    public LevelPropParser LevelPropParser { get; }
    public ProfessionBaseInfoParser ProfessionBaseInfoParser { get; }
    public RolePropGroupInfoParser RolePropGroupInfoParser { get; }
    public HomeCharacterBattleFormula Formula { get; }
    public HomeCharacterBattleTables(HomeBattleSettings settings, byte[] levelBytes,
        IEnumerable<ProfessionBaseInfo> professions, IEnumerable<RolePropGroupInfo> roles, IEnumerable<LevelPropDefaultValue> defaults)
    {
        Settings = settings ?? throw new ArgumentNullException(nameof(settings));
        LevelPropParser = new LevelPropParser(levelBytes, defaults);
        ProfessionBaseInfoParser = new ProfessionBaseInfoParser(professions);
        var roleRows = roles.ToArray();
        RolePropGroupInfoParser = new RolePropGroupInfoParser(roleRows);
        Formula = new HomeCharacterBattleFormula(this);
        foreach (var row in roleRows) row.Tables = this;
    }
    public void Dispose() => LevelPropParser.Dispose();
}
public static class HomeLevelUpDiagnostics
{ public static Action<string> Error = message => System.Diagnostics.Trace.TraceError(message); }
''')
    write(DEST/'Zjcs.Official.HomeLevelUpRules.asmdef', json.dumps(dict(name='Zjcs.Official.HomeLevelUpRules',
        references=['Zjcs.Official.HomeIncomeRules','Zjcs.Official.HomeExperienceRules'],noEngineReferences=True),indent=2)+'\n')
    apk = Apk(reuse_current_cache=True)
    configuration = {}
    try:
        constructors = Constructors(CODE)
        mapping = {name.rsplit('/',1)[-1]:kind for kind,name in re.findall(r'new (Tb\w+)\(loader\("([^\"]+)"\)\)', constructors.source('Tables'))}
        tables = {}
        for name in ('game_settings', 'profession_base', 'role_prop_group', 'level_prop_default_value'):
            obj = apk.asset('config_luban', '/'+name+'.g.bytes'); raw = obj.read().m_Script.encode('utf8','surrogateescape')
            tables[name] = constructors.table(raw, mapping[name])
            configuration[name] = dict(source=apk.provenance(obj), sha256=hashlib.sha256(raw).hexdigest(), constructor=mapping[name])
        settings = tables['game_settings']
        if isinstance(settings, list):
            assert len(settings) == 1; settings = settings[0]
        settings = {k: settings[k] for k in ('PlayerLevelBattlePropId','PlayerSubRankBattlePropId','PlayerFightRolePropGroupId')}
        tables['game_settings'] = settings
        tables['profession_base'] = [{k: row[k] for k in ('Profession','LevelPropId','FeatureProps')} for row in tables['profession_base']]
        tables['role_prop_group'] = [row for row in tables['role_prop_group'] if row['ID'] == settings['PlayerFightRolePropGroupId']]
        verified = apk.reader.read('WwiseBnk', 'wwisebnk_assets_config_binary_level_prop_g_bytes.rawfile')
        shared = ROOT/'unity/Assets/StreamingAssets/PartnerNpcShared/Config/Binary/level_prop.g.bytes'
        assert shared.read_bytes() == verified.data, 'Keep the selected shared binary; never replace another owner.'
        inputs[shared.relative_to(ROOT).as_posix()] = sha(shared)
        configuration['level_prop'] = dict(source=dict(verified.provenance),sha256=sha(shared),shared_path=shared.relative_to(ROOT).as_posix())
        for name in constructors.sources:
            p = CODE/'common-source/Common'/(name+'.cs')
            if not p.is_file(): p = CODE/'common-source/Common.LubanGen'/(name+'.cs')
            inputs[p.relative_to(ROOT).as_posix()] = sha(p)
        path = ROOT/'unity/Assets/Game/Resources/Home/level-up-properties.json'
        write(path,json.dumps(dict(manifest='884_156692',tables=tables,level_prop_sha256=sha(shared)),ensure_ascii=False,separators=(',',':'))+'\n')
    finally: apk.reader.close()
    item = metadata(DEST,True); outputs[item.relative_to(ROOT).as_posix()] = sha(item)
    for name in ('official_apk.py','official_tables.py','official_source.py','prepare_home_auto_increase_runtime.py'):
        p = ROOT/'scripts'/name; inputs[p.relative_to(ROOT).as_posix()] = sha(p)
    for name in ('prepare_home_level_up_properties_validation.py','prepare_unity_character_catalog.py',
                 'prepare_home_level_up_validation.py','fixtures/home_level_up_native_validation.cs',
                 'fixtures/home_level_up_properties_checks.cs'):
        p = ROOT/'scripts'/name; inputs[p.relative_to(ROOT).as_posix()] = sha(p)
    folder = ROOT/'unity/Assets/Game/Runtime/Offline/HomeLevelUpUI'
    for p in sorted(folder.iterdir()):
        if p.suffix not in ('.cs','.asmdef'): continue
        for item in (p,metadata(p)): outputs[item.relative_to(ROOT).as_posix()] = sha(item)
    item = metadata(folder,True); outputs[item.relative_to(ROOT).as_posix()] = sha(item)
    proof = dict(manifest='884_156692',generator_sha256=sha(Path(__file__)),inputs=inputs,outputs=outputs,configuration=configuration,
        adaptations=['Explicit table owner instead of global Parsers/BattleFormula singletons',
                     'Original binary reader, zero omission, default fill, maximum-level fallback and 1000-entry LRU retained',
                     'Configuration DTO fields limited to consumed original fields; original logger formatting routed through diagnostics'])
    RECEIPT.write_text(json.dumps(proof,ensure_ascii=False,indent=2)+'\n','utf8',newline='\n')
    print('Recovered selected character battle formula:', {k:len(v) for k,v in tables.items()}, flush=True)
    return proof


if __name__ == '__main__': build()
