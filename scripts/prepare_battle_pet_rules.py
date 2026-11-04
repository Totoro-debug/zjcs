"""Supply the existing battle pet assembler with its pinned original tables/properties."""
import hashlib
import json
from pathlib import Path
import re

from official_apk import Apk, ROOT
from official_tables import Constructors
from prepare_battle_pet_hud import identity, write
from prepare_unity_character_catalog import decode_level_props


def build():
    rules_path = ROOT / 'unity/Assets/Official/Combat/rules.json'
    source_path = rules_path.with_name('rules-source-manifest.json')
    if identity(rules_path)['sha256'] != json.loads(source_path.read_bytes())['output_sha256']:
        raise ValueError('Installed combat rules differ from their source')
    rules = json.loads(rules_path.read_bytes())
    character_source = ROOT / 'unity/Assets/Official/Character/source-manifest.json'
    # Use the recorded historical binary, matching the installed battle rules.
    raw_path = ROOT / '.tools/source-packages/history/854_156383/WwiseBnk/wwisebnk_assets_config_binary_level_prop_g_bytes.rawfile'
    expected = '1b8e2cf7029ba8a80750f54aa35658878166d1bfbe2e206317473b43f6af3f1f'
    if not raw_path.exists():
        raw_path = ROOT / '.tools/source-packages/WwiseBnk/wwisebnk_assets_config_binary_level_prop_g_bytes.rawfile'
    raw = raw_path.read_bytes()
    if hashlib.sha256(raw).hexdigest() != expected or expected not in character_source.read_text(encoding='utf8'):
        raise ValueError('Historical pet property binary differs from the recorded input')
    wanted = {row['EntityExtraPropId'] for row in rules['pet'] if row['EntityExtraPropId'] > 0}
    wanted.update(row['PropId'] for row in rules['pet_ability_prop'] if row['PropId'] > 0)
    tables = {name: rules[name] for name in ('pet', 'pet_evolution', 'pet_ability_prop',
              'pet_ability_prop_scale_factor', 'skill', 'skill_rank', 'item')}
    skill_ids = {row['EntitySkill'] for row in rules['pet_evolution'] if row['EntitySkill'] > 0}
    for row in rules['pet_evolution']:
        for key in ('SupportSkills', 'EntityActiveSkills', 'EntityPassiveSkills', 'DeactiveEntityPassiveSkills'):
            skill_ids.update(row[key] or [])
    for table in ('skill', 'item'):
        tables[table] = [row for row in rules[table] if row['ClassId'] in skill_ids]
    properties = decode_level_props(raw, wanted)
    # The installed rules keep their historical identity. Independently decode
    # the selected APK before combining them with historical property rows.
    apk = Apk.current()
    parser = Constructors(ROOT / '.tools/source-packages/current' / apk.version / 'official-code')
    mapping = {name: cls for cls, name in re.findall(r'new (Tb\w+)\(loader\("([^"]+)"\)\)', parser.source('Tables'))}
    comparisons = []
    fields = {
        'pet': ('ClassId', 'AbilityId', 'EntityExtraPropId'),
        'pet_evolution': ('ClassId', 'EvolutionPhase', 'EntitySkill', 'FightEntityClassId', 'EntityActiveSkills', 'EntityPassiveSkills', 'DeactiveEntityPassiveSkills', 'SupportSkills'),
        'pet_ability_prop': ('ClassId', 'NodeId', 'NodeLevel', 'PropId', 'PropScaleFactorId'),
        'pet_ability_prop_scale_factor': ('ClassId', 'ScaleFactors'),
        'skill': ('ClassId', 'InitEnergy', 'TriggerNeedEnergy'),
        'item': ('ClassId', 'Quality'),
        'skill_rank': ('Rank', 'Quality')}
    for table, keys in fields.items():
        original = apk.asset('config_luban', '/' + table + '.g.bytes')
        payload = original.read().m_Script.encode('utf8', 'surrogateescape')
        active = parser.table(payload, mapping[table])
        # Luban maps have integer keys; JSON maps have string keys. Canonicalize
        # that serialization distinction before comparing the actual values.
        def canonical(value):
            return json.dumps(json.loads(json.dumps(value)), sort_keys=True)
        active_rows = {tuple(canonical(row[key]) for key in keys) for row in active}
        if any(tuple(canonical(row[key]) for key in keys) not in active_rows for row in tables[table]):
            raise ValueError('Consumed battle pet fields changed across recorded sources: ' + table)
        comparisons.append(dict(table=table, source=apk.provenance(original), payload_sha256=hashlib.sha256(payload).hexdigest()))
    record, = [row for row in apk.source.manifest('WwiseBnk', apk.archive)['bundles']
               if row['name'] == 'wwisebnk_assets_config_binary_level_prop_g_bytes.rawfile']
    entry = 'assets/yoo/WwiseBnk/' + record['file']
    if entry in apk.available:
        active_raw = apk.archive.read(entry)
        origin = dict(origin='apk_zip', apk_entry=entry, apk_manifest=apk.source.apk_version, apk_sha256=apk.source.apk_sha256)
    else:
        active_raw, origin = apk.cloud.read('WwiseBnk', record) or (None, None)
    if active_raw is None or len(active_raw) != record['size'] or hashlib.md5(active_raw).hexdigest() != record['hash']:
        raise ValueError('Selected official pet property binary is unavailable or differs')
    if decode_level_props(active_raw, wanted) != properties:
        raise ValueError('Pet property rows changed across recorded sources')
    output = ROOT / 'unity/Assets/Game/Resources/Combat/pet-battle-rules.json'
    write(output, dict(manifest='854_156383', tables=tables, level_prop=properties))
    output.write_bytes((json.dumps(json.loads(output.read_bytes()), ensure_ascii=False, separators=(',', ':'))+'\n').encode())
    write(output.with_name('pet-battle-rules-source-manifest.json'),
          dict(manifest='854_156383', output_sha256=identity(output)['sha256']))
    receipt = ROOT / 'assets/reference/battle-rules/pet-assembly-source-manifest.json'
    receipt.write_bytes((json.dumps(dict(manifest='854_156383', generator=identity(Path(__file__).resolve()),
        inputs=[identity(rules_path), identity(source_path), identity(raw_path), identity(character_source)],
        active_comparison=dict(manifest=apk.version, tables=comparisons,
            properties=dict(record=record, source=origin, payload_sha256=hashlib.sha256(active_raw).hexdigest(), groups=sorted(wanted))),
        outputs=[identity(output), identity(output.with_name('pet-battle-rules-source-manifest.json'))],
        runtime_sources=[identity(ROOT / path) for path in (
            'unity/Assets/Game/Runtime/Offline/Progression/OfficialCharacterCatalog.BattlePets.cs',
            'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattlePetEnergyAssembler.cs')],
        operation='Copy the existing battle pet tables and decode original pet entity/ability property groups; no formula changes.'),
        ensure_ascii=False, indent=2)+'\n').encode())
    print('Battle pet rules:', len(tables['pet_evolution']), 'evolutions;', len(wanted), 'property groups')


if __name__ == '__main__':
    build()
