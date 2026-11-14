"""Prepare independent selected-property vectors and a background C# runner."""
import json
from pathlib import Path
import hashlib
import argparse
import shutil

from official_apk import ROOT
from prepare_unity_character_catalog import decode_level_props
from validation_workspace import validation_directory


def prepare(work):
    configuration = ROOT/'unity/Assets/Game/Resources/Home/level-up-properties.json'
    source = json.loads(configuration.read_bytes()); tables = source['tables']; settings = tables['game_settings']
    binary = ROOT/'unity/Assets/StreamingAssets/PartnerNpcShared/Config/Binary/level_prop.g.bytes'
    assert hashlib.sha256(binary.read_bytes()).hexdigest() == source['level_prop_sha256']
    ids = {settings['PlayerLevelBattlePropId'], settings['PlayerSubRankBattlePropId']}
    ids.update(row['LevelPropId'] for row in tables['profession_base'] if row['LevelPropId'])
    ids.update(row[key] for row in tables['role_prop_group'] for key in ('LevelPropID','ExtraLevelPropID') if row[key])
    groups = decode_level_props(binary.read_bytes(), ids)
    defaults = {row['ClassId']: row for row in tables['level_prop_default_value']}
    def props(identity, level):
        group = groups.get(str(identity))
        if group is None: return None
        row = group['rows'].get(str(level if level <= group['max'] else group['max']))
        if row is None: return None
        result = dict(row)
        default = defaults.get(identity)
        if default:
            for key in default['PropTypes']: result.setdefault(str(key), default['DefaultValue'])
        return result
    level_rows = []
    for identity, group in groups.items():
        for level in [*map(int, group['rows']), group['max']+1, 2147483647]:
            level_rows.append(dict(id=int(identity), level=level, expected=props(int(identity), level)))
    professions = {row['Profession']: row for row in tables['profession_base']}
    formulas = []
    for profession, base in professions.items():
        unlocked = None if profession == 0 else {1:5, 10:7, profession:2}
        for role in tables['role_prop_group']:
            for level in (1,2,10,100,2147483647):
                first = props(settings['PlayerLevelBattlePropId'], level)
                components = [] if first is None else [first, props(settings['PlayerSubRankBattlePropId'],role['SubRank'])]
                components += [props(role['LevelPropID'],level)]
                if role['ExtraLevelPropID']: components.append(props(role['ExtraLevelPropID'],level))
                if unlocked: components += [props(professions[key]['LevelPropId'],value) for key,value in unlocked.items()]
                if profession: components.append(base['FeatureProps'])
                total = {}
                for component in components:
                    for key,value in (component or {}).items(): total[key] = total.get(key,0)+value
                formulas.append(dict(profession=profession,professions=unlocked,subrank=role['SubRank'],level=level,expected=total))
    path = work/'properties-reference.json'; path.write_text(json.dumps(dict(level_rows=level_rows,formulas=formulas),separators=(',',':'))+'\n','utf8',newline='\n')
    # All compiler outputs stay in this session. PowerShell's Roslyn compiles the
    # exact migrated source, without loading a Unity process or changing focus.
    files = [*sorted((ROOT/'unity/Assets/Game/Runtime/Official/HomeLevelUpRules').glob('*.cs')),
        ROOT/'unity/Assets/Game/Runtime/Official/HomeIncomeRules/PropType.cs',
        ROOT/'unity/Assets/Game/Runtime/Official/HomeExperienceRules/SubRank.cs',
        ROOT/'scripts/fixtures/home_level_up_properties_checks.cs']
    quote = lambda value: "'"+str(value).replace("'","''")+"'"
    script = "$ErrorActionPreference='Stop'\nAdd-Type -IgnoreWarnings -Path @("+','.join(map(quote,files))+') -ReferencedAssemblies @(Get-ChildItem -LiteralPath "$PSHOME/ref" -Filter *.dll | Select-Object -ExpandProperty FullName)\n'
    script += '[HomeLevelUpPropertiesChecks]::Run('+','.join(map(quote,(configuration,binary,path)))+')\n'
    (work/'properties-checks.ps1').write_text(script,'utf8',newline='\n')
    print('Independent selected vectors:',len(level_rows),'binary rows and',len(formulas),'formula combinations',flush=True)
    return path


