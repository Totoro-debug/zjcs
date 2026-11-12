"""Record the confirmed offline task-state producer and original consumers."""
import argparse
import hashlib
import json
import shutil
from pathlib import Path

from prepare_home_task_condition_reader import ROOT, sha
from prepare_home_task_condition_notifications import build as build_notifications, stage as stage_notifications


def build():
    reader, notifications, original = build_notifications()
    code = ROOT / '.tools/source-packages/current/884_156692/official-code'
    guards = {
        'common-source/Common/TaskState.cs': ('Processing,', 'Completed,', 'Received,'),
        'common-source/Common/PlayerTaskDataUtils.cs': ('return self.TaskState == TaskState.Completed;',
            'public static bool CanFinish(this PlayerTask self, Player player)', 'if (!self.IsCompletedState())'),
        'common-source/Common/TaskCompleteCondition.cs': ('player.FinishedTaskManager.Exist(taskClassId)',
            'taskByClassId != null && taskByClassId.IsCompletedState()'),
        'common-source/Common/TaskSubmitCondition.cs': ('TaskClassIds.Count((int id) => player.FinishedTaskManager.Exist(id))',),
        'client-source/PinnedTaskPanel.cs': ('DisplayCondition', 'IsMet('),
    }
    inputs = {}
    for relative, fragments in guards.items():
        path = code / relative
        text = path.read_text('utf-8-sig')
        for fragment in fragments:
            assert fragment in text, (relative, fragment)
        inputs[path.relative_to(ROOT).as_posix()] = sha(path)
    for relative in ('assets/reference/home-mainline-task-data-source-manifest.json',
            'assets/reference/home-task-condition-reader-source-manifest.json',
            'assets/reference/home-task-condition-notifications-source-manifest.json',
            'scripts/fixtures/home_task_condition_reader_validation.cs',
            'scripts/prepare_home_task_condition_notifications.py'):
        inputs[relative] = sha(ROOT / relative)
    state = ROOT / 'unity/Assets/Game/Runtime/Offline/Progression/OfflineMainlineTaskStates.cs'
    meta = Path(str(state) + '.meta')
    if not meta.exists():
        meta.write_text('fileFormatVersion: 2\nguid: ' + hashlib.md5(b'zjcs:mainline-task-states').hexdigest()
            + '\nMonoImporter:\n  serializedVersion: 2\n  externalObjects: {}\n', 'utf8', newline='\n')
    outputs = {p.relative_to(ROOT).as_posix(): sha(p) for p in (state, meta,
        ROOT / 'unity/Assets/Game/Runtime/Offline/Progression/OfflineMainlineProgressSession.cs',
        ROOT / 'unity/Assets/Game/Editor/Progression/OfflineMainlineProgressValidation.cs')}
    proof = dict(manifest='884_156692', generator_sha256=sha(Path(__file__)), inputs=inputs, outputs=outputs,
        original_fragments=guards,
        confirmed_offline_policy='Acquisition met and finish met generate local Completed; DisplayCondition controls presentation only. '
            'Successful reward submission records Received in the existing submitted-ID ledger.',
        adaptations=['Processing/Completed/Received retain original enum values; local Unavailable represents absence before acquisition.',
            'State is a read-only projection of existing saved cumulative mainline progress, with per-query memoization and task-cycle rejection.',
            'TaskComplete counts submitted IDs and unsubmitted Completed states; TaskSubmit counts submitted IDs only.',
            'Existing fixed reward and receipt persistence stay atomic. Native task-player event forwarding and row composition remain separate.'])
    (ROOT / 'assets/reference/home-mainline-task-states-source-manifest.json').write_text(
        json.dumps(proof, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    return reader, notifications, original, proof


def stage(project, reader, notifications, original, proof):
    stage_notifications(project, reader, notifications, original)
    for relative, expected in proof['outputs'].items():
        assert sha(ROOT / relative) == expected, relative
        target = project / (ROOT / relative).relative_to(ROOT / 'unity')
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(ROOT / relative, target)
    print('Staged confirmed local task states with original complete/submit consumers', flush=True)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(__doc__)
    parser.add_argument('--project', type=Path)
    args = parser.parse_args()
    reader, notifications, original, proof = build()
    if args.project:
        stage(args.project, reader, notifications, original, proof)
