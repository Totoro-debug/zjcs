"""Recover the selected mainline TaskInfo rows and their original dynamic text.

This supplements the existing transaction catalog without relabeling its older
source. It does not generate task instances or change the offline claim policy.
"""
import hashlib
import json
from pathlib import Path
import re
import struct

from official_apk import Apk, ROOT
from official_tables import Constructors
from prepare_unity_mainline_tasks import condition, write_meta
import xxhash


OUTPUT = ROOT / 'unity/Assets/Game/Resources/Home/home-mainline-task-data.json'
RECEIPT = ROOT / 'assets/reference/home-mainline-task-data-source-manifest.json'
HISTORICAL = ROOT / 'unity/Assets/Game/Resources/Official/Progression/mainline-tasks.json'


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def text_meta(path):
    write_meta(path, 'text')
    meta = Path(str(path) + '.meta')
    meta.write_text('\n'.join(line.rstrip() for line in meta.read_text('utf8').splitlines())
        + '\n', 'utf8', newline='\n')


def projection(row):
    return dict(classId=row['ClassId'], order=row['Order'], triggerType=row['TriggerType'],
        acquestCondition=condition(row['AcquestCondition']),
        displayCondition=condition(row['DisplayCondition']),
        finishCondition=condition(row['FinishCondition']),
        rewards=[dict(itemId=int(key), count=value) for key, value in (row['Reward'] or {}).items()],
        npcFriendshipRewards=[dict(npcClassId=int(key), amount=value)
            for key, value in (row['NpcFriendshipReward'] or {}).items()],
        rewardRuleIds=row['RewardRuleId'] or [],
        rewardRuleIdsWithProbability=row['RewardRuleIdWithProbability'] or [])


def read_texts(raw, keys):
    # The existing official-language converter's xxHash64 index/UTF-8 layout.
    count, = struct.unpack_from('<i', raw)
    start = 4 + count * 12
    if count < 0 or start > len(raw):
        raise ValueError('Invalid original language index')
    wanted = {xxhash.xxh64_intdigest(key.encode()): key for key in keys}
    if len(wanted) != len(keys):
        raise ValueError('Language key hash collision')
    result = {}
    for index in range(count):
        key, offset = struct.unpack_from('<Qi', raw, 4 + index * 12)
        if key not in wanted:
            continue
        pos = start + offset
        if offset < 0 or pos + 2 > len(raw):
            raise ValueError('Invalid original language offset')
        size, = struct.unpack_from('<H', raw, pos)
        if pos + 2 + size > len(raw):
            raise ValueError('Truncated original language entry')
        result[wanted[key]] = raw[pos + 2:pos + 2 + size].decode('utf8')
    return result


