"""Record selected original task condition methods and stage their local reads."""
import argparse
import hashlib
import json
from pathlib import Path
import shutil

from official_apk import ROOT
from prepare_home_task_reward_validation import stage as stage_rewards
from validation_workspace import validation_directory


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def build():
    code = ROOT / '.tools/source-packages/current/884_156692/official-code'
    originals = {
        'common-source/Common/ICondition.cs': 'TextProcessor = ConditionTextUtils.GetTextProcessor(color)',
        'common-source/Common/ConditionTextUtils.cs': 'return new ConditionTextProcessor(color)',
        'common-source/Common/ConditionTextProcessor.cs': 'LanguageUtils.TryReplaceConditionStateText(text, mColor)',
        'common-source/Common/TextUnchangedProcessor.cs': 'return text;',
        'common-source/Common/LanguageUtils.cs': 'return text.Replace("{c}", color);',
        'common-source/Common/Formula.cs': 'return (float)val / 10000f;',
        'common-source/Common/FormulaHandler.cs': 'return Formula.Float2Int((float)progress / (float)regionMaxProgress);',
        'common-source/Common/Player.cs': 'public int GetWorldMapRegionProgress(int mapClassId, int region)',
        'common-source/Common/WorldMapProgressCondition.cs': 'return (current: GetValue(dataHub), target: CodeProgress);',
        'common-source/Common/MapGameTotalCountCondition.cs': 'descComponent.Args = new object[2] { value, Count };',
        'common-source/Common/MapLogicEntityEndByTagCondition.cs': 'return (current: GetValue(dataHub), target: 1L);',
        'common-source/Common/MapRegionGoddessCondition.cs': 'return (current: IsGoddessActive(dataHub) ? 1 : 0, target: 1L);',
        'common-source/Common/MaxProfessionRankCondition.cs': 'return (current: 1L, target: 1L);',
        'common-source/Common/TotalOpenChestCondition.cs': 'return (current: GetValue(dataHub), target: Count);',
        'common-source/Common/DemonBattleLayerPassCondition.cs': 'item = Math.Min(item, item2);',
        'common-source/Common/TaskSubmitCondition.cs': 'return TaskClassIds.Count((int id) => player.FinishedTaskManager.Exist(id));',
        'client-source/ClientWorldMapProgressCondition.cs': 'new CultureTextArg(LanguageUtils.GetRegionNameKey(MapClassId, CodeRegion))',
        'client-source/ClientMapLogicEntityEndByTagCondition.cs': 'descComponent.Args = new object[2] { item, item2 };',
        'client-source/ClientDemonBattleLayerPassCondition.cs': 'return (current: num + 1, target: num2 + 1);',
    }
    inputs = {}
    for relative, fragment in originals.items():
        path = code / relative
        if fragment not in path.read_text('utf-8-sig'):
            raise ValueError('Review original condition method: ' + relative)
        inputs[path.relative_to(ROOT).as_posix()] = sha(path)
    task_path = ROOT / 'assets/reference/home-mainline-task-data-source-manifest.json'
    tasks = json.loads(task_path.read_bytes())
    assert tasks['manifest'] == '884_156692'
    for relative, expected in tasks['outputs'].items():
        assert sha(ROOT / relative) == expected, relative
        inputs[relative] = expected
    inputs[task_path.relative_to(ROOT).as_posix()] = sha(task_path)
    module = ROOT / 'unity/Assets/Game/Runtime/Offline/HomeTaskUI'
    reader = module / 'OfflineHomeTaskConditionReader.cs'
    meta = Path(str(reader) + '.meta')
    if not meta.exists():
        meta.write_text('fileFormatVersion: 2\nguid: ' + hashlib.md5(b'zjcs:home-task-condition-reader').hexdigest()
            + '\nMonoImporter:\n  serializedVersion: 2\n  externalObjects: {}\n', 'utf8', newline='\n')
    # This is an updated local assembly dependency, not a native asset/hash change.
    reward_path = ROOT / 'assets/reference/home-task-reward-images-source-manifest.json'
    reward = json.loads(reward_path.read_bytes())
    assembly = module / 'Zjcs.Offline.HomeTaskUI.asmdef'
    reward['runtime_output_sha256'][assembly.relative_to(ROOT).as_posix()] = sha(assembly)
    reward_path.write_text(json.dumps(reward, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    for relative in ('scripts/fixtures/home_task_condition_reader_validation.cs',
            'scripts/prepare_home_task_reward_validation.py', 'assets/reference/home-task-reward-images-source-manifest.json',
            'assets/reference/home-condition-notifications-source-manifest.json'):
        inputs[relative] = sha(ROOT / relative)
    outputs = {path.relative_to(ROOT).as_posix(): sha(path) for path in (reader, meta, assembly,
        ROOT / 'unity/Assets/Game/Runtime/Offline/Progression/OfflineMainlineProgressSession.cs',
        ROOT / 'unity/Assets/Game/Editor/Progression/OfflineMainlineProgressValidation.cs')}
    proof = dict(manifest='884_156692', generator_sha256=sha(Path(__file__)), inputs=inputs, outputs=outputs,
        original_fragments=originals,
        offline_policy='Confirmed: task profession-rank parameter follows the actual highest unlocked local profession rank.',
        scope='Original display/progress behavior for all 55 supported mainline conditions, reading existing profile state. '
            'Normalized local region progress is converted once to original integer percentages. '
            'Native task-row event and navigation composition remain separate; tower/GachaId providers are explicit.')
    (ROOT / 'assets/reference/home-task-condition-reader-source-manifest.json').write_text(
        json.dumps(proof, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    return proof


def stage(project, proof):
    assert project.resolve().is_relative_to(validation_directory().resolve())
    stage_rewards(project)
    for relative, expected in proof['outputs'].items():
        source = ROOT / relative
        assert sha(source) == expected, relative
        target = project / source.relative_to(ROOT / 'unity')
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(source, target)
    folder = project / 'Assets/HomeCurrencyWindowsNativeValidation'
    path = folder / 'HomeCurrencyWindowsNativeValidation.cs'
    text = path.read_text('utf8')
    marker = '        yield return ExerciseTaskRewardIcons();'
    assert text.count(marker) == 1
    text = text.replace(marker, marker + '\n        CheckTaskConditionReader();', 1)
    marker = 'LanguageManager.Initialize(new Language(dictionary));'
    assert text.count(marker) == 1
    text = text.replace(marker, 'foreach(var pair in OfflineHomeMainlineTaskData.LoadDefault().Texts)dictionary[pair.Key]=pair.Value;\n        ' + marker)
    path.write_text(text, 'utf8', newline='\n')
    shutil.copy2(ROOT / 'scripts/fixtures/home_task_condition_reader_validation.cs', folder / 'HomeTaskConditionReaderValidation.cs')
    print('Staged original task condition reads with an actual new-character profile', flush=True)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(__doc__)
    parser.add_argument('--project', type=Path)
    args = parser.parse_args()
    proof = build()
    if args.project:
        stage(args.project, proof)
