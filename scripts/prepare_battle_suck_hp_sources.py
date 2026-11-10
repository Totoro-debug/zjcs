"""Extract selected official C# as an in-memory reference for absorption formulas."""
import argparse
import hashlib
import json
from pathlib import Path
import random
import re

ROOT = Path(__file__).resolve().parents[1]
CODE = ROOT / '.tools/source-packages/current/884_156692/official-code/common-source/Common'
COMBAT = ROOT / 'unity/Assets/Official/Combat'
MANIFEST = ROOT / 'assets/reference/battle-rules/suck-hp-source-manifest.json'


def identity(path):
    return dict(path=path.relative_to(ROOT).as_posix(),
                sha256=hashlib.sha256(path.read_bytes()).hexdigest())


def body(text, signature):
    start = text.index(signature)
    opening = text.index('{', start)
    depth = 1
    end = opening + 1
    while depth:
        depth += (text[end] == '{') - (text[end] == '}')
        end += 1
    return text[start:end]


def reference():
    assert json.loads((ROOT / 'official-source.json').read_bytes())['active'] == '884_156692'
    original = (CODE / 'BattleFormulaHandler.cs').read_text('utf8')
    signatures = ['private double CalcCureAdd(', 'private double ApplyCureAddSoftCap(',
                  'public long SuckHp(', 'public long SuckHpByOnceHit(']
    methods = '\n'.join(body(original, signature) for signature in signatures)
    enums = '\n'.join(re.sub(r'\[NotCheckFightResult\]\s*', '',
                            body((CODE / (name + '.cs')).read_text('utf8'), 'public enum ' + name))
                      for name in ('PropType', 'GameCharacterType'))
    info = body((CODE / 'BattleFormula.cs').read_text('utf8'), 'public struct SuckHpInfo')
    rules = json.loads((COMBAT / 'rules.json').read_bytes())
    skill = json.loads((COMBAT / 'skill-rules.json').read_bytes())
    ids = {v: int(k) for k, v in skill['prop_names'].items()}
    floats = [int(row['PropType']) for row in rules['prop_cfg'] if row['Float']]
    threshold = rules['game_settings']['Battle']['CureAddSoftCapThreshold']
    overflow = rules['game_settings']['Battle']['CureAddOverflowScale']
    code = '''using System; using System.Collections.Generic;
namespace PinnedSuckHp {
''' + enums + '\npublic class BattleFormula {' + info + '}\n'
    code += ('public class BattleSettings { public long CureAddSoftCapThreshold=' + str(threshold)
             + 'L, CureAddOverflowScale=' + str(overflow) + 'L; }\n'
             + 'public class Settings { public BattleSettings Battle=new BattleSettings(); }\n'
             + 'public static class Parsers { public static Settings GameSettings=new Settings(); }\n')
    code += '''public sealed class Handler {
private static readonly HashSet<int> Floats=new HashSet<int>{''' + ','.join(map(str, floats)) + '''};
public double GetProp(Dictionary<PropType,long> values,PropType id) {
    long value; if(values==null || !values.TryGetValue(id,out value)) value=0;
    return Floats.Contains((int)id) ? value/10000.0 : value;
}
private double Long2Double(long value) { return value/10000.0; }
private double OneAddPropWithLimitMinValue(Dictionary<PropType,long> values,PropType id,double min) {
    return Math.Max(1.0+GetProp(values,id),min);
}
''' + methods + '\n}}'

    def props(**values):
        return {str(ids[k]): v for k, v in values.items()}

    def source(**values):
        return props(**dict(BaseCureAdd=100, BaseBeCureAdd=100, **values))

    def case(name, kind=1, master=0, damage=-211, source_props=None,
             rate=2500, fixed=20, coefficient=17500):
        return dict(name=name, source=source_props if source_props is not None else source(),
                    kind=kind, master=master, damage=damage,
                    skill=props(SuckHpByDamage=rate, FixedSuckHp=fixed),
                    damaging=props(SkillAttack1=coefficient))

    cases = [case('character-' + str(kind) + '-master-' + str(master), kind, master,
                  source_props=source(PlayerSkillDmgReduceScale=5000, ProSkillDmgReduceScale=2500))
             for kind in range(10) for master in (0, 1, 2, 4)]
    for value in (-211, 0, 211):
        cases.append(case('damage-sign-' + str(value), damage=value))
    for name, values in (
        ('negative-absorption', dict(rate=-30000, fixed=-20)),
        ('unclamped-absorption', dict(rate=17500)),
        ('fixed-only', dict(rate=0)), ('proportional-only', dict(fixed=0)),
        ('zero-fixed-coefficient', dict(coefficient=0)),
        ('negative-fixed-coefficient', dict(coefficient=-17500)),
        ('negative-reduction-floor', dict(source_props=source(PlayerSkillDmgReduceScale=-7500,
                                                            ProSkillDmgReduceScale=-20000))),
        ('no-final-cure-scale', dict(source_props=source(FinalCureScale=50000))),
        ('cure-values', dict(source_props=source(CureAdd=15, BeCureAdd=33))),
        ('cure-soft-cap', dict(source_props=source(CureAddPercent=300000))),
        ('negative-cure-add', dict(source_props=source(CureAddPercent=-30000))),
        ('zero-cure-bases', dict(source_props=props(SuckHpByDamage=2500))),
    ):
        cases.append(case(name, **values))
    rng = random.Random(884156692)
    for i in range(64):
        cases.append(case('combined-' + str(i), rng.randrange(10), rng.randrange(10),
                          rng.randrange(-100000, 100001),
                          source(CureAdd=rng.randrange(-100, 500), BeCureAdd=rng.randrange(-100, 500),
                                 CureAddPercent=rng.randrange(-20000, 300001),
                                 BeCureAddPercent=rng.randrange(-5000, 10001),
                                 PlayerSkillDmgReduceScale=rng.randrange(-15000, 30001),
                                 ProSkillDmgReduceScale=rng.randrange(-15000, 30001),
                                 SuckHpByDamage=rng.randrange(-5000, 30001)),
                          rng.randrange(-5000, 30001), rng.randrange(-30, 101),
                          rng.randrange(-10000, 30001)))
    return dict(code=code, cases=cases, names=skill['prop_names'], floats=floats,
                threshold=threshold / 10000.0, overflow=overflow / 10000.0, ids=ids)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--harness', action='store_true')
    args = parser.parse_args()
    data = reference()
    if args.harness:
        record = json.loads(MANIFEST.read_bytes())
        for entry in record['official_code'] + record['runtime_inputs'] + [record['generator']]:
            assert identity(ROOT / entry['path']) == entry, 'Absorption formula source identity changed.'
        print(json.dumps(data))
        return
    record = dict(manifest='884_156692', official_code=[identity(CODE / (name + '.cs')) for name in
                  ('BattleFormulaHandler', 'BattleFormula', 'PropType', 'GameCharacterType')],
                  runtime_inputs=[identity(COMBAT / name) for name in ('rules.json', 'skill-rules.json')],
                  generator=identity(Path(__file__)),
                  extracted_methods=['CalcCureAdd', 'ApplyCureAddSoftCap', 'SuckHp', 'SuckHpByOnceHit'],
                  extraction='Original method bodies compiled in memory; only property access and settings use '
                             'the separately hashed existing runtime tables. No asset version replacement.')
    MANIFEST.write_text(json.dumps(record, ensure_ascii=False, indent=2) + '\n', encoding='utf8')
    print('Recorded original absorption formulas and runtime property/configuration input identities.')


if __name__ == '__main__':
    main()
