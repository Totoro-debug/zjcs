"""Recover selected 884 experience tables and the original home query rules."""
import hashlib
import json
from pathlib import Path
import re

from official_apk import Apk, ROOT
from official_tables import Constructors
from prepare_home_auto_increase_runtime import method

CODE = ROOT/'.tools/source-packages/current/884_156692/official-code'
DEST = ROOT/'unity/Assets/Game/Runtime/Official/HomeExperienceRules'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def metadata(path, folder=False):
    meta = Path(str(path)+'.meta')
    if not meta.exists():
        key = hashlib.md5(('zjcs:home-experience-rules:'+path.relative_to(ROOT).as_posix()).encode()).hexdigest()
        importer = 'folderAsset: yes\nDefaultImporter:\n' if folder else 'AssemblyDefinitionImporter:\n' if path.suffix == '.asmdef' else 'MonoImporter:\n  serializedVersion: 2\n' if path.suffix == '.cs' else 'TextScriptImporter:\n'
        meta.write_text('fileFormatVersion: 2\nguid: '+key+'\n'+importer+'  externalObjects: {}\n', 'utf8', newline='\n')
    return meta


def build():
    inputs, outputs = {}, {}

    def read(name):
        p = CODE/'common-source/Common'/f'{name}.cs'
        inputs[p.relative_to(ROOT).as_posix()] = sha(p)
        return p.read_text('utf-8-sig').replace('\r\n','\n')

    def write(path, text):
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(text, 'utf8', newline='\n')
        for p in (path, metadata(path)):
            outputs[p.relative_to(ROOT).as_posix()] = sha(p)

    def emit(name, body):
        write(DEST/(name+'.cs'), '// Selected original 884 experience implementation; table and player service boundaries only.\n'
              +'using System;\nusing System.Collections.Generic;\nnamespace Zjcs.Official.HomeExperienceRules\n{\n'+body+'\n}\n')

    for name in ('Rank','SubRank'):
        emit(name, read(name).split('namespace Common;')[1].strip())
    enum = read('EnumExt')
    body = '\n'.join(method(enum, signature) for signature in (
        'public static Rank ToRank(this SubRank subRank)',
        'public static SubRank GetHighSubRank(this Rank rank)'))
    # Only the Framework logger is replaced. Keep the original None fallback.
    body = re.sub(r'GLoggerErrorInterpolatedStringHandler message =.*?Log.Error\(ref message,.*?;\n',
                  'Diagnostic?.Invoke("Unsupported rank value");\n', body, flags=re.S)
    assert 'GLogger' not in body and 'Log.Error' not in body
    emit('EnumExt', 'public static class EnumExt\n{\n    public static Action<string> Diagnostic;\n'+body+'}\n')

    for name in ('LevelExp','LevelBlessInfo','SystemLevelLimitInfo'):
        original = read(name)
        fields = [('int','Id'),('List<long>','Exp')] if name == 'LevelExp' else [('Rank','Rank'),('int','BlessLevel'),('long','Exp'),('long','SeasonScore')] if name == 'LevelBlessInfo' else [('SubRank','SubRank'),('int','PlayerLevel'),('int','PlayerBlessLevel')]
        for kind, field in fields:
            assert re.search(r'public (?:readonly )?'+re.escape(kind)+r' '+field+';', original), (name,field)
        emit(name, '[Serializable]\npublic sealed class '+name+'\n{\n'+''.join('    public '+kind+' '+field+';\n' for kind,field in fields)+'}\n')

    text = read('LevelExpParser')
    body = text[text.index('\tpublic const int'):text.index('\tprotected override')]
    emit('LevelExpParser', 'public sealed class LevelExpParser\n{\n'
         +'    private readonly Dictionary<int, LevelExp> rows = new Dictionary<int, LevelExp>();\n'
         +'    public LevelExpParser(IEnumerable<LevelExp> source) { foreach (var row in source) rows.Add(row.Id, row); }\n'
         +'    public LevelExp Get(int id) { rows.TryGetValue(id, out var row); return row; }\n'+body+'}\n')
    text = read('LevelBlessInfoParser')
    post = method(text, 'public override void PostParse()').replace('public override void PostParse()', 'private void PostParse()').replace('\t\t((ParserBase)this).PostParse();\n','')
    body = '\n'.join(method(text, signature) for signature in (
        'public int GetMaxLevel(Rank rank)', 'public long GetExp(Rank rank, int level)',
        'public int GetBlessLevel(Rank rank, long exp)'))
    emit('LevelBlessInfoParser', '''public sealed class LevelBlessInfoParser
{
    private readonly LevelBlessInfo[] rows;
    private readonly Dictionary<Rank, Dictionary<int, LevelBlessInfo>> lookup = new Dictionary<Rank, Dictionary<int, LevelBlessInfo>>();
    private Dictionary<Rank, List<LevelBlessInfo>> mLevelBlessInfos;
    public LevelBlessInfoParser(IEnumerable<LevelBlessInfo> source)
    {
        rows = new List<LevelBlessInfo>(source).ToArray();
        foreach (var row in rows) {
            if (!lookup.TryGetValue(row.Rank, out var group)) lookup.Add(row.Rank, group = new Dictionary<int, LevelBlessInfo>());
            group.Add(row.BlessLevel, row);
        }
        PostParse();
    }
    private LevelBlessInfo[] GetAll() => rows;
    public LevelBlessInfo Get(Rank rank, int level) => lookup.TryGetValue(rank, out var group) && group.TryGetValue(level, out var row) ? row : null;
'''+post+body+'}\n')
    text = read('SystemLevelLimitInfoParser')
    body = '\n'.join(method(text, signature) for signature in (
        'private void SetPlayerMaxLevelLimit(SystemLevelLimitInfo info)',
        'public int GetPlayerBaseLevelLimit(SubRank subRank)', 'public int GetPlayerBlessLevelLimit(Rank rank)'))
    emit('SystemLevelLimitInfoParser', '''public sealed class SystemLevelLimitInfoParser
{
    private Dictionary<SubRank, int> mPlayerBaseLevelLimitByRank = new Dictionary<SubRank, int>();
    private Dictionary<Rank, int> mPlayerBlessLevelLimitByRank = new Dictionary<Rank, int>();
    private HashSet<Rank> mAllRanks = new HashSet<Rank>();
    private Rank mFirstPlayerBlessLevelRank;
    public Rank FirstPlayerBlessLevelRank => mFirstPlayerBlessLevelRank;
    public IEnumerable<Rank> GetAllRanks => mAllRanks;
    public SystemLevelLimitInfoParser(IEnumerable<SystemLevelLimitInfo> rows)
    {
        mFirstPlayerBlessLevelRank = Rank.None;
        foreach (var row in rows) SetPlayerMaxLevelLimit(row);
    }
'''+body+'}\n')

    original = read('AstrologicalSeasonConfig')
    fields = [('int','Season'), ('Rank','Rank'), ('int','NextSeason'), ('int','SourceStarRateMatrial'), ('float','SourceStarRateMatrialValue')]
    for kind,field in fields:
        assert re.search(r'public (?:readonly )?'+kind+r' '+field+';', original)
    emit('AstrologicalSeasonConfig', '[Serializable]\npublic sealed class AstrologicalSeasonConfig\n{\n'+''.join('    public '+kind+' '+field+';\n' for kind,field in fields)+'}\n')
    original = read('AstrologicalSeasonConfigParser')
    post = method(original, 'public override void PostParse()').replace('public override void PostParse()', 'private void PostParse()').replace('\t\t((ParserBase)this).PostParse();\n','')
    emit('AstrologicalSeasonConfigParser', '''public sealed class AstrologicalSeasonConfigParser
{
    private readonly AstrologicalSeasonConfig[] rows;
    private Dictionary<Rank, AstrologicalSeasonConfig> mAstrologicalConfigs = new Dictionary<Rank, AstrologicalSeasonConfig>();
    public int MinSeason { get; private set; }
    public int MaxSeason { get; private set; }
    public AstrologicalSeasonConfigParser(IEnumerable<AstrologicalSeasonConfig> source) { rows = new List<AstrologicalSeasonConfig>(source).ToArray(); PostParse(); }
    private AstrologicalSeasonConfig[] GetAll() => rows;
'''+post+method(original, 'public int GetSeasonByRank(Rank rank)')+'}\n')

    original = read('PlayerBlessLevelUtils')
    assert re.search(r'PlayerExpLimit\s*=\s*173', read('FunctionType'))
    signatures = (
        'public static bool IsBlessLevelOpen(this Player player)',
        'public static void UpdateShowLevel(this Player player)',
        'public static int GetCurBlessPlayerLevel(this Player player)',
        'public static int GetBlessPlayerLevel(this Player player, Rank rank)',
        'public static int GetBlessPlayerLevel(this Player player, int season)',
        'public static long GetBlessPlayerExp(this Player player, int season)',
        'public static long GetCurBlessPlayerExp(this Player player)',
        'public static long GetMaxBlessExp(this Player player, Rank planeRank)',
        'public static bool IsMaxBlessLevel(this Player player)',
        'public static bool HasBlessLevel(this Player player)',
        'private static int GetSeason(this Player player, PlayerSeasonRecordDataWrap seasonRecordData)',
        'private static Rank GetPlaneRank(this Player player, PlayerSeasonRecordDataWrap seasonRecordData)',
        'public static bool PlayerExpLimit(this Player player)',
        'public static long GetPlayerExpLimitCount(this Player player)',
        'public static bool ShowPlayerStoreExpLimit(this Player player)',
        'public static int GetMaxStoreExpLevel(this Player player)',
        'public static long GetCurrentExp(this Player player)',
        'public static long GetExpRequestByBlessLevel(int blessLevel)')
    body = '\n'.join(method(original, signature) for signature in signatures)
    body += method(read('PlayerExpUtils'), 'public static bool IsMaxLevelByRankLimit(this Player player)')
    replacements = (
        ('this Player player', 'this HomeExperiencePlayer player'),
        ('PlayerSeasonRecordDataWrap', 'HomeExperienceSeasonRecord'),
        ('TryGetSeasonBlessData<PlayerSeasonRecordPlayerBlessLevelRecordDataWrap>', 'TryGetSeasonBlessData'),
        ('Parsers.Instance.SystemLevelLimitInfoParser', 'player.Tables.SystemLevelLimitInfoParser'),
        ('Parsers.Instance.LevelBlessInfoParser', 'player.Tables.LevelBlessInfoParser'),
        ('Parsers.Instance.LevelExpParser', 'player.Tables.LevelExpParser'),
        ('Parsers.Instance.AstrologicalSeasonConfigParser', 'player.Tables.AstrologicalSeasonConfigParser'),
        ('Parsers.GameSettings.PlayerExpItemId', 'player.Tables.PlayerExpItemId'),
        ('Parsers.GameSettings.BlessLevelSettings.FirstBlessRank', 'player.Tables.FirstBlessRank'),
        ('PlanesInfoUtils.PlanesRank', 'player.PlanesRank'),
        ('Parsers.Instance.FunctionLimitInfoParser.IsLimit(player, FunctionType.PlayerExpLimit)', 'player.PlayerExpLimitFunctionLimited'),
        ('player.PlayerSeasonRecordDataManager.CurrentRecordData', 'player.CurrentRecordData'),
        ('public static long GetExpRequestByBlessLevel(int blessLevel)', 'public static long GetExpRequestByBlessLevel(this HomeExperiencePlayer player, int blessLevel)'),
        ('\t\tPlayer mainPlayer = MeManager.MainPlayer;\n', ''),
        ('mainPlayer.', 'player.'))
    for before,after in replacements: body = body.replace(before,after)
    assert all(token not in body for token in ('Parsers.', 'MeManager', 'PlayerSeasonRecord', 'PlanesInfoUtils'))
    emit('PlayerBlessLevelUtils', 'public static class PlayerBlessLevelUtils\n{\n'+body+'}\n')
    text = read('Player')
    refresh = method(text, 'public bool TryRefreshLevel()')
    refresh = refresh.replace('Parsers.GameSettings.PlayerExpItemId','Tables.PlayerExpItemId').replace('Parsers.Instance.LevelExpParser','Tables.LevelExpParser').replace('Parsers.Instance.SystemLevelLimitInfoParser','Tables.SystemLevelLimitInfoParser')
    emit('HomeExperiencePlayer', '''// A read projection: it never writes the local profile or invents season records.
public sealed class HomeExperiencePlayer
{
    public HomeExperienceTables Tables { get; }
    public SubRank SubRank;
    public Rank Rank => SubRank.ToRank();
    public int BaseLevel;
    public int Level;
    public bool Inited;
    public double PlayerLevelLimitProperty;
    public int LevelLimit => (int)((double)Tables.SystemLevelLimitInfoParser.GetPlayerBaseLevelLimit(SubRank) + PlayerLevelLimitProperty);
    public int MaxLevelLimit => Tables.LevelExpParser.GetLevelMax(1);
    public int ServerRecordSeason;
    public Rank PlanesRank;
    public HomeExperienceSeasonRecord CurrentRecordData;
    public bool PlayerExpLimitFunctionLimited;
    public long Experience;
    public readonly Dictionary<int, HomeExperienceBlessRecord> BlessRecords = new Dictionary<int, HomeExperienceBlessRecord>();
    public event Action<int> OnLevelUpdate;
    public HomeExperiencePlayer(HomeExperienceTables tables) { Tables = tables ?? throw new ArgumentNullException(nameof(tables)); }
    public long GetItemAmount(int itemId) => itemId == Tables.PlayerExpItemId ? Experience : 0;
    public bool TryGetSeasonBlessData(int season, out HomeExperienceBlessRecord record) => BlessRecords.TryGetValue(season, out record);
    public void SetLevel(int value) { BaseLevel = value; }
    public void FireOnLevelUpdate(int level) { OnLevelUpdate?.Invoke(level); }
'''+refresh+''' }
[Serializable] public sealed class HomeExperienceSeasonRecord { public int Season; public Rank RecordRank; }
[Serializable] public sealed class HomeExperienceBlessRecord { public long PlayerBlessExp; }
public sealed class HomeExperienceTables
{
    public LevelExpParser LevelExpParser { get; }
    public LevelBlessInfoParser LevelBlessInfoParser { get; }
    public SystemLevelLimitInfoParser SystemLevelLimitInfoParser { get; }
    public AstrologicalSeasonConfigParser AstrologicalSeasonConfigParser { get; }
    public int PlayerExpItemId { get; }
    public Rank FirstBlessRank { get; }
    public HomeExperienceTables(IEnumerable<LevelExp> experience, IEnumerable<LevelBlessInfo> blessing,
        IEnumerable<SystemLevelLimitInfo> limits, IEnumerable<AstrologicalSeasonConfig> seasons, int playerExpItemId, Rank firstBlessRank)
    {
        LevelExpParser = new LevelExpParser(experience); LevelBlessInfoParser = new LevelBlessInfoParser(blessing);
        SystemLevelLimitInfoParser = new SystemLevelLimitInfoParser(limits);
        AstrologicalSeasonConfigParser = new AstrologicalSeasonConfigParser(seasons);
        PlayerExpItemId = playerExpItemId; FirstBlessRank = firstBlessRank;
    }
}
''')

    apk = Apk(reuse_current_cache=True)
    try:
        constructors = Constructors(CODE)
        mapping = {name.rsplit('/',1)[-1]:kind for kind,name in re.findall(r'new (Tb\w+)\(loader\("([^"]+)"\)\)', constructors.source('Tables'))}
        tables, sources = {}, {}
        wanted = ('game_settings','level_exp','level_bless','system_level_limit','astrological_season_config')
        for bundle in ('defaultpackage_assets_config_luban.bundle', 'defaultpackage_assets_config_luban_astrologicaldestiny.bundle'):
            for obj in apk.objects(bundle):
                if obj.type.name != 'TextAsset': continue
                name = obj.read().m_Name.rsplit('/',1)[-1].removesuffix('.g')
                if name not in wanted: continue
                raw = obj.read().m_Script.encode('utf8','surrogateescape')
                tables[name] = constructors.table(raw,mapping[name])
                sources[name] = dict(source=apk.provenance(obj), sha256=hashlib.sha256(raw).hexdigest(), constructor=mapping[name])
        assert set(tables) == set(wanted)
        settings = tables.pop('game_settings')
        config = dict(manifest=apk.version, playerExpItemId=settings['PlayerExpItemId'],
                      firstBlessRank=settings['BlessLevelSettings']['FirstBlessRank'], tables=tables)
        write(ROOT/'unity/Assets/Game/Resources/Home/experience-rules.json', json.dumps(config,ensure_ascii=False,separators=(',',':'))+'\n')
        for name in constructors.sources:
            p = CODE/'common-source/Common'/f'{name}.cs'
            if not p.is_file(): p = CODE/'common-source/Common.LubanGen'/f'{name}.cs'
            inputs[p.relative_to(ROOT).as_posix()] = sha(p)
    finally:
        apk.reader.close()
    write(DEST/'Zjcs.Official.HomeExperienceRules.asmdef', json.dumps(dict(name='Zjcs.Official.HomeExperienceRules',
        rootNamespace='Zjcs.Official.HomeExperienceRules', references=[], noEngineReferences=True),indent=2)+'\n')
    for p in (metadata(DEST,True),): outputs[p.relative_to(ROOT).as_posix()] = sha(p)
    for name in ('official_apk.py','official_tables.py','official_source.py','official_bundle_reader.py','prepare_home_auto_increase_runtime.py'):
        p = ROOT/'scripts'/name; inputs[p.relative_to(ROOT).as_posix()] = sha(p)
    proof = dict(manifest='884_156692', generator_sha256=sha(Path(__file__)), inputs=inputs, outputs=outputs,
        configuration=sources, playerExpItemId=config['playerExpItemId'], firstBlessRank=config['firstBlessRank'],
        adaptations=['Original parser methods with explicit local table constructors instead of Luban parser base classes.',
            'Original PlayerBlessLevelUtils query branches and Player.TryRefreshLevel; explicit selected tables, season record, plane rank, function 173 and non-battle property boundaries.',
            'Projection refresh only: does not mutate profile progression or generate server season records.',
            'Original rank switches; diagnostic logger port retains original None fallback.'])
    path = ROOT/'assets/reference/home-experience-rules-source-manifest.json'
    path.write_text(json.dumps(proof,ensure_ascii=False,indent=2)+'\n','utf8',newline='\n')
    print('Recovered selected experience rules:', {key:len(value) for key,value in tables.items()}, 'item',config['playerExpItemId'],'first bless rank',config['firstBlessRank'],flush=True)
    return proof


if __name__ == '__main__':
    build()
