"""Recover the original configuration consumed by the native pet achievement hint."""
import json
from pathlib import Path

from official_apk import Apk, ROOT
from official_tables import Constructors
from prepare_current_home_furniture_stories import sha
from prepare_unity_map13_sources import meta
from validation_workspace import validation_directory


OUTPUT = ROOT / 'unity/Assets/Game/Resources/Home/pet-achievement-hints.json'
RECEIPT = ROOT / 'assets/reference/home-pet-achievement-hint-bindings-source-manifest.json'


def build(work):
    if not (work / '.zjcs-validation.json').is_file():
        raise ValueError('An owned validation workspace is required')
    apk = Apk.current()
    try:
        code = ROOT / '.tools/source-packages/current' / apk.version / 'official-code'
        constructors = Constructors(code)
        tables = []

        def table(name, constructor):
            obj = apk.asset('config_luban', '/' + name + '.g.bytes')
            raw = obj.read().m_Script.encode('utf8', 'surrogateescape')
            tables.append(dict(table=name, source=apk.provenance(obj),
                               object_sha256=sha(obj.get_raw_data()), payload_sha256=sha(raw)))
            return constructors.table(raw, constructor)

        achievements = table('achievement', 'TbAchievementRawInfo')
        groups = table('achievement_group', 'TbAchievementGroupInfo')
        settings = table('game_settings', 'TbGameSettings')
        if isinstance(settings, list):
            if len(settings) != 1:
                raise ValueError('Original game settings must be unique')
            settings = settings[0]
        grouped = []
        for group in groups:
            if group['AchievementGroupId'] > 0:
                grouped.append(group['AchievementGroupId'])
            else:
                grouped.extend(sorted({row['Group'] for row in achievements
                                       if row['AchievementType'] == group['AchievementGroupType']}))
        keys = []
        for kind in (65, 66):
            rows = [row for row in achievements if row['AchievementType'] == kind]
            values = {row['Group'] for row in rows}
            if len(values) != 1 or any(row['GroupParams'] for row in rows):
                raise ValueError('Pet hint requires another original achievement key adapter')
            keys.append(dict(type=kind, group=values.pop()))
        data = dict(keys=keys, groupedAchievementIds=grouped,
                    showAllTypes=settings['AchievementTypeListForShowAllInfosInSameType'])
        payload = (json.dumps(data, ensure_ascii=False, indent=2) + '\n').encode()
        if OUTPUT.exists() and (not RECEIPT.exists() or sha(OUTPUT.read_bytes()) !=
                                json.loads(RECEIPT.read_bytes())['output']['sha256']):
            raise ValueError('Existing pet achievement configuration differs from its receipt')
        OUTPUT.write_bytes(payload)
        meta(OUTPUT)
        sources = [Path(__file__), ROOT / 'scripts/official_tables.py', ROOT / 'scripts/official_apk.py',
                   ROOT / 'scripts/prepare_unity_map13_sources.py',
                   code / 'common-source/Common/AchievementGroupInfo.cs',
                   code / 'common-source/Common/AchievementGroupInfoParser.cs',
                   code / 'common-source/Common/AchievementDataKey.cs',
                   code / 'common-source/Common/PlayerUtils.cs',
                   code / 'common-source/Common/Player.cs',
                   code / 'client-source/GachaBlueDotBase.cs',
                   code / 'client-source/GachaBlueDotForSetupMinCount.cs',
                   code / 'client-source/AchievementRedDotItem.cs']
        proof = dict(manifest=apk.version, tables=tables,
                     inputs={p.relative_to(ROOT).as_posix(): sha(p.read_bytes()) for p in sources},
                     output=dict(path=OUTPUT.relative_to(ROOT).as_posix(), sha256=sha(payload),
                                 meta_sha256=sha(Path(str(OUTPUT) + '.meta').read_bytes())))
        RECEIPT.write_text(json.dumps(proof, ensure_ascii=False, indent=2) + '\n', encoding='utf8')
        print('Recovered native pet achievement keys and group visibility configuration')
    finally:
        apk.reader.close()


if __name__ == '__main__':
    build(validation_directory())
