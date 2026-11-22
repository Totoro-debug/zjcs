"""Record the local activity-time adapter while verifying existing official ports."""
import hashlib
import json
from pathlib import Path
import subprocess

ROOT = Path(__file__).resolve().parents[1]
RECEIPT = ROOT / 'assets/reference/home-output-addition-source-manifest.json'
READER = 'unity/Assets/Game/Runtime/Offline/HomeUI/OfflineHomeOutputAddition.cs'
FILES = (
    READER,
    'unity/Assets/Game/Runtime/Offline/Home/IHomeIncomeAdditionSnapshot.cs',
    'unity/Assets/Game/Runtime/Offline/Home/IHomeIncomeAdditionSnapshot.cs.meta',
    'unity/Assets/Game/Runtime/Offline/HomeUI/OfflineHomeIncomeAdditionSnapshot.cs',
    'unity/Assets/Game/Runtime/Offline/HomeUI/OfflineHomeIncomeAdditionSnapshot.cs.meta',
)


def digest(raw):
    return hashlib.sha256(raw).hexdigest()


def git(*args):
    return subprocess.check_output(['git', '-C', str(ROOT), *args])


def prepare():
    receipt = json.loads(RECEIPT.read_bytes())
    for name, expected in receipt['outputs'].items():
        if digest((ROOT / name).read_bytes()) != expected:
            raise ValueError('Original migrated output differs: ' + name)
    expected = receipt['runtime_bindings'][READER]
    if digest((ROOT / READER).read_bytes()) != expected:
        baseline = git('show', 'HEAD:' + READER)
        if digest(baseline) != expected:
            # Older local bindings can precede a committed adaptation. Retain
            # the exact recorded bytes and identify both verified Git revisions;
            # do not replace an original input/output hash to accept a mismatch.
            recorded = None
            for revision in git('log', '--format=%H', '--', READER).decode('ascii').splitlines():
                if digest(git('show', revision + ':' + READER)) == expected:
                    recorded = revision
                    break
            if recorded is None:
                raise ValueError('The existing reader binding has no matching committed source.')
            baseline_revision = git('log', '-1', '--format=%H', '--', READER).decode('ascii').strip()
            receipt.setdefault('runtime_binding_ancestry', {})[READER] = dict(
                recorded_revision=recorded, recorded_sha256=expected,
                baseline_revision=baseline_revision, baseline_sha256=digest(baseline))
    for name in FILES:
        receipt['runtime_bindings'][name] = digest((ROOT / name).read_bytes())
    adaptation = ('The user-confirmed offline settlement policy selects parent and individual property records '
        'only during their active intervals, including the start and excluding the end. A copied snapshot '
        'then calls the unchanged original selectors and extra-count/capacity formulas. Original display/query '
        'methods retain their differing time handling; this adapter does not schedule activities or grant rewards.')
    if adaptation not in receipt['adaptations']:
        receipt['adaptations'].append(adaptation)
    receipt['income_snapshot_inputs'] = {name: digest((ROOT / name).read_bytes()) for name in (
        'scripts/prepare_home_income_addition_snapshot.py',
        'scripts/prepare_home_output_addition_validation.py',
        'scripts/test_home_income_addition_snapshot.ps1',
        'scripts/fixtures/home_income_addition_snapshot_harness.cs')}
    RECEIPT.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    print('HOME_INCOME_ADDITION_BINDINGS_PASSED: official outputs unchanged; local snapshot bindings recorded.')


if __name__ == '__main__':
    prepare()