def prepare_native(work):
    from prepare_home_level_up_validation import prepare as prepare_notification
    from validation_assemblies import stage_runtime_modules
    project = prepare_notification(work)
    stage_runtime_modules(project, ROOT/'unity/Assets/Game/Runtime', [
        'Zjcs.Offline.HomeLevelUpUI', 'Zjcs.Official.HomeLevelUpUI', 'Zjcs.Offline.ProfessionUI',
        'Zjcs.Offline.PartnersUI', 'Zjcs.Official.World.NativeText', 'Coffee.UIParticle'])
    for relative in ('Assets/Game/Resources/Home/level-up-properties.json',
                     'Assets/StreamingAssets/PartnerNpcShared/Config/Binary/level_prop.g.bytes'):
        source = ROOT/'unity'/relative; target = project/relative
        target.parent.mkdir(parents=True, exist_ok=True); shutil.copy2(source, target)
        shutil.copy2(Path(str(source)+'.meta'), Path(str(target)+'.meta'))
    path = project/'Assets/Editor/HomeLevelUpNativeValidation.cs'
    text = path.read_text('utf8').replace('HomeLevelUpNative', 'HomeLevelUpPropertiesNative')
    text = text.replace('HOME_LEVEL_UP_NATIVE_PASSED:', 'HOME_LEVEL_UP_PROPERTIES_NATIVE_PASSED:')
    text = text.replace('Formula/player endpoints are controlled fixture data.', 'Selected original formula and shared binary; player/window owners are fixture inputs.')
    text = text.replace('using Zjcs.Official.HomeLevelUpUI;', 'using Zjcs.Official.HomeLevelUpUI;\nusing Zjcs.Offline.HomeLevelUpUI;\nusing Zjcs.Offline.Progression;')
    text = text.replace('    public int CurrentLevel => 2;', '    OfflineHomeLevelUpProperties calculator;\n    public int CurrentLevel => 2;')
    start = text.index('    public Dictionary<ProfessionPropertyType, long> CalcCharacterBattleProps(')
    brace = text.index('{', start); end = brace+1; depth = 1
    while depth:
        depth += (text[end] == '{')-(text[end] == '}'); end += 1
    text = text[:brace]+'''{
        requestedLevels.Add(level);
        var value = calculator.Evaluate(level);
        // Keep the existing unchanged-row branch test as an explicit override.
        if (unchanged && level == 2) value[(ProfessionPropertyType)52] = calculator.Evaluate(1)[(ProfessionPropertyType)52];
        return value;
    }'''+text[end:]
    marker = '        Require(!LevelUpNotifyTip.TryPopup(this, 2)'
    assert text.count(marker) == 1
    text = text.replace(marker, '''        var player = new OfflinePlayerProgress { Level = 2, SubRank = 1 };
        player.Wardrobe.Profession = 1;
        player.ProfessionLevels.Add(new OfflineProfessionLevel { Profession = 1, Level = 1 });
        calculator = OfflineHomeLevelUpProperties.LoadDefault(() => player);
        string serialized = JsonUtility.ToJson(player);
        var expected = calculator.Evaluate(2);
        Require(JsonUtility.ToJson(player) == serialized, "notification calculation never mutates saved model");
        string saved = Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"), "level-up-player.json");
        File.WriteAllText(saved, serialized);
        player = JsonUtility.FromJson<OfflinePlayerProgress>(File.ReadAllText(saved));
        Require(calculator.Evaluate(2).OrderBy(row=>row.Key).SequenceEqual(expected.OrderBy(row=>row.Key)), "owner rereads reloaded player without stale model capture");
'''+marker)
    text = text.replace('rows.Count(row => row.gameObject.activeSelf) == 3, "equal speed hides its original row"',
                        'rows.Count(row => row.gameObject.activeSelf) == 4, "all four original base properties change in selected level 1 to 2"')
    text = text.replace('rows.Count(row => row.gameObject.activeSelf) == 2, "second equal property hides independently"',
                        'rows.Count(row => row.gameObject.activeSelf) == 3, "controlled unchanged defence hides independently of real formula"')
    text = text.replace('new WaitForSeconds(.6f); Capture(', 'new WaitForSeconds(.9f); Capture(')
    text = text.replace('        fonts.Dispose();', '        calculator.Dispose();\n        fonts.Dispose();')
    path.write_text(text,'utf8',newline='\n')
    path = project/'Assets/Editor/Validation.asmdef'
    definition = json.loads(path.read_bytes())
    definition['references'] += ['Zjcs.Offline.HomeLevelUpUI','Zjcs.Offline.Progression']
    path.write_text(json.dumps(definition,indent=2)+'\n','utf8',newline='\n')
    print('Staged selected formula in complete original level-up notification:', project,flush=True)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(__doc__); parser.add_argument('--native',action='store_true'); args = parser.parse_args()
    (prepare_native if args.native else prepare)(validation_directory())
