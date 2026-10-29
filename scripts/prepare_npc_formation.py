"""Stage the cached original NPC matching table; optionally verify its APK bytes.

The default writes only below .tools and makes no runtime/resource-list changes.
The narrow decoder is checked against the original C# constructors by
generate_npc_formation_reference.py before this data is eligible for publishing.
"""
import argparse
import hashlib
import json
from pathlib import Path
from official_apk import ROOT, Apk, LubanReader

TABLE = 'game_match_npc'
SOURCE = ROOT / '.tools/source-packages/official-code/common-source/Common'


def decode(raw):
    reader = LubanReader(raw)
    result = []
    keys = set()
    for _ in range(reader.uint()):
        row = dict(TargetCatalog=reader.integer(), TargetId=reader.integer(),
                   NpcList=reader.collection('int'))
        key = row['TargetCatalog'], row['TargetId']
        if key in keys or row['NpcList'] is None:
            raise ValueError('Duplicate matching key or null original NPC list')
        keys.add(key)
        result.append(row)
    if reader.pos != len(raw):
        raise ValueError(f'Consumed {reader.pos} of {len(raw)} bytes')
    return result


def build(output, verify_apk=False):
    cached = ROOT / '.tools/source-packages/official-data' / (TABLE + '.g.bytes')
    raw = cached.read_bytes()
    source = dict(path=cached.relative_to(ROOT).as_posix(), bytes=len(raw),
                  sha256=hashlib.sha256(raw).hexdigest(), apk_verified_this_run=False)
    if verify_apk:
        apk = Apk()
        found = False
        for path, pointer in apk.container('config_luban'):
            if path.rsplit('/', 1)[-1] != TABLE + '.g.bytes':
                continue
            obj = pointer.deref()
            if obj.read().m_Script.encode('utf8', 'surrogateescape') != raw:
                raise ValueError('Matching table cache differs from pinned APK')
            source.update(apk.provenance(obj), asset=path, apk_verified_this_run=True)
            found = True
            break
        if not found:
            raise ValueError('Matching table not found in pinned APK')
    tables = dict(game_match_npc=decode(raw))
    dungeons_path = ROOT / 'assets/official/dungeons.json'
    dungeons = json.loads(dungeons_path.read_text('utf8'))
    for name in ['secret_area_phase', 'joint_strike_phase']:
        tables[name] = dungeons['tables'][name]
    output.mkdir(parents=True, exist_ok=True)
    target = output / 'npc-formation.json'
    target.write_text(json.dumps(dict(tables=tables), ensure_ascii=False, indent=2) + '\n', 'utf8')
    code = {name: hashlib.sha256((SOURCE / name).read_bytes()).hexdigest() for name in [
        'GameMatchNpcInfo.cs', 'TbGameMatchNpcInfo.cs', 'GameMatchNpcInfoParser.cs',
        'TeamTargetSecretArea.cs', 'SecretAreaPhaseInfoParser.cs', 'SecretAreaPhaseInfo.cs',
        'JointStrikePhaseInfoParser.cs', 'JointStrikePhaseInfo.cs', 'NpcTargetData.cs']}
    record = dict(manifest='854_156383', table=source, original_code_sha256=code,
                  dungeons_sha256=hashlib.sha256(dungeons_path.read_bytes()).hexdigest(),
                  output_sha256=hashlib.sha256(target.read_bytes()).hexdigest(),
                  status='staged_only', constructor_reference_verified=False)
    (output / 'source-manifest.json').write_text(json.dumps(record, ensure_ascii=False, indent=2) + '\n', 'utf8')
    return record


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', type=Path, default=ROOT / '.tools/gameplay-fixtures/combat/npc-formation-staged')
    parser.add_argument('--verify-apk', action='store_true')
    args = parser.parse_args()
    record = build(args.output, args.verify_apk)
    print('Staged original matching rows:', len(json.loads((args.output / 'npc-formation.json').read_text('utf8'))['tables']['game_match_npc']))
    print('APK verified this run:', record['table']['apk_verified_this_run'])


if __name__ == '__main__':
    main()