def build():
    inputs = {}

    def read(path):
        raw = path.read_bytes()
        inputs[path.relative_to(ROOT).as_posix()] = sha(raw)
        return raw

    with Apk.current() as apk:
        if apk.version != '884_156692':
            raise ValueError('Review mainline UI fields for the newly selected version')
        code = ROOT / '.tools/source-packages/current' / apk.version / 'official-code'
        constructors = Constructors(code)
        mapping = {name.rsplit('/', 1)[-1]: kind for kind, name in re.findall(
            r'new (Tb\w+)\(loader\("([^"]+)"\)\)', constructors.source('Tables'))}
        wanted = {'task', 'item', 'game_settings'}
        tables, proofs = {}, {}
        for obj in apk.objects('defaultpackage_assets_config_luban.bundle'):
            if obj.type.name != 'TextAsset':
                continue
            name = obj.read().m_Name.rsplit('/', 1)[-1].removesuffix('.g')
            if name not in wanted:
                continue
            raw = obj.read().m_Script.encode('utf8', 'surrogateescape')
            if name in tables:
                raise ValueError('Ambiguous original task table: ' + name)
            tables[name] = constructors.table(raw, mapping[name])
            proofs[name] = dict(source=apk.provenance(obj), object_sha256=sha(obj.get_raw_data()),
                payload_sha256=sha(raw), constructor=mapping[name])
        if tables.keys() != wanted:
            raise ValueError('Missing original task tables: ' + repr(wanted - tables.keys()))

        historical = json.loads(read(HISTORICAL))
        historical_rows = {row['classId']: row for row in historical['tasks']}
        selected = {row['ClassId']: row for row in tables['task']}
        rows = [selected[key] for key in historical_rows]
        # Exact comparison of every field consumed by the current transaction
        # service, including condition subfields, reward order and random rules.
        for row in rows:
            expected = dict(historical_rows[row['ClassId']])
            expected.setdefault('npcFriendshipRewards', [])
            if projection(row) != expected:
                raise ValueError('Current mainline transaction differs: ' + str(row['ClassId']))
        if len(rows) != 55:
            raise ValueError('Review the supported offline task scope')
        rows.sort(key=lambda row: (row['Order'], row['ClassId']))
        reward_ids = {int(key) for row in rows for key in (row['Reward'] or {})}
        items = [row for row in tables['item'] if row['ClassId'] in reward_ids]
        if {row['ClassId'] for row in items} != reward_ids:
            raise ValueError('Original task reward item is missing')

        parser = read(code / 'common-source/Common/TaskInfoParser.cs').decode('utf-8-sig')
        if 'value.FinishCondition.SetDescKey(taskProgressKey);' not in parser:
            raise ValueError('Review original task description post-parse')
        read(code / 'common-source/Common/LanguageUtils.cs')
        keys = {f'task_{row["ClassId"]}_{suffix}' for row in rows for suffix in ('title', 'progress')}
        keys.update(f'item_{key}_name' for key in reward_ids)
        keys.update(f'region_{c["MapClassId"]}_{c["Region"]}' for row in rows
            if (c := row['FinishCondition'])['_type'] == 'WorldMapProgressCondition')
        texts, language_proofs = {}, {}
        for name in ('wwisebnk_assets_launch_config_binary_language_zh_cn_text_g_bytes.rawfile',
                     'wwisebnk_assets_config_binary_language_zh_cn_text_g_bytes.rawfile'):
            verified = apk.reader.read('WwiseBnk', name)
            texts.update(read_texts(verified.data, keys))
            language_proofs[name] = dict(source=dict(verified.provenance), payload_sha256=sha(verified.data))
        if keys - texts.keys():
            raise ValueError('Original task text is missing: ' + repr(sorted(keys - texts.keys())))

        for name in constructors.sources:
            path = code / 'common-source/Common' / (name + '.cs')
            if not path.is_file():
                path = code / 'common-source/Common.LubanGen' / (name + '.cs')
            read(path)
        for path in (code / 'global-metadata.dat', ROOT / 'scripts/official_tables.py',
                     ROOT / 'scripts/official_apk.py', ROOT / 'scripts/official_bundle_reader.py',
                     ROOT / 'scripts/npc_travel_language.py', ROOT / 'scripts/prepare_unity_mainline_tasks.py'):
            read(path)
        data = dict(manifest=apk.version, transactionCatalogSha256=sha(read(HISTORICAL)), tasks=rows, items=items,
            taskSettings=tables['game_settings']['TaskSettings'],
            texts=[dict(key=key, text=value) for key, value in sorted(texts.items())])
        raw = (json.dumps(data, ensure_ascii=False, indent=2) + '\n').encode('utf8')
        OUTPUT.write_bytes(raw)
        text_meta(OUTPUT)
        checksum = OUTPUT.with_name('home-mainline-task-data-hash.json')
        checksum.write_text(json.dumps(dict(manifest=apk.version, output_sha256=sha(raw)), indent=2)
            + '\n', 'utf8', newline='\n')
        text_meta(checksum)
        receipt = dict(manifest=apk.version, generator_sha256=sha(Path(__file__).read_bytes()),
            inputs=inputs, tables=proofs, language=language_proofs,
            original_task_count=len(tables['task']), supported_task_ids=[row['ClassId'] for row in rows],
            verified_transaction_fields=list(projection(rows[0])),
            outputs={path.relative_to(ROOT).as_posix(): sha(path.read_bytes())
                for path in (OUTPUT, Path(str(OUTPUT) + '.meta'), checksum, Path(str(checksum) + '.meta'))},
            scope='Complete current TaskInfo fields for the 55 supported offline mainline tasks, '
                'their seven reward item definitions, original TaskSettings and dynamic CN text. '
                'No generated server task instances; existing transaction payload retains its historical source version.')
        RECEIPT.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
        print('Current mainline UI data:', len(rows), 'tasks;', len(items), 'reward items;', len(texts), 'texts', flush=True)
        return receipt


if __name__ == '__main__':
    build()
