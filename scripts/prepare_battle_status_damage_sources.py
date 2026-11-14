"""Pin and compile the original shared status-damage formula for comparison."""
import argparse
import hashlib
import json
from pathlib import Path

from prepare_battle_suck_hp_sources import CODE, COMBAT, ROOT, body, identity, reference

MANIFEST = ROOT / 'assets/reference/battle-rules/status-damage-source-manifest.json'


def code_identity(path):
    return dict(path=path.relative_to(ROOT).as_posix(),
        sha256=hashlib.sha256(path.read_bytes().replace(b'\r\n', b'\n')).hexdigest(),
        text_hash='CRLF normalized to LF')


def main():
    parser = argparse.ArgumentParser(__doc__)
    parser.add_argument('--harness', action='store_true')
    args = parser.parse_args()
    originals = [CODE / (name + '.cs') for name in (
        'BattleFormulaHandler', 'BattleFormula', 'PropType', 'GameCharacterType', 'FightHandleCfgStatusDamageByDamageResultValueAndStore',
        'FightHandleStatusDamageByDamageResultValueAndStore', 'DamageResultParameter', 'DamageResultValueType')]
    inputs = [COMBAT / name for name in ('rules.json', 'skill-rules.json', 'battle-rules.json')]
    helpers = [ROOT / 'scripts/prepare_battle_suck_hp_sources.py', Path(__file__)]
    record = dict(manifest='884_156692', official_code=[identity(p) for p in originals],
        runtime_inputs=[identity(p) for p in inputs], generators=[code_identity(p) for p in helpers],
        extracted_methods=['BattleFormulaHandler.StatusDamage'],
        migration=code_identity(ROOT / 'unity/Assets/Game/Runtime/Official/Combat/FightDamageFormula.StatusDamage.cs'),
        validation=code_identity(ROOT / 'scripts/test_battle_status_damage.ps1'),
        operation='Preserve original absolute input, typed property conversion, positive-only cap, '
            'zero floor, truncation and negative output. This formula does not select a result, '
            'apply HP, dispatch status notifications or infer missing server handle behavior.')
    if not args.harness:
        MANIFEST.write_text(json.dumps(record, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
        print('Recorded original status-damage formula and input identities.')
        return
    assert json.loads(MANIFEST.read_bytes()) == record, 'Status-damage source identities changed.'
    data = reference()
    method = body((CODE / 'BattleFormulaHandler.cs').read_text('utf-8'), 'public long StatusDamage(')
    # Reuse the already audited property/settings harness, with the untouched
    # original method inserted in its own type namespace.
    data['code'] = data['code'].replace('namespace PinnedSuckHp', 'namespace PinnedStatusDamage')[:-2] + method + '\n}}'
    print(json.dumps(dict(code=data['code'], names=data['names'], floats=data['floats'], ids=data['ids'])))


if __name__ == '__main__':
    main()
