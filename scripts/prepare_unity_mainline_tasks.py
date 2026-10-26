"""Build the pinned Unity mainline task catalog from verified official rules."""
import hashlib
import json
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
RULES = ROOT / 'assets/official/rules.json'
RULES_MANIFEST = ROOT / 'assets/reference/official-city/rules-manifest.json'
SOURCE_MANIFEST = ROOT / 'assets/reference/official-city/source-manifest.json'
OUTPUT_DIR = ROOT / 'unity/Assets/Game/Resources/Official/Progression'
OUTPUT_NAME = 'mainline-tasks.json'
MANIFEST_NAME = 'mainline-tasks-source-manifest.json'
EXPECTED_MANIFEST = '854_156383'
SUPPORTED_TASK_IDS = tuple(range(1000, 1271, 5))
MOUNTAIN_SOURCE = ROOT / 'unity/Assets/Official/World/Map/12/Source/tables.json'
MOUNTAIN_MANIFEST = ROOT / 'assets/reference/official-map/map12-source-manifest.json'
SUPPORTED_CONDITIONS = {
    'NoneCondition', 'AlwaysCondition', 'TaskCompleteCondition', 'TaskSubmitCondition',
    'MapGameTotalCountCondition', 'WorldMapProgressCondition',
    'MapLogicEntityEndByTagCondition', 'MapRegionGoddessCondition',
    'MaxProfessionRankCondition', 'TotalOpenChestCondition', 'DemonBattleLayerPassCondition',
}


def digest(data):
    return hashlib.sha256(data).hexdigest()


def write_meta(path, importer):
    relative = path.relative_to(ROOT / 'unity').as_posix()
    guid = hashlib.md5(('zjcs-official:' + relative).encode('utf-8')).hexdigest()
    if importer == 'folderAsset':
        content = (f"fileFormatVersion: 2\nguid: {guid}\nfolderAsset: yes\n"
                   "DefaultImporter:\n  externalObjects: {}\n  userData: \n"
                   "  assetBundleName: \n  assetBundleVariant: \n")
    else:
        content = (f"fileFormatVersion: 2\nguid: {guid}\n"
                   "TextScriptImporter:\n  externalObjects: {}\n  userData: \n"
                   "  assetBundleName: \n  assetBundleVariant: \n")
    meta = path.with_name(path.name + '.meta')
    if meta.exists():
        return
    meta.write_text(content, encoding='utf-8', newline='\n')


def condition(value):
    if value is None:
        return None
    fields = {
        '_type': 'type', 'MapClassId': 'mapClassId', 'Region': 'region',
        'Count': 'count', 'Result': 'result', 'Progress': 'progress',
        'TaskClassIds': 'taskClassIds', 'Tag': 'tag', 'Rank': 'rank',
        'ChestId': 'chestId', 'Ids': 'ids', 'EntryIndex': 'entryIndex',
        'TargetLayer': 'targetLayer',
    }
    return {target: value[source] for source, target in fields.items() if source in value}


