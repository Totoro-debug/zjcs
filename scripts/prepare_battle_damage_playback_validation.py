"""Stage the actual damage/skill entity views without unrelated game UI assemblies."""
import argparse
import json
import os
from pathlib import Path
import re
import shutil
import subprocess

from prepare_battle_movement_effect_validation import prepare, ROOT
from validation_assemblies import stage_runtime_modules

GUID = re.compile(rb'(?m)^guid: ([a-f0-9]{32})\r?$')


def main():
    parser = argparse.ArgumentParser(__doc__)
    parser.add_argument('--code-only', action='store_true')
    parser.add_argument('--custom-targets', action='store_true')
    parser.add_argument('--summon-spawns', action='store_true')
    args = parser.parse_args()
    work = Path(os.environ['ZJCS_VALIDATION_DIR']).resolve()
    project = prepare(work)
    runtime = ROOT / 'unity/Assets/Game/Runtime'
    selected = stage_runtime_modules(project, runtime,
        ['Zjcs.Presentation.Combat', 'Zjcs.Offline.BattleNativePlayables',
         'Zjcs.Official.BattleMaterialPlayables', 'Zjcs.Official.BattleAnimationPlayables'])
    # Progression also consumes the standalone item-regeneration rule module.
    # It contains no home UI and is part of the actual combat dependency closure.
    if any('Home' in name and name != 'Zjcs.Official.HomeAutoIncrease' for name in selected):
        raise ValueError('Damage fixture unexpectedly depends on unrelated home UI.')
    shutil.copytree(ROOT / 'unity/Assets/Spine', project / 'Assets/Spine', dirs_exist_ok=True)
    shutil.copytree(ROOT / 'unity/Assets/TextMesh Pro', project / 'Assets/TextMesh Pro', dirs_exist_ok=True)
    shutil.copytree(ROOT / 'unity/Assets/Official/Combat/DamageUI',
        project / 'Assets/Official/Combat/DamageUI', dirs_exist_ok=True)
    for folder in (runtime / 'Official/Combat', runtime / 'Presentation/Combat'):
        for source in folder.rglob('*'):
            if source.is_file() and source.suffix in ('.shader', '.hlsl', '.cginc'):
                for path in (source, Path(str(source) + '.meta')):
                    if not path.is_file():
                        continue
                    target = project / path.relative_to(ROOT / 'unity')
                    target.parent.mkdir(parents=True, exist_ok=True)
                    shutil.copy2(path, target)
    # The base helper installs only this session's small movement fixture.
    old_editor = project / 'Assets/Game/Editor/BattleMovementEffects'
    if old_editor.is_dir():
        assert old_editor.resolve().is_relative_to(work) and not old_editor.is_symlink()
        shutil.rmtree(old_editor)
    editor = project / 'Assets/Editor'
    editor.mkdir(exist_ok=True)
    for name in ('OfficialBattleDamagePlaybackAcceptance', 'OfficialBattleDamageReceiptAcceptance',
                 'OfficialBattleStatusActionTimingCases'):
        fixture = ROOT / 'unity/Assets/Game/Editor/Combat/AnimatorPlayback' / (name + '.cs')
        shutil.copy2(fixture, editor / fixture.name)
    (editor / 'DamageValidation.asmdef').write_bytes((json.dumps(dict(
        name='DamageEntityValidation', includePlatforms=['Editor'],
        references=sorted(set(selected) | ({'Unity.RenderPipelines.Core.Runtime', 'Unity.RenderPipelines.Universal.Runtime'}
            if args.summon_spawns else set()))), indent=2) + '\n').encode())
    if args.code_only:
        print('Prepared actual damage code:', len(selected), 'runtime assemblies;', project)
        return

    if args.custom_targets:
        for fixture in (ROOT / 'unity/Assets/Game/Editor/Combat/AnimatorPlayback').glob('OfficialBattleHitPlaybackAcceptance*.cs'):
            shutil.copy2(fixture, editor / fixture.name)
    if args.summon_spawns:
        fixture = ROOT / 'unity/Assets/Game/Editor/Combat/AnimatorPlayback/OfficialBattleSummonSpawnAcceptance.cs'
        shutil.copy2(fixture, editor / fixture.name)
    prepare_native_assets(project, work, selected, args.custom_targets, args.summon_spawns)


