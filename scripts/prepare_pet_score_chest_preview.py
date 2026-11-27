"""Recover the explicit use-result closure of the original pet score reward boxes."""
import argparse
import hashlib
import json
from pathlib import Path

from official_apk import Apk, ROOT
from official_tables import Constructors
from prepare_unity_map13_sources import meta
from prepare_partner_input_dialog import metadata as runtime_metadata

OUTPUT = ROOT / 'unity/Assets/Game/Resources/PetGacha/chest-use-preview.json'
RECEIPT = ROOT / 'assets/reference/pet-score-chest-preview-source-manifest.json'
RUNTIME = ROOT / 'unity/Assets/Game/Runtime/Offline/Progression/OfficialChestUsePreviewRules.cs'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def original_method(text, signature):
    start = text.index(signature)
    opening = text.index('{', start)
    # The selected source method contains no braces inside string literals.
    depth = 1
    end = opening + 1
    while depth:
        depth += (text[end] == '{') - (text[end] == '}')
        end += 1
    return text[start:end]


def write_oracle(destination):
    from official_source import OfficialSource
    code = ROOT / '.tools/source-packages/current' / OfficialSource().version / 'official-code/client-source'
    original = (code / 'ItemDetailDialogUseResult.cs').read_text('utf8')
    method = original_method(original, 'private void LoadResultFromChest(')
    # Only access is changed to call the actual original method in the fixture.
    method = method.replace('private void', 'public void', 1)
    sort = (code / 'SkillOptionSortUtils.cs').read_text('utf8')
    oracle = '''using System;
using System.Collections.Generic;
using Common;
public sealed class OriginalChestUsePreview {
    public readonly List<OriginalRow> mResultItemInfos = new List<OriginalRow>();
    public struct OriginalRow { public int ItemId; public long Count; public int FromId;
        public OriginalRow(int id, long count, int from) { ItemId=id; Count=count; FromId=from; } }
    private bool LoadResultFromRewardRules(List<int> ids, int itemId)
        => throw new Exception("Random loader must not run in the explicit-reward oracle.");
'''
    oracle += method.replace('new ResultItemInfo(', 'new OriginalRow(') + '\n}\n' + sort[sort.index('public static class SkillOptionSortUtils'):]
    # The type spelling above is the only method-body substitution; no branch,
    # sorting, dictionary iteration, count or input is reconstructed in a test.
    destination.parent.mkdir(parents=True, exist_ok=True)
    destination.write_text(oracle, 'utf8', newline='\n')