def build():
    rules_bytes = RULES.read_bytes()
    rules_manifest = json.loads(RULES_MANIFEST.read_text(encoding='utf-8'))
    source_manifest = json.loads(SOURCE_MANIFEST.read_text(encoding='utf-8'))
    rules_hash = digest(rules_bytes)
    if rules_hash != rules_manifest.get('rules_sha256'):
        raise ValueError('Official rules.json differs from its generation manifest')
    if rules_hash != source_manifest.get('data_sha256', {}).get('rules.json'):
        raise ValueError('Official rules.json differs from the module source manifest')
    task_source = rules_manifest.get('tables', {}).get('task')
    if not task_source or task_source.get('constructor') != 'Console.dll/Common/TbTaskInfo.cs':
        raise ValueError('Official TaskInfo source record is absent or unexpected')
    friendship_source = rules_manifest.get('tables', {}).get('npc_friendship_level')
    if not friendship_source or friendship_source.get('constructor') != 'Console.dll/Common/TbNpcFriendshipLevelInfo.cs':
        raise ValueError('Official NPC friendship level source record is absent or unexpected')

    rules = json.loads(rules_bytes)
    all_task_rows = {int(row['ClassId']): row for row in rules.get('task', [])}
    # Reuse historical conversion only after exact comparison with the selected
    # mountain source. No current-version row is silently replaced by an old row.
    mountain_bytes = MOUNTAIN_SOURCE.read_bytes()
    mountain = json.loads(mountain_bytes)
    current_tasks = {int(row['ClassId']): row for row in mountain['task']}
    for task_id in range(1165, 1271, 5):
        if current_tasks[task_id] != all_task_rows[task_id]:
            raise ValueError(f'Selected mountain TaskInfo differs from historical conversion: {task_id}')
    task_rows = [all_task_rows[task_id] for task_id in SUPPORTED_TASK_IDS if task_id in all_task_rows]
    if len(task_rows) != len(SUPPORTED_TASK_IDS):
        missing = sorted(set(SUPPORTED_TASK_IDS) - set(all_task_rows))
        raise ValueError('Pinned locally supported mainline TaskInfo rows are missing: ' + str(missing))
    for row in task_rows:
        task_id = int(row['ClassId'])
        if int(row.get('TaskType', -1)) != 10 or int(row.get('TriggerType', -1)) != 1:
            raise ValueError(f'Unexpected task type or trigger on supported mainline task {task_id}')
        for field in ('AcquestCondition', 'DisplayCondition', 'FinishCondition'):
            value = row.get(field)
            if value is not None and value.get('_type') not in SUPPORTED_CONDITIONS:
                raise ValueError(f'Unsupported {field} on locally supported task {task_id}: {value.get("_type")}')
            if value and value.get('_type') in ('TaskCompleteCondition', 'TaskSubmitCondition'):
                references = value.get('TaskClassIds') or []
                if not set(map(int, references)).issubset(SUPPORTED_TASK_IDS):
                    raise ValueError(f'Unsupported task dependency on {task_id}: {references}')
        if row.get('RewardRuleId') or row.get('RewardRuleIdWithProbability'):
            raise ValueError(f'Randomized task rewards are outside this local mainline slice: {task_id}')
    task_rows.sort(key=lambda row: (int(row['Order']), int(row['ClassId'])))
    items = {int(row['ClassId']) for row in rules.get('item', [])}
    npcs = {int(row['Id']) for row in rules.get('npc', [])}
    friendship_levels = sorted((
        {'npcClassId': int(row['Id']), 'level': int(row['Level']),
         'friendshipExp': int(row['FriendshipExp']), 'subRankLimit': int(row['SubRankLimit'])}
        for row in rules.get('npc_friendship_level', [])
    ), key=lambda row: (row['npcClassId'], row['level']))
    if len(friendship_levels) != 10000:
        raise ValueError('Pinned NPC friendship level table does not contain 10,000 rows')

    tasks = []
    used_items = set()
    for row in task_rows:
        reward = row.get('Reward') or {}
        entries = []
        for item_id, count in reward.items():
            item_id, count = int(item_id), int(count)
            if item_id not in items or count < 0:
                raise ValueError(f'Invalid official mainline reward on task {row["ClassId"]}: {item_id}')
            used_items.add(item_id)
            entries.append({'itemId': item_id, 'count': count})
        npc_friendship_rewards = []
        for npc_id, amount in sorted((row.get('NpcFriendshipReward') or {}).items(),
                                     key=lambda entry: int(entry[0])):
            npc_id, amount = int(npc_id), int(amount)
            if npc_id not in npcs or amount < 0:
                raise ValueError(f'Invalid NPC friendship reward on task {row["ClassId"]}: {npc_id}')
            npc_friendship_rewards.append({'npcClassId': npc_id, 'amount': amount})
        task = {
            'classId': int(row['ClassId']),
            'order': int(row['Order']),
            'triggerType': int(row['TriggerType']),
            'acquestCondition': condition(row.get('AcquestCondition')),
            'displayCondition': condition(row.get('DisplayCondition')),
            'finishCondition': condition(row.get('FinishCondition')),
            'rewards': entries,
            'rewardRuleIds': [int(value) for value in row.get('RewardRuleId') or []],
            'rewardRuleIdsWithProbability': [int(value) for value in row.get('RewardRuleIdWithProbability') or []],
        }
        if npc_friendship_rewards:
            task['npcFriendshipRewards'] = npc_friendship_rewards
        tasks.append(task)

    item_table = {int(row['ClassId']): row for row in rules.get('item', [])}
    settings = rules.get('game_settings', {})
    player_exp_item_id = int(settings.get('PlayerExpItemId', 0))
    if player_exp_item_id <= 0 or player_exp_item_id not in item_table:
        raise ValueError('Official PlayerExpItemId is missing from the item table')
    catalog = {
        'manifest': EXPECTED_MANIFEST,
        'playerExpItemId': player_exp_item_id,
        'tasks': tasks,
        'npcFriendshipLevels': friendship_levels,
        'rewardItemIds': sorted(used_items),
        'rewardItems': [{'classId': item_id, 'itemType': int(item_table[item_id].get('ItemType', 0))}
                        for item_id in sorted(used_items)],
    }
    payload = (json.dumps(catalog, ensure_ascii=False, indent=2, sort_keys=True) + '\n').encode('utf-8')
    manifest = {
        'manifest': EXPECTED_MANIFEST,
        'output_sha256': digest(payload),
        'source_rules_sha256': rules_hash,
        'source_rules_manifest_sha256': digest(RULES_MANIFEST.read_bytes()),
        'task_table': task_source,
        'npc_friendship_level_table': friendship_source,
        'mountain_current_source': {
            'path': MOUNTAIN_SOURCE.relative_to(ROOT).as_posix(),
            'sha256': digest(mountain_bytes),
            'manifest_path': MOUNTAIN_MANIFEST.relative_to(ROOT).as_posix(),
            'manifest_sha256': digest(MOUNTAIN_MANIFEST.read_bytes()),
            'verified_task_ids': list(range(1165, 1271, 5)),
        },
        'tasks': len(tasks),
        'supported_task_ids': list(SUPPORTED_TASK_IDS),
        'scope': 'Only fixed-version mainline targets whose condition kinds and direct rewards are handled by OfflineMainlineProgressSession.',
    }
    manifest_bytes = (json.dumps(manifest, ensure_ascii=False, indent=2, sort_keys=True) + '\n').encode('utf-8')

    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    for folder in [OUTPUT_DIR, OUTPUT_DIR.parent, OUTPUT_DIR.parent.parent]:
        write_meta(folder, 'folderAsset')
    output_path = OUTPUT_DIR / OUTPUT_NAME
    manifest_path = OUTPUT_DIR / MANIFEST_NAME
    previous_output_is_verified = False
    if output_path.exists() and manifest_path.exists():
        try:
            previous_manifest = json.loads(manifest_path.read_text(encoding='utf-8'))
            previous_output_is_verified = (
                previous_manifest.get('manifest') == EXPECTED_MANIFEST
                and previous_manifest.get('output_sha256') == digest(output_path.read_bytes())
            )
        except (OSError, json.JSONDecodeError):
            previous_output_is_verified = False

    for name, content in [(OUTPUT_NAME, payload), (MANIFEST_NAME, manifest_bytes)]:
        path = OUTPUT_DIR / name
        if path.exists() and digest(path.read_bytes()) != digest(content):
            if not previous_output_is_verified:
                raise ValueError('Refusing to overwrite changed Unity task catalog: ' + str(path))
            path.write_bytes(content)
        elif not path.exists():
            path.write_bytes(content)
        write_meta(path, 'text')
    print(f'Built verified Unity mainline catalog: {len(tasks)} tasks, {len(used_items)} direct reward items')


if __name__ == '__main__':
    build()
