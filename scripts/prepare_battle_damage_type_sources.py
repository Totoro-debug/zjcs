"""Verify current breaking settings and the official formula behind typed damage paths."""
import argparse
import hashlib
import json
from pathlib import Path

from official_apk import Apk, ROOT
from official_tables import Constructors


def identity(path):
    return dict(path=path.relative_to(ROOT).as_posix(),
                sha256=hashlib.sha256(path.read_bytes()).hexdigest())


def build():
    fields=('TrueDamageAddPerWhenHasShield','TrueDamageAddPerWhenIsSummon','TrueDamageCharacterTypes')
    installed=ROOT/'unity/Assets/Official/Combat/rules.json'
    legacy=json.loads(installed.read_bytes())['game_settings']['Battle']
    with Apk.current() as apk:
        code=ROOT/'.tools/source-packages/current'/apk.version/'official-code'
        parser=Constructors(code)
        asset=apk.asset('config_luban','/game_settings.g.bytes')
        payload=asset.read().m_Script.encode('utf8','surrogateescape')
        battle=parser.table(payload,'TbGameSettings')['Battle']
        selected={name:battle[name] for name in fields}
        assert selected=={name:legacy[name] for name in fields}, 'Migrate changed current breaking settings.'
        constructors=[]
        for name in sorted(parser.sources):
            path=code/'common-source/Common'/(name+'.cs')
            if not path.is_file():path=code/'common-source/Common.LubanGen'/(name+'.cs')
            constructors.append(identity(path))
        return dict(manifest=apk.version,settings=selected,settings_source=apk.provenance(asset),
            payload_sha256=hashlib.sha256(payload).hexdigest(),constructors=constructors,
            original_formula=identity(code/'common-source/Common/BattleFormulaHandler.cs'),
            original_settings=identity(code/'common-source/Common/BattleSettings.cs'),
            installed_settings=identity(installed),
            runtime=[identity(ROOT/'unity/Assets/Game/Runtime'/name) for name in (
                'Official/Combat/FightDamageFormula.cs','Official/Combat/FightDamageFormula.DamageTypes.cs',
                'Official/Combat/FightDamageFormula.TrueDamage.cs','Official/Combat/FightOfficialCombatCatalog.cs',
                'Official/Combat/FightBattleHitExecutor.cs','Offline/Combat/FightBattleHitExecutor.DamageTypes.cs')],
            tools=[identity(Path(__file__)),identity(ROOT/'scripts/official_tables.py')],
            operation='Decode the selected current settings with their generated constructors; compare the migrated fields without relabelling historical rules. '
                'Bind the original breaking shield formula and source settings to the shared physical, element, fixed true and breaking functions. '
                'The local common-response/type-effect composition is separate from the original formula input.')


def main():
    parser=argparse.ArgumentParser(__doc__);parser.add_argument('--write',action='store_true');args=parser.parse_args()
    result=build();path=ROOT/'assets/reference/battle-rules/damage-type-sources-source-manifest.json'
    if args.write:path.write_bytes((json.dumps(result,ensure_ascii=False,indent=2)+'\n').encode())
    else:assert json.loads(path.read_bytes())==result,'Damage type source binding changed.'
    print('Current official damage-type inputs verified:',result['manifest'],result['settings'])


if __name__=='__main__':main()
