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
    'unity/Assets/Game/Runtime/Offline/Home/OfflineHangUpIncome.cs',
    'unity/Assets/Game/Runtime/Offline/Home/OfflineHangUpIncomeAddition.cs',
    'unity/Assets/Game/Runtime/Offline/Home/OfflineHangUpIncomeAddition.cs.meta',
    'unity/Assets/Game/Runtime/Offline/Home/OfflineHomeIncomeActivityLedger.cs',
    'unity/Assets/Game/Runtime/Offline/Home/OfflineHomeIncomeActivityLedger.cs.meta',
    'unity/Assets/Game/Runtime/Offline/Home/OfflineHomeIncomeSession.cs',
    'unity/Assets/Game/Runtime/Offline/Home/OfflineHomeIncomeSession.Activities.cs',
    'unity/Assets/Game/Runtime/Offline/Home/OfflineHomeIncomeSession.Activities.cs.meta',
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
    formula_receipt = ROOT / 'assets/reference/home-income-formula-source-manifest.json'
    formula_ports = {name: expected for name, expected in json.loads(formula_receipt.read_bytes())['outputs'].items()
        if name.startswith('unity/Assets/Game/Runtime/Official/HomeIncomeRules/')}
    if not formula_ports:
        raise ValueError('The original income formula proof is empty.')
    for name, expected in formula_ports.items():
        if digest((ROOT / name).read_bytes()) != expected:
            raise ValueError('Original income formula port differs: ' + name)
    receipt['income_projection_original_ports'] = formula_ports
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
    projection = ('Local activity production is the difference of the ordinary preview value at each active boundary, '
        'using the original level/elapsed argument order before its integer cast. The original float percentage '
        'conversion is retained; fractional additions are exposed for the durable ledger rather than truncated at '
        'each checkpoint. This projection does not grant rewards or enforce capacity.')
    if projection not in receipt['adaptations']:
        receipt['adaptations'].append(projection)
    ledger = ('The local activity ledger keeps separate ordinary and extra integer counts, retaining earned fractions. '
        'It reuses the fruit-tree policy of stopping production while full and preserving mature overflow and '
        'earned fractions after capacity expiry. A fixed production-time and constant-rate anchor makes results '
        'independent of online checkpoint frequency. Inventory grants and durable transactions remain owned by the home session.')
    if ledger not in receipt['adaptations']:
        receipt['adaptations'].append(ledger)
    transactions = ('The home session saves optional activity state in its existing atomic ledger. Live previews '
        'project an isolated entry without writes; claims advance the old activity snapshot inside the grant '
        'transaction. An activity replacement first settles its previous saved interval, and actual rule/property '
        'changes rebase future activity production while keeping already earned counts and fractions. Empty inline '
        'objects produced by Unity JSON remain disabled and do not create activity records.')
    if transactions not in receipt['adaptations']:
        receipt['adaptations'].append(transactions)
    receipt['income_snapshot_inputs'] = {name: digest((ROOT / name).read_bytes()) for name in (
        'scripts/prepare_home_income_addition_snapshot.py',
        'scripts/prepare_home_output_addition_validation.py',
        'scripts/test_home_income_addition_snapshot.ps1',
        'scripts/fixtures/home_income_addition_snapshot_harness.cs',
        'scripts/test_home_income_addition_intervals.ps1',
        'scripts/fixtures/home_income_addition_intervals_harness.cs',
        'scripts/test_home_income_activity_ledger.ps1',
        'scripts/fixtures/home_income_activity_ledger_harness.cs',
        'unity/Assets/Game/Validation/HomeIncomeActivity/HomeIncomeActivitySessionValidation.cs',
        'unity/Assets/Game/Validation/HomeIncomeActivity/Zjcs.Validation.HomeIncomeActivity.asmdef',
        'assets/reference/home-income-formula-source-manifest.json')}
    RECEIPT.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    print('HOME_INCOME_ADDITION_BINDINGS_PASSED: official outputs unchanged; local snapshot bindings recorded.')


if __name__ == '__main__':
    prepare()
