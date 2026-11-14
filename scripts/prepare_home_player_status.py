"""Recover selected player-status records and original client/shared transitions."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import shutil

from official_apk import Apk, ROOT
from official_tables import Constructors
from prepare_home_auto_increase_runtime import method
from prepare_home_experience_rules import metadata, sha
from validation_workspace import require_temporary_unity_project

CODE = ROOT/'.tools/source-packages/current/884_156692/official-code'
RULES = ROOT/'unity/Assets/Game/Runtime/Official/HomePlayerStatusRules'


def build():
    inputs, outputs = {}, {}
    def source(name):
        p = CODE/'common-source/Common'/f'{name}.cs'
        inputs[p.relative_to(ROOT).as_posix()] = sha(p)
        return p.read_text('utf-8-sig').replace('\r\n', '\n')
    def write(path, text):
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(text, 'utf8', newline='\n')
        for p in (path, metadata(path)):
            outputs[p.relative_to(ROOT).as_posix()] = sha(p)
    def emit(name, body):
        write(RULES/(name+'.cs'), '// Selected original 884 player-status implementation; storage/configuration boundaries only.\n'
              +'using System;\nusing System.Collections.Generic;\nnamespace Zjcs.Official.HomePlayerStatusRules\n{\n'+body+'\n}\n')
    for name in ('EStatusSourceType', 'StatusEffectIconType'):
        body = source(name).split('namespace Common;')[1].strip()
        emit(name, body)
        panel_enum = ROOT/'unity/Assets/Game/Runtime/Official/HomePlayerPanelUI'/f'{name}.cs'
        assert body in panel_enum.read_text('utf-8-sig').replace('\r\n', '\n')
        inputs[panel_enum.relative_to(ROOT).as_posix()] = sha(panel_enum)
    original = source('PlayerStatusEffectData')
    for kind, name in (('int', 'ActivedTimes'), ('bool', 'IsActive'), ('DateTime', 'LastGainedTime'),
                       ('bool', 'LastIsReduceOncePerPlay'), ('List<long>', 'OccupiedFightIds')):
        assert re.search(r'public '+re.escape(kind)+' '+name+r'\b', original), name
    emit('PlayerStatusEffectData', '''[Serializable]
public sealed class PlayerStatusEffectData
{
    public long PlayerId;
    public int ClassId;
    public EStatusSourceType StatusSourceType;
    public int ActivedTimes;
    public bool IsActive = true;
    // JsonUtility does not serialize DateTime. Preserve its ticks exactly.
    public long LastGainedTimeTicks;
    public DateTimeKind LastGainedTimeKind;
    public DateTime LastGainedTime
    {
        get => new DateTime(LastGainedTimeTicks, LastGainedTimeKind);
        set { LastGainedTimeTicks = value.Ticks; LastGainedTimeKind = value.Kind; }
    }
    public bool LastIsReduceOncePerPlay;
    public List<long> OccupiedFightIds = new List<long>();
}''')
    original = source('StatusEffectInfo')
    fields = [('int', 'ClassId'), ('int', 'MaxActivateTimes'), ('int', 'EntryId'),
              ('int', 'Duration'), ('StatusEffectIconType', 'IconType'), ('bool', 'IsReduceOncePerPlay'), ('List<int>', 'Scopes')]
    for kind, name in fields:
        assert re.search(r'public readonly '+re.escape(kind)+' '+name+';', original), name
    emit('StatusEffectInfo', '[Serializable]\npublic sealed class StatusEffectInfo\n{\n'
         +''.join('    public '+kind+' '+name+';\n' for kind, name in fields)+'}\n')
    original = source('PlayerStatusEffectDataWrap')
    signatures = ('public int GetKey()', 'public void Reset()',
                  'public bool TryResetActivedTimesOnReduceOncePerPlayChanged()',
                  'public void Deactivate()', 'public int GetRemainTimes()',
                  'public int GetDisplayRemainTimes()', 'public bool ShouldBeRemove()',
                  'public bool IsActiveForFight(long fightUniqueId)',
                  'public bool IsOccupiedBy(long fightUniqueId)', 'public bool TryOccupy(long fightUniqueId)',
                  'public void ReleaseFinishedOccupies(Func<long, bool> isPlayRunning)')
    body = '\n'.join(method(original, signature) for signature in signatures)
    body = body.replace('TimeManager.Instance.GetDateTime()', 'clock()')
    create = method(original, 'public static PlayerStatusEffectDataWrap CreateNew(Player player, int classId)')
    create = create.replace('CreateNew(Player player, int classId)',
                            'CreateNew(long playerId, int classId, Func<int, StatusEffectInfo> configuration, Func<DateTime> clock)')
    create = create.replace('PlayerStatusEffectDataWrap playerStatusEffectDataWrap = CreateNew();',
                            'var playerStatusEffectDataWrap = new PlayerStatusEffectDataWrap(new PlayerStatusEffectData(), configuration, clock);')
    create = create.replace('player.Id', 'playerId').replace('TimeManager.Instance.GetDateTime()', 'clock()')
    props = [('long', 'PlayerId'), ('int', 'ClassId'), ('EStatusSourceType', 'StatusSourceType'),
             ('int', 'ActivedTimes'), ('bool', 'IsActive'), ('DateTime', 'LastGainedTime'), ('bool', 'LastIsReduceOncePerPlay')]
    emit('PlayerStatusEffectDataWrap', '''public sealed class PlayerStatusEffectDataWrap
{
    public PlayerStatusEffectData Data { get; }
    private readonly Func<int, StatusEffectInfo> configuration;
    private readonly Func<DateTime> clock;
    public PlayerStatusEffectDataWrap(PlayerStatusEffectData data, Func<int, StatusEffectInfo> configuration, Func<DateTime> clock)
    {
        Data = data ?? throw new ArgumentNullException(nameof(data));
        this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }
    public StatusEffectInfo Info => configuration(ClassId) ?? throw new InvalidOperationException("Unbound status configuration: " + ClassId);
    public List<long> OccupiedFightIds => Data.OccupiedFightIds ?? (Data.OccupiedFightIds = new List<long>());
'''+''.join('    public '+kind+' '+name+' { get => Data.'+name+'; set => Data.'+name+' = value; }\n' for kind, name in props)
         +create+'\n'+body+'\n}\n')
    gain = method(source('PlayerStatusEffectManager'), 'public PlayerStatusEffectDataWrap GainStatusEffect(int classId, EStatusSourceType statusSourceType)')
    gain = gain.replace('CreateNew(Player, classId)', 'CreateNew(playerId, classId, configuration, clock)')
    emit('PlayerStatusEffectManager', '''public sealed class PlayerStatusEffectManager
{
    private readonly long playerId;
    private readonly Func<int, StatusEffectInfo> configuration;
    private readonly Func<DateTime> clock;
    private readonly IDictionary<int, PlayerStatusEffectDataWrap> records;
    public PlayerStatusEffectManager(long playerId, IDictionary<int, PlayerStatusEffectDataWrap> records,
        Func<int, StatusEffectInfo> configuration, Func<DateTime> clock)
    {
        this.playerId = playerId; this.records = records ?? throw new ArgumentNullException(nameof(records));
        this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }
    private bool TryGetValue(int id, out PlayerStatusEffectDataWrap value) => records.TryGetValue(id, out value);
    private void Add(int id, PlayerStatusEffectDataWrap value) => records.Add(id, value);
'''+gain+'\n}\n')
    write(RULES/'Zjcs.Official.HomePlayerStatusRules.asmdef', json.dumps(dict(
        name='Zjcs.Official.HomePlayerStatusRules', references=[], noEngineReferences=True), indent=2)+'\n')
    apk = Apk(reuse_current_cache=True)
    try:
        constructors = Constructors(CODE)
        mapping = {name.rsplit('/', 1)[-1]: kind for kind, name in re.findall(r'new (Tb\w+)\(loader\("([^"]+)"\)\)', constructors.source('Tables'))}
        tables, provenance = {}, {}
        wanted = ('status_effect', 'status_effect_scope', 'entry')
        for obj in apk.objects('defaultpackage_assets_config_luban.bundle'):
            if obj.type.name != 'TextAsset': continue
            name = obj.read().m_Name.rsplit('/', 1)[-1].removesuffix('.g')
            if name not in wanted: continue
            raw = obj.read().m_Script.encode('utf8', 'surrogateescape')
            tables[name] = constructors.table(raw, mapping[name])
            provenance[name] = dict(source=apk.provenance(obj), sha256=hashlib.sha256(raw).hexdigest(), constructor=mapping[name])
        assert set(tables) == set(wanted), set(tables)
        write(ROOT/'unity/Assets/Game/Resources/Home/player-status.json', json.dumps(
            dict(manifest='884_156692', statuses=tables['status_effect'], scopes=tables['status_effect_scope'], entries=tables['entry']),
            ensure_ascii=False, separators=(',', ':'))+'\n')
        for name in constructors.sources:
            p = CODE/'common-source/Common'/f'{name}.cs'
            if not p.is_file(): p = CODE/'common-source/Common.LubanGen'/f'{name}.cs'
            inputs[p.relative_to(ROOT).as_posix()] = sha(p)
    finally:
        apk.reader.close()
    outputs[metadata(RULES, True).relative_to(ROOT).as_posix()] = sha(metadata(RULES, True))
    for relative in ('scripts/official_apk.py', 'scripts/official_tables.py', 'scripts/official_source.py',
                     'scripts/prepare_home_auto_increase_runtime.py', 'scripts/prepare_home_experience_rules.py',
                     'scripts/fixtures/home_player_status_profile_checks.cs',
                     'scripts/prepare_home_player_health_profile.py'):
        inputs[relative] = sha(ROOT/relative)
    for relative in ('unity/Assets/Game/Runtime/Offline/Progression/OfflinePlayerStatusProgress.cs',
                     'unity/Assets/Game/Runtime/Offline/HomePlayerHealthUI/OfflineHomePlayerStatuses.cs',
                     'unity/Assets/Game/Runtime/Offline/HomePlayerHealthUI/OfflineHomeStatusConfiguration.cs',
                     'unity/Assets/Game/Runtime/Offline/Progression/OfflineMainlineProgressSession.cs',
                     'unity/Assets/Game/Runtime/Offline/Progression/Zjcs.Offline.Progression.asmdef',
                     'unity/Assets/Game/Runtime/Offline/HomePlayerHealthUI/Zjcs.Offline.HomePlayerHealthUI.asmdef'):
        p = ROOT/relative
        for out in (p, metadata(p)): outputs[out.relative_to(ROOT).as_posix()] = sha(out)
    proof = dict(manifest='884_156692', generator_sha256=sha(Path(__file__)), inputs=inputs, outputs=outputs,
                 configuration=provenance, scope='Original shared record/reset/count/occupancy rules and selected complete status/scope/entry configuration. Offline profile transactions and original panel adapters. No invented expiry, cooking consumption, activation counters or initial gains; those require their owning producers.')
    (ROOT/'assets/reference/home-player-status-source-manifest.json').write_text(json.dumps(proof, ensure_ascii=False, indent=2)+'\n', 'utf8', newline='\n')
    print('Recovered selected status records:', {key: len(value) for key, value in tables.items()}, flush=True)
    return proof


def stage(project, proof):
    project = require_temporary_unity_project(project)
    paths = list(proof['outputs'])
    for relative in ('unity/Assets/Game/Runtime/Offline/Progression/OfflineMainlineProgressSession.cs',
                     'unity/Assets/Game/Runtime/Offline/Progression/Zjcs.Offline.Progression.asmdef',
                     'unity/Assets/Game/Runtime/Offline/HomePlayerHealthUI/Zjcs.Offline.HomePlayerHealthUI.asmdef'):
        paths.append(relative)
    for relative in paths:
        source = ROOT/relative
        if relative in proof['outputs'] and sha(source) != proof['outputs'][relative]:
            raise ValueError('Changed status output: '+relative)
        target = project/Path(relative).relative_to('unity')
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(source, target)
    print('Staged original status records in', project, flush=True)


def stage_validation(project):
    from prepare_home_player_health_profile import build as health_build, stage as health_stage
    project = require_temporary_unity_project(project)
    health_stage(project, health_build())
    folder = project/'Assets/HomePlayerHealthProfileValidation'
    path = folder/'HomePlayerHealthProfileValidation.cs'
    text = path.read_text('utf-8-sig')
    text = text.replace('HomePlayerHealthProfile', 'HomePlayerStatusProfile')
    text = text.replace('AudioListener.volume=0;', 'AudioListener.volume=0;Application.runInBackground=true;')
    text = text.replace('HOME_PLAYER_HEALTH_PROFILE_PASSED:', 'HOME_PLAYER_STATUS_PROFILE_PASSED: saved original status rules and native panel;')
    text = text.replace('Rating/map/battle/status owner inputs are controlled;',
                        'Rating/map/battle and entry-detail services are controlled; status records use the actual committed profile;')
    marker = '            var owner=new OwnerState();var games=new List<bool>();'
    assert text.count(marker) == 1
    text = text.replace(marker, '''            var statusConfig=OfflineHomeStatusConfiguration.LoadDefault();
            using var savedStatuses=new OfflineHomePlayerStatuses(profile,statusConfig,()=>experience.Now);
            var owner=new OwnerState(savedStatuses);var games=new List<bool>();''')
    marker = '            var hp=avatar.Root.GetComponentInChildren<MainPlayerHpImage>(true);'
    assert text.count(marker) == 1
    checks = (ROOT/'scripts/fixtures/home_player_status_profile_checks.cs').read_text('utf-8-sig')
    text = text.replace(marker, checks+'\n'+marker)
    text = text.replace('public IReadOnlyDictionary<int,IHomePanelStatusEffect> StatusEffects{get;}=new Dictionary<int,IHomePanelStatusEffect>();',
                        '''readonly OfflineHomePlayerStatuses savedStatuses;
        public OwnerState(OfflineHomePlayerStatuses savedStatuses){this.savedStatuses=savedStatuses;}
        public IReadOnlyDictionary<int,IHomePanelStatusEffect> StatusEffects=>savedStatuses.Statuses;''')
    text = text.replace('StatusEffectDataUpdate{add{status+=value;}remove{status-=value;}}',
                        'StatusEffectDataUpdate{add{status+=value;savedStatuses.StatusUpdated+=value;}remove{status-=value;savedStatuses.StatusUpdated-=value;}}')
    text = text.replace('// These scenarios are explicit fixture owner inputs, never a production\n        // empty-status or guessed no-blessing-rating implementation.',
                        '// Ratings and maps remain controlled; status records use the actual profile owner.')
    path.write_text(text, 'utf8', newline='\n')
    asm = folder/'HomePlayerHealthProfileValidation.asmdef'
    data = json.loads(asm.read_bytes())
    data['references'].append('Zjcs.Official.HomePlayerStatusRules')
    asm.write_text(json.dumps(data, indent=2)+'\n', 'utf8', newline='\n')
    print('Staged committed-status/native-panel validation in', project, flush=True)


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--project', type=Path)
    parser.add_argument('--validation', action='store_true')
    args = parser.parse_args()
    proof = build()
    if args.project:
        if args.validation: stage_validation(args.project)
        stage(args.project, proof)
