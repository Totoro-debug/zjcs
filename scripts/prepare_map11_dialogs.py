"""Convert pinned forest battle-stele recommendations and original Chinese text."""
import argparse
import hashlib
import json
from pathlib import Path
from official_apk import ROOT
from npc_travel_language import language

STAGES = [900, 901, 902, 925, 926, 927]


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def build(destination,mid=11,stages=STAGES):
    source_path = ROOT / 'assets/reference/official-map/battle-stele-source-manifest.json'
    source = json.loads(source_path.read_text(encoding='utf8'))
    table_path = ROOT / 'assets/official/battle-stele.json'
    if source['version'] != '854_156383' or digest(table_path) != source['output']['sha256']:
        raise ValueError('Pinned battle-stele table differs from its original conversion receipt')
    table = json.loads(table_path.read_text(encoding='utf8'))
    keys = [f'stage_{stage}_{suffix}' for stage in stages for suffix in ['name', 'desc']]
    keys += ['ui_battle_stele_recommend_personal_combat_rating',
             'ui_battle_stele_current_combat_rating',
             'ui_battle_stele_recommend_personal_combat_rating_rank_suppression',
             'ui_award_preview_text', 'ui_challenge_text']
    strings, inputs = language(keys)
    receipt = json.loads((ROOT / 'assets/reference/official-map/localization-source-manifest.json').read_text(encoding='utf8'))
    for path, sha in inputs.items():
        if sha != receipt['inputs'][Path(path).name]['sha256']:
            raise ValueError('Pinned official language input differs: ' + path)
    if set(strings) != set(keys):raise ValueError('Original battle-stele strings are incomplete')
    payload = dict(strings=strings, recommend_index=table['recommend_index'],
                   steles=[row for row in table['rows'] if int(row['Id']) in stages])
    if {int(row['Id']) for row in payload['steles']}!=set(stages):raise ValueError('Original requested battle-stele rows are incomplete')
    relative = f'assets/official/map{mid}-dialogs.json'
    target = destination / relative
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_text(json.dumps(payload, ensure_ascii=False, indent=2) + '\n', encoding='utf8')
    record = dict(manifest='854_156383', table_source=source['tables']['stage_battle_stele'],
                  table_conversion=dict(path='assets/official/battle-stele.json', sha256=digest(table_path)),
                  localization_inputs={path:dict(sha256=sha, source=receipt['inputs'][Path(path).name]['source']) for path, sha in inputs.items()},
                  outputs={relative:digest(target)}, resources={},
                  operation='Select requested original stage_battle_stele rows; XXHash64 original zh_CN strings; existing official UI sprites reused, no drawing')
    receipt_path = destination / f'assets/reference/official-map/dialogs-{mid}-source-manifest.json'
    receipt_path.parent.mkdir(parents=True, exist_ok=True)
    receipt_path.write_text(json.dumps(record, ensure_ascii=False, indent=2) + '\n', encoding='utf8')
    print('Original map',mid,'dialogue catalog:', len(payload['steles']), 'steles,', len(strings), 'strings')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output-dir', type=Path, default=ROOT)
    build(parser.parse_args().output_dir.resolve())
