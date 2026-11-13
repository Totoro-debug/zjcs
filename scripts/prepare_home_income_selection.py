"""Migrate the selected original home reward-row selectors, without CSV infrastructure."""
import hashlib
import json
from pathlib import Path

from official_apk import ROOT
from prepare_home_auto_increase_runtime import method


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def build():
    code = ROOT / '.tools/source-packages/current/884_156692/official-code'
    target = ROOT / 'unity/Assets/Game/Runtime/Official/HomeIncomeRules'
    inputs, outputs = {}, {}

    def read(name):
        path = code / 'common-source/Common' / (name + '.cs')
        inputs[path.relative_to(ROOT).as_posix()] = digest(path)
        return path.read_text('utf-8-sig').replace('\r\n', '\n')

    proof_path = ROOT / 'assets/reference/official-client-code/source-manifest.json'
    proof = next(row for row in json.loads(proof_path.read_bytes())['projects']
                 if row['assembly'] == 'Console.dll' and row['folder'] == 'common-source')
    assembly = code / 'managed/decoded/Console.dll'
    assert digest(assembly) == proof['source']['decoded_sha256']
    for path in (proof_path, assembly, ROOT / 'scripts/prepare_home_auto_increase_runtime.py',
                 ROOT / 'assets/reference/home-rules-current-source-manifest.json',
                 ROOT / 'assets/reference/home-condition-source-manifest.json',
                 ROOT / 'unity/Assets/Official/Home/home-rules-current.json'):
        inputs[path.relative_to(ROOT).as_posix()] = digest(path)
    table_proof = json.loads((ROOT / 'assets/reference/home-rules-current-source-manifest.json').read_bytes())
    assert table_proof['manifest'] == '884_156692'
    assert digest(ROOT / table_proof['output']['path']) == table_proof['output']['sha256']
    for name in ('HangUpUtils', 'PlayerHangUpDataExt', 'FurnitureLevelRuleInfo'):
        read(name)

    base_get = method(read('LubanDoubleKeyCsvParser'), 'public T Get(K0 key0, K1 key1)')
    base_get = base_get.replace('K0 key0, K1 key1', 'int key0, int key1')
    for name, row_type, signature in (
        ('HangUpRewardInfoParser', 'HangUpRewardInfo', 'public HangUpRewardInfo Get(int id, ICondition.DataHub dataHub)'),
        ('RewardRuleInfoParser', 'RewardRuleInfo', 'public RewardRuleInfo Get(int id, ICondition.DataHub dataHub, bool ignoreCondition = false)')):
        source = read(name)
        post = method(source, 'protected override void PostParse(')
        post = post.replace('protected override void', 'private void')
        post = post.replace('\t\tbase.PostParse(configs);\n', '')
        select = method(source, signature)
        select = select.replace('ICondition.DataHub dataHub, ', '').replace(', ICondition.DataHub dataHub', '')
        select = select.replace('Get(id, key).Condition.IsMet(dataHub)', 'conditionIsMet(Get(id, key))')
        for var in ('rewardRuleInfo', 'rewardRuleInfo2'):
            select = select.replace(var + '.Condition == null', '!hasCondition(' + var + ')')
            select = select.replace(var + '.Condition.IsMet(dataHub)', 'conditionIsMet(' + var + ')')
        assert 'dataHub' not in select and '.Condition' not in select
        ctor_args = ', Func<T, bool> hasCondition' if name == 'RewardRuleInfoParser' else ''
        ctor_assign = '\n        this.hasCondition = hasCondition ?? throw new ArgumentNullException(nameof(hasCondition));' if ctor_args else ''
        body = (post + '\n' + select).replace(row_type, 'T')
        text = ('// Selected Console.dll: original PostParse/Get and double-key lookup.\n'
                'using System;\nusing System.Collections.Generic;\n'
                'namespace Zjcs.Official.HomeIncomeRules\n{\n'
                'public sealed class ' + name + '<T> where T : class\n{\n'
                '    private readonly Dictionary<int, Dictionary<int, T>> configs;\n'
                '    private readonly Dictionary<int, List<int>> mOrderedIndex = new Dictionary<int, List<int>>();\n'
                '    private readonly Func<T, bool> conditionIsMet;\n'
                + ('    private readonly Func<T, bool> hasCondition;\n' if ctor_args else '')
                + '    public ' + name + '(Dictionary<int, Dictionary<int, T>> configs, Func<T, bool> conditionIsMet' + ctor_args + ')\n'
                '    {\n        this.configs = configs ?? throw new ArgumentNullException(nameof(configs));\n'
                '        this.conditionIsMet = conditionIsMet ?? throw new ArgumentNullException(nameof(conditionIsMet));'
                + ctor_assign + '\n        PostParse(configs);\n    }\n' + base_get + '\n' + body + '\n}\n}\n')
        path = target / (name + '.cs')
        path.write_text(text, 'utf8', newline='\n')
        meta = Path(str(path) + '.meta')
        if not meta.exists():
            guid = hashlib.md5(('zjcs:home-income-selection:' + name).encode()).hexdigest()
            meta.write_text('fileFormatVersion: 2\nguid: ' + guid +
                            '\nMonoImporter:\n  externalObjects: {}\n  serializedVersion: 2\n', 'utf8', newline='\n')
        for output in (path, meta):
            outputs[output.relative_to(ROOT).as_posix()] = digest(output)
    receipt = dict(manifest='884_156692', generator_sha256=digest(Path(__file__)), inputs=inputs, outputs=outputs,
                   adaptation='Original sorted indexes and selection branches. Generic row carrier and explicit condition callbacks replace parser inheritance and DataHub; original missing-key default lookup retained. No reward, condition or order values changed.')
    (ROOT / 'assets/reference/home-income-selection-source-manifest.json').write_text(
        json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')


if __name__ == '__main__':
    build()
