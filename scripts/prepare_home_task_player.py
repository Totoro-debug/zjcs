"""Bind the selected task model, fixed reward preview and original safe events."""
import argparse
import hashlib
import json
import re
import shutil
from pathlib import Path

from prepare_home_auto_increase_runtime import method
from prepare_partner_gift_configuration import normalize, without_logging
from prepare_home_task_condition_reader import ROOT, sha
from prepare_home_mainline_task_states import build as build_states, stage as stage_states


def metadata(path):
    target = Path(str(path) + '.meta')
    if not target.exists():
        target.write_text('fileFormatVersion: 2\nguid: ' + hashlib.md5(('zjcs:task-player:' + path.name).encode()).hexdigest()
            + '\nMonoImporter:\n  serializedVersion: 2\n  externalObjects: {}\n', 'utf8', newline='\n')
    return target


def events():
    source = ROOT / '.tools/source-packages/current/884_156692/official-code/framework-firstpass-source/Framework/SafeAction.cs'
    raw = source.read_text('utf-8-sig').replace('\r\n', '\n')
    text = 'using System;\nusing System.Collections.Generic;\nnamespace Zjcs.Official.HomeTaskUI\n{\n'
    methods = {}
    for arity in range(1, 5):
        types = ', '.join('T' + str(i) for i in range(1, arity + 1))
        action = 'Action<' + types + '>'
        declaration = 'public class SafeAction<' + types + '>'
        start = raw.index(declaration)
        end = raw.find('\npublic class SafeAction<', start + len(declaration))
        section = raw[start:end if end >= 0 else len(raw)]
        for field in ('private readonly HashSet<' + action + '> mActionSet = new HashSet<' + action + '>();',
                'private readonly Stack<List<' + action + '>> mStack = new Stack<List<' + action + '>>();'):
            assert field in section, field
        text += 'public sealed class HomeTaskPlayerEvent<' + types + '>\n{\n'
        text += 'private readonly HashSet<' + action + '> mActionSet = new HashSet<' + action + '>();\n'
        text += 'private readonly Stack<List<' + action + '>> mStack = new Stack<List<' + action + '>>();\n'
        text += 'readonly Action<Exception> reportException;\npublic HomeTaskPlayerEvent(Action<Exception> reportException) '
        text += '{ this.reportException=reportException??throw new ArgumentNullException(nameof(reportException)); }\n'
        signatures = ('private List<' + action + '> PopList()', 'private void PushList(List<' + action + '> list)',
            'public void AddListener(' + action + ' action)', 'public void RemoveListener(' + action + ' action)',
            'public void ClearListener()', 'public void Invoke(' + ', '.join('T' + str(i) + ' param' + str(i) for i in range(1, arity + 1)) + ')')
        for signature in signatures:
            original = method(section, signature)
            methods[str(arity) + ':' + signature] = hashlib.sha256(original.encode()).hexdigest()
            value = without_logging(original, 1) if ' Invoke(' in signature else original
            if ' Invoke(' in signature:
                value, count = re.subn(r'catch \(Exception exception\)\s*\{\s*\}',
                    'catch (Exception exception) { reportException(exception); }', value)
                assert count == 1
            text += value + '\n'
        text += '}\n'
    text += '}\n'
    destination = ROOT / 'unity/Assets/Game/Runtime/Official/HomeTaskUI/HomeTaskPlayerEvents.cs'
    destination.write_text(normalize('// Selected original SafeAction<T1..T4>; explicit local exception reporter.\n' + text),
        'utf8', newline='\n')
    return source, methods, destination