def prepare_native_assets(project, work, selected, custom_targets=False, summon_spawns=False):
    receipt = json.loads((ROOT / 'assets/reference/battle-animation/skill-native-graph-assets-source-manifest.json').read_bytes())
    catalog_path = ROOT / 'unity/Assets/Game/Resources/Combat/skill-native-graphs.json'
    catalog = json.loads(catalog_path.read_bytes())
    if len(catalog.get('damages', ())) != 1368:
        raise ValueError('Complete selected original damage conversion required.')
    # Build the new native-resource GUID index from the conversion receipt;
    # source scripts and the remaining rule assets are resolved separately.
    index = {}
    for row in receipt['outputs']:
        meta = ROOT / row['path']
        if meta.suffix != '.meta' or not meta.is_file():
            continue
        source = Path(str(meta)[:-5])
        if not source.is_file():
            continue
        match = GUID.search(meta.read_bytes())
        if match:
            index[match[1]] = source
    for meta in (project / 'Assets').rglob('*.meta'):
        if not Path(str(meta)[:-5]).is_file():
            continue
        match = GUID.search(meta.read_bytes())
        if match:
            index.setdefault(match[1], ROOT / 'unity' / Path(str(meta)[:-5]).relative_to(project))

    pending = [ROOT / 'unity/Assets/Game/Resources/Combat' / name for name in (
        'OfficialBattleRenderPipelineAssets.asset', 'OfficialCombatAssetCatalog.asset')]
    if summon_spawns:
        for folder in ('NativeMonsters', 'NativeMonsterAppearance', 'SummonRoleViews', 'SceneCharacterAppearance'):
            source = ROOT / 'unity/Assets/Official/Combat' / folder
            shutil.copytree(source, project / source.relative_to(ROOT / 'unity'), dirs_exist_ok=True)
            # Index the copied working assets too; new converted animations have
            # no cached Git entry until the caller stages them.
            for meta in source.rglob('*.meta'):
                match = GUID.search(meta.read_bytes())
                if match and Path(str(meta)[:-5]).is_file():
                    index[match[1]] = Path(str(meta)[:-5])
            pending.extend(path for path in source.rglob('*') if path.is_file() and path.suffix in ('.asset', '.mat', '.prefab'))
    # The actual skill host initializes this original embedded renderer adapter
    # even when a receipt fixture has no visible character meshes.
    pending.append(ROOT / 'unity/Assets/Official/Combat/EmbeddedMonsterMecanim/Resources/Combat/EmbeddedMonsterMecanim/catalog.asset')
    pending.extend(ROOT / 'unity/Assets/Official/Combat' / name for name in (
        'NativePartnerAssets/Resources/Combat/NativePartnerAssets/skill-skeleton-bindings.json',
        'EmbeddedMonsterSkeletons/Resources/Combat/EmbeddedMonsterSkeletons/bindings.json'))
    resources = ROOT / 'unity/Assets/Game/Resources/Combat'
    for source in resources.iterdir():
        if source.is_file() and source.suffix in ('.json', '.txt', '.bytes'):
            pending.append(source)
    # Include actual child casts and trigger/chain hit entities from the rules.
    skills = [row for row in catalog['skills'] if row['skill_entity_id'] in (10100, 10910, 11050, 13520, 13523, 15300)]
    hits = [row for row in catalog['hits'] if row['hit_entity_id'] in (10101, 10911, 10912, 11051, 13521, 13522, 13524, 13525)]
    moves = [row for row in catalog['moves'] if row['action_entity_id'] == 15301]
    if len(skills) != 6 or len(hits) != 8 or len(moves) != 1:
        raise ValueError('Formal receipt fixture original skill/hit entities are not installed.')
    if custom_targets:
        skills.extend(row for row in catalog['skills'] if row['skill_entity_id'] in (12060,13300,13670))
        hits.extend(row for row in catalog['hits'] if row['hit_entity_id'] in (12061,12063,13301,13303,13671,13673))
    if summon_spawns:
        skills.extend(row for row in catalog['skills'] if row['skill_entity_id'] == 12140)
        hits.extend(row for row in catalog['hits'] if row['hit_entity_id'] == 12141)
    for row in [*skills, *hits, *moves, *catalog['damages']]:
        prefix = row['entity_resource'].removeprefix('Combat/SkillGraphs/')
        pending.append(ROOT / 'unity/Assets/Official/Combat/SkillGraphRuntime/Resources/Combat/SkillGraphs' / (prefix + '.prefab'))
    copied, unresolved = set(), set()
    while pending:
        references = set()
        while pending:
            source = pending.pop()
            if source in copied:
                continue
            copied.add(source)
            if source.suffix in ('.cs', '.asmdef', '.asmref'):
                continue  # Already copied with its complete runtime assembly.
            for path in (source, Path(str(source) + '.meta')):
                target = project / path.relative_to(ROOT / 'unity')
                target.parent.mkdir(parents=True, exist_ok=True)
                shutil.copy2(path, target)
            raw = source.read_bytes()
            if raw.startswith(b'%YAML'):
                references.update(re.findall(rb'guid: ([a-f0-9]{32})', raw))
        missing = references - index.keys() - unresolved
        if missing:
            patterns = work / 'damage-dependency-guids.txt'
            patterns.write_bytes(b'\n'.join(b'guid: ' + key for key in sorted(missing)) + b'\n')
            result = subprocess.run(['git', 'grep', '--cached', '-l', '-F', '-f', str(patterns),
                '--', 'unity/**/*.meta'], cwd=ROOT, capture_output=True)
            if result.returncode not in (0, 1):
                raise RuntimeError(result.stderr.decode())
            for value in result.stdout.decode().splitlines():
                meta = ROOT / value
                match = GUID.search(meta.read_bytes())
                if not match:
                    raise ValueError('Native input has no primary GUID: ' + value)
                if match[1] not in missing:
                    # A package importer can itself reference the requested
                    # GUID. Only its primary sidecar GUID names this asset.
                    cached = subprocess.check_output(['git', 'show', ':' + value], cwd=ROOT)
                    cached_guid = GUID.search(cached)
                    if cached_guid and cached_guid[1] in missing:
                        raise ValueError('Tracked native GUID differs from its live input: ' + value)
                    continue
                index[match[1]] = Path(str(meta)[:-5])
            unresolved.update(missing - index.keys())
        pending.extend(index[key] for key in references if key in index)
    unresolved = {key for key in unresolved if not key.startswith(b'0000000000000000')}
    if unresolved:
        raise ValueError('Unresolved damage fixture asset GUIDs: ' + repr(sorted(unresolved)))
    print('Prepared actual damage fixture:', len(selected), 'runtime assemblies;', len(copied), 'native inputs;', project)


if __name__ == '__main__':
    main()