def build(check=False):
    apk = Apk.current()
    try:
        code = ROOT / '.tools/source-packages/current' / apk.version / 'official-code'
        constructors = Constructors(code)
        sources = []

        def table(name, kind):
            obj = apk.asset('config_luban', '/' + name + '.g.bytes')
            payload = obj.read().m_Script.encode('utf8', 'surrogateescape')
            sources.append(dict(name=name, source=apk.provenance(obj),
                object_sha256=hashlib.sha256(obj.get_raw_data()).hexdigest(),
                payload_sha256=hashlib.sha256(payload).hexdigest()))
            return constructors.table(payload, kind)

        items = table('item', 'TbItemInfo')
        chests = table('chest', 'TbChestInfo')
        achievements = table('achievement', 'TbAchievementRawInfo')
        settings = table('game_settings', 'TbGameSettings')
        ids = {int(key) for row in achievements if row['AchievementType'] == 66 for key in row['Reward']}
        selected = [row for row in chests if row['ClassId'] in ids]
        if {row['ClassId'] for row in selected} != ids:
            raise ValueError('Inspect every original score reward item before projecting chest previews')
        materials = set()
        for row in selected:
            if row['RewardRules']:
                raise ValueError('Migrate original random reward records before projecting this chest')
            materials.update(map(int, row['Rewards'] or {}))
            materials.update(map(int, row['OptionalRewards'] or {}))
        closure = [row for row in items if row['ClassId'] in ids | materials]
        if {row['ClassId'] for row in closure} != ids | materials:
            raise ValueError('Original preview item closure is incomplete')
        skill_types = set(map(int, settings['SkillTypes'])) | set(settings['SkillPieceTypes'])
        if any(row['ItemType'] in skill_types for row in closure if row['ClassId'] in materials):
            raise ValueError('Migrate original skill candidate ordering before projecting these rewards')
        data = dict(manifest=apk.version, tables=dict(item=closure, chest=selected,
            game_settings={key: settings[key] for key in ('SkillTypes', 'SkillPieceTypes')}))
        raw = (json.dumps(data, ensure_ascii=False, indent=2) + '\n').encode('utf8')
        previous = json.loads(RECEIPT.read_bytes()) if RECEIPT.is_file() else None
        if check:
            if OUTPUT.read_bytes() != raw:
                raise ValueError('Selected original chest preview payload differs')
        else:
            if OUTPUT.exists() and (not previous or sha(OUTPUT) != previous['outputs'][OUTPUT.relative_to(ROOT).as_posix()]):
                raise ValueError('Preserve another chest preview payload edit')
            OUTPUT.write_bytes(raw)
            meta(OUTPUT)
            metadata = Path(str(RUNTIME) + '.meta')
            if not metadata.exists():
                token = hashlib.md5(('zjcs-official:' + RUNTIME.relative_to(ROOT / 'unity').as_posix()).encode()).hexdigest()
                metadata.write_text('fileFormatVersion: 2\nguid: ' + token + '\nMonoImporter:\n'
                    '  externalObjects: {}\n  serializedVersion: 2\n  defaultReferences: []\n'
                    '  executionOrder: 0\n  icon: {instanceID: 0}\n  userData:\n'
                    '  assetBundleName:\n  assetBundleVariant:\n', 'utf8', newline='\n')
            runtime_metadata(ROOT / 'unity/Assets/Game/Runtime/Offline/Progression/OfflineOptionalChestService.cs')
        paths = [Path(__file__), ROOT / 'scripts/official_apk.py', ROOT / 'scripts/official_tables.py', RUNTIME,
            Path(str(RUNTIME) + '.meta'), ROOT / 'unity/Assets/Game/Runtime/Offline/Equipment/OfficialEquipmentJson.cs',
            ROOT / 'scripts/fixtures/chest_use_preview_validation.cs', ROOT / 'scripts/test_chest_use_preview.ps1']
        paths += [ROOT / 'scripts/fixtures/optional_chest_validation.cs', ROOT / 'scripts/test_optional_chest.ps1',
            ROOT / 'unity/Assets/Game/Runtime/Offline/Progression/OfflineOptionalChestService.cs',
            ROOT / 'unity/Assets/Game/Runtime/Offline/Progression/OfflineOptionalChestService.cs.meta',
            ROOT / 'unity/Assets/Game/Runtime/Offline/Progression/OfflineInventoryOperations.cs',
            ROOT / 'unity/Assets/Game/Runtime/Offline/Progression/OfflineMainlineProgressSession.cs']
        paths += [code / relative for relative in ('client-source/ItemDetailDialogUseResult.cs',
            'client-source/SkillOptionSortUtils.cs', 'client-source/ItemDetailDialogFactory.cs',
            'client-source/ItemUseEffectOpenChest.cs')]
        for name in constructors.sources:
            path = code / 'common-source/Common' / (name + '.cs')
            if path.is_file(): paths.append(path)
        proof = dict(manifest=apk.version, tables=sources, score_reward_items=sorted(ids),
            inputs={path.relative_to(ROOT).as_posix(): sha(path) for path in paths},
            outputs={path.relative_to(ROOT).as_posix(): sha(path) for path in (OUTPUT, Path(str(OUTPUT) + '.meta'))},
            scope='Explicit non-skill chest use-result data, optional precedence, original material order and use-count scaling. Local optional chest transactions atomically consume configured costs, grant selected entries and record openings. Native item-use/choose windows remain separate work.')
        if check:
            if previous != proof: raise ValueError('Chest preview source boundary differs')
            print('Original score chest use-result data and source verified')
        else:
            RECEIPT.write_text(json.dumps(proof, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    finally:
        apk.reader.close()


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    parser.add_argument('--oracle', type=Path)
    args = parser.parse_args()
    if args.oracle: write_oracle(args.oracle)
    else: build(args.check)