def build():
    # This module adds the exact attachments required even by the original
    # SetAttachment(ProbabilityTip, false) branch of a fixed reward preview.
    reward_path = ROOT / 'assets/reference/home-task-reward-images-source-manifest.json'
    reward = json.loads(reward_path.read_bytes())
    pool = 'unity/Assets/Game/Runtime/Offline/HomeTaskUI/OfflineHomeTaskRewardIcons.cs'
    reward['runtime_output_sha256'][pool] = sha(ROOT / pool)
    reward_path.write_text(json.dumps(reward, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    reader, notifications, original, states = build_states()
    safe, methods, event_source = events()
    code = ROOT / '.tools/source-packages/current/884_156692/official-code'
    guards = {
        'common-source/MsgPlayerTaskUpdate.cs': ('foreach (int finishedTask in playerTaskUpdateMsg.FinishedTaskList)',
            'foreach (long removeTask in playerTaskUpdateMsg.RemoveTaskList)', 'mainPlayer.FireTaskUpdate(task);'),
        'common-source/Common/TaskSettings.cs': ('public readonly HashSet<TaskType> AutoPinTaskTypes;',
            'public readonly HashSet<TaskType> CanBePinnedTaskTypes;'),
        'common-source/Common/PlayerTaskDataUtils.cs': ('public static bool CanGetReward(this PlayerTask self, Player player)',
            'public static bool CanFinish(this PlayerTask self, Player player)', 'return self.TaskState == TaskState.Completed;',
            'return task.Info().FinishCondition.Type == ConditionType.HandInItem;'),
        'common-source/Common/PlayerTaskRecordDataWrap.cs': ('return int.MaxValue;', 'public bool CanFinishMoreTask(Player player, TaskType taskType)'),
        'common-source/Common/TaskInfo.cs': ('public bool IsAllowTrace => !BanTrace;',),
        'client-source/TaskDataUtils.cs': ('public static void InitRewardIconWithProbability(',
            '.SetClickPopupInfoDialog().SetCountTextScale(ItemIcon.sTextScale)', 'itemIcon2.SetCount(item2.FixedCount);'),
        'client-source/AwardUtils.cs': ('Parsers.GameSettings.IgnoreShowAwardId.Contains(itemId)', 'case ItemType.SeasonBadge:'),
        'common-source/RewardPreviewItemComparer.cs': ('(ItemComparerType.Designated, ComparerOrderType.Desc)',
            '(ItemComparerType.Quality, ComparerOrderType.Desc)', '(ItemComparerType.ClassId, ComparerOrderType.Asc)',
            'Parsers.GameSettings.PlayerExpItemId == itemInfo.ClassId', 'itemInfo.ItemType == ItemType.Money'),
        'common-source/Common/ItemType.cs': ('TreasureSuit = 307,', 'Score = 312,', 'NpcFriendship = 329,', 'SeasonBadge = 426,'),
    }
    inputs = {safe.relative_to(ROOT).as_posix(): sha(safe)}
    for relative, fragments in guards.items():
        path = code / relative
        text = path.read_text('utf-8-sig')
        for fragment in fragments:
            assert fragment in text, (relative, fragment)
        inputs[path.relative_to(ROOT).as_posix()] = sha(path)
    for relative in ('assets/reference/home-mainline-task-states-source-manifest.json',
            'assets/reference/home-task-condition-notifications-source-manifest.json',
            'assets/reference/home-task-reward-images-source-manifest.json',
            'assets/reference/home-currency-windows-native-source-manifest.json',
            'assets/reference/home-task-reward-attachments-source-manifest.json',
            'unity/Assets/Official/Home/UI/CurrencyWindows/home-currency-windows-rules.json',
            'scripts/prepare_home_mainline_task_states.py', 'scripts/fixtures/home_task_player_validation.cs'):
        inputs[relative] = sha(ROOT / relative)
    sources = [event_source,
        ROOT / 'unity/Assets/Game/Runtime/Offline/HomeTaskUI/OfflineHomeTaskPlayer.cs',
        ROOT / 'unity/Assets/Game/Runtime/Offline/HomeTaskUI/OfflineHomeTaskRewardPresenter.cs']
    outputs = {p.relative_to(ROOT).as_posix(): sha(p) for source in sources for p in (source, metadata(source))}
    attachments = json.loads((ROOT / 'assets/reference/home-task-reward-attachments-source-manifest.json').read_bytes())
    assert attachments['generator_sha256'] == sha(ROOT / 'scripts/prepare_home_task_reward_attachments.py')
    for group in ('inputs', 'installed_output_sha256', 'reused_output_sha256'):
        for relative, expected in attachments[group].items(): assert sha(ROOT / relative) == expected, relative
    for binding in attachments['script_bindings'].values():
        assert sha(ROOT / binding['path']) == binding['sha256']
        if binding['path'].endswith('/MultiLanguageTransformControler.cs'):
            for relative in (binding['path'], binding['path'] + '.meta'): outputs[relative] = sha(ROOT / relative)
    outputs.update(attachments['installed_output_sha256'])
    outputs[pool] = sha(ROOT / pool)
    proof = dict(manifest='884_156692', generator_sha256=sha(Path(__file__)), inputs=inputs, outputs=outputs,
        safe_event_methods=methods, original_fragments=guards,
        adaptations=['Selected 55 single-instance mainline models read actual local Processing/Completed/Received state, original flags and conditions.',
            'Local task class IDs identify these non-repeating instances; original task settings supply automatic pin eligibility.',
            'Original SafeAction generic arities 1..4 retain deduplication, snapshot/list pooling and exception isolation, with an explicit exception reporter.',
            'Fixed reward preview uses original ignore IDs, designated/quality/class-ID sorting and original native pooled icons. Special filters outside the selected seven definitions are explicitly rejected.',
            'Submission reloads the actual shared profile, commits existing reward/receipt logic, then publishes coherent task and item changes. Navigation, trace and character combat/rank sources are explicit host ports.',
            'Full native task-row services and production HUD composition remain separate.'])
    (ROOT / 'assets/reference/home-task-player-source-manifest.json').write_text(
        json.dumps(proof, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    return reader, notifications, original, states, proof


def stage(project, reader, notifications, original, states, proof):
    # The older reward fixture runs before this module's checks, so install the
    # added native attachment dependencies before staging that fixture.
    for relative, expected in proof['outputs'].items():
        if relative.startswith('unity/Assets/Official/') or relative.startswith('unity/Assets/Game/Resources/'):
            source = ROOT / relative
            assert sha(source) == expected
            target = project / source.relative_to(ROOT / 'unity')
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(source, target)
    stage_states(project, reader, notifications, original, states)
    for relative, expected in proof['outputs'].items():
        assert sha(ROOT / relative) == expected, relative
        target = project / (ROOT / relative).relative_to(ROOT / 'unity')
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(ROOT / relative, target)
    folder = project / 'Assets/HomeCurrencyWindowsNativeValidation'
    path = folder / 'HomeCurrencyWindowsNativeValidation.cs'
    text = path.read_text('utf8')
    marker = '        CheckTaskConditionReader();'
    assert text.count(marker) == 1
    text = text.replace(marker, marker + '\n        yield return ExerciseTaskPlayer();', 1)
    path.write_text(text, 'utf8', newline='\n')
    shutil.copy2(ROOT / 'scripts/fixtures/home_task_player_validation.cs', folder / 'HomeTaskPlayerValidation.cs')
    print('Staged real task-player models, native fixed reward previews and original safe events', flush=True)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(__doc__)
    parser.add_argument('--project', type=Path)
    args = parser.parse_args()
    reader, notifications, original, states, proof = build()
    if args.project:
        stage(args.project, reader, notifications, original, states, proof)
