"""Migrate selected official hang-up arithmetic without algebraic rewrites."""
import hashlib
import json
from pathlib import Path
import re

from official_apk import ROOT
from prepare_home_auto_increase_runtime import method

CODE = ROOT / '.tools/source-packages/current/884_156692/official-code'
DEST = ROOT / 'unity/Assets/Game/Runtime/Official/HomeIncomeRules'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def build():
    inputs, outputs = {}, {}

    def read(name):
        path = CODE / 'common-source/Common' / (name + '.cs')
        inputs[path.relative_to(ROOT).as_posix()] = sha(path)
        return path.read_text('utf-8-sig').replace('\r\n', '\n')

    def write(path, text, folder=False):
        path.parent.mkdir(parents=True, exist_ok=True)
        if not folder:
            path.write_text(text, 'utf8', newline='\n')
            outputs[path.relative_to(ROOT).as_posix()] = sha(path)
        meta = Path(str(path) + '.meta')
        if not meta.exists():
            guid = hashlib.md5(('zjcs:home-income-formula:' + path.relative_to(ROOT).as_posix()).encode()).hexdigest()
            importer = 'folderAsset: yes\nDefaultImporter:\n' if folder else (
                'AssemblyDefinitionImporter:\n' if path.suffix == '.asmdef' else
                'MonoImporter:\n  serializedVersion: 2\n')
            meta.write_text('fileFormatVersion: 2\nguid: ' + guid + '\n' + importer + '  externalObjects: {}\n',
                            'utf8', newline='\n')
        outputs[meta.relative_to(ROOT).as_posix()] = sha(meta)

    def emit(name, body):
        write(DEST / (name + '.cs'), '// Selected official Console.dll; parser/player boundaries only.\n'
              'using System;\nusing System.Collections.Generic;\nnamespace Zjcs.Official.HomeIncomeRules\n{\n' + body + '\n}\n')

    receipt = ROOT / 'assets/reference/official-client-code/source-manifest.json'
    proof = next(row for row in json.loads(receipt.read_bytes())['projects']
                 if row['assembly'] == 'Console.dll' and row['folder'] == 'common-source')
    assembly = CODE / 'managed/decoded/Console.dll'
    assert sha(assembly) == proof['source']['decoded_sha256']
    for path in (receipt, assembly):
        inputs[path.relative_to(ROOT).as_posix()] = sha(path)
    helper = ROOT / 'scripts/prepare_home_auto_increase_runtime.py'
    inputs[helper.relative_to(ROOT).as_posix()] = sha(helper)
    for name in ('PropType', 'EFurniture'):
        emit(name, read(name).split('namespace Common;')[1].strip())

    models = []
    for name, fields in (
        ('HangUpInfo', [('EFurniture', 'BindFurniture'), ('PropType', 'EffectPercentPropType'),
                        ('PropType', 'EffectFixedPropType'), ('PropType', 'FinallyEffectPercentPropType')]),
        ('HangUpRewardInfo', [('float', 'HourlyOutput')]), ('PropCfgInfo', [('bool', 'Float')])):
        source = read(name)
        for kind, field in fields:
            assert re.search(r'public readonly ' + kind + ' ' + field + ';', source), (name, field)
        models.append('public sealed class ' + name + '\n{\n' + ''.join(
            '    public ' + kind + ' ' + field + ';\n' for kind, field in fields) + '}')
    emit('HomeIncomeModels', '\n'.join(models))

    source = read('BattleFormulaHandler')
    signatures = (
        'public void GetHangUpProp(', 'public void GetExtraHangUpProp(',
        'public double GetProp(Dictionary<PropType, long> props, PropType type)')
    bodies = '\n'.join(method(source, signature) for signature in signatures)
    bodies = bodies.replace('Dictionary<PropType, long> props', 'IReadOnlyDictionary<int, long> props')
    bodies = bodies.replace('Parsers.Instance.PropCfgInfoParser.Get(type)', 'configuration(type)')
    bodies = bodies.replace('props.TryGetValue(type,', 'props.TryGetValue((int)type,')
    bodies += '\n' + method(read('Formula'), 'public static double Long2Double(long val)')
    emit('BattleFormulaHandler', 'public sealed class BattleFormulaHandler\n{\n'
         '    private readonly Func<PropType, PropCfgInfo> configuration;\n'
         '    public BattleFormulaHandler(Func<PropType, PropCfgInfo> configuration)\n'
         '    { this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration)); }\n'
         + bodies + '\n}')

    source = read('HangUpUtils')
    body = method(source, 'public static double CalcRewardCount(this Player player, HangUpInfo hangUpInfo, double hangUpHour, double scaledNum)')
    body = body.replace('this Player player, HangUpInfo hangUpInfo,',
                        'HangUpInfo hangUpInfo, HangUpRewardInfo hangUpRewardInfo, BattleFormulaHandler formula, IReadOnlyDictionary<int, long> props,')
    body = body.replace('\t\tHangUpRewardInfo hangUpRewardInfo = Parsers.Instance.HangUpRewardInfoParser.Get(hangUpInfo.RewardId, player);\n', '')
    body = body.replace('BattleFormula.Instance.GetHangUpProp(hangUpInfo, player.NonBattleProps,',
                        'formula.GetHangUpProp(hangUpInfo, props,')
    assert 'Parsers.' not in body and 'player.' not in body
    unsettled = method(source, 'public static long GetUnsettledRewardCount(this Player player, int hangUpId, int level, out double remainderSeconds)')
    time_conversion = method(read('TimeUtils'), 'public static double SecondToHour(double sec)')
    assert 'return sec / 3600.0;' in time_conversion
    arithmetic = unsettled[unsettled.index('\t\tdouble num3 ='):] if '\t\tdouble num3 =' in unsettled else unsettled[unsettled.index('\t\t\tdouble num3 ='):]
    arithmetic = arithmetic[:arithmetic.index('\n\t\t}')]
    arithmetic = arithmetic.replace('player.CalcRewardCount(hangUpInfo, furnitureLevelRuleInfo.LevelNum, scaledNum)',
        'CalcRewardCount(hangUpInfo, hangUpRewardInfo, formula, props, levelNum, scaledNum)')
    award = method(source, 'public static long GetHangUpAwardCount(this Player player, int hangUpId, out double remainderSeconds)')
    clamp = award[award.index('\t\tlong val ='):award.rindex('\n\t}')]
    clamp = re.sub(r'\n\t\tlong hangUpMaxRewardStore =[^\n]+', '', clamp)
    body += ('\n\t// Original GetUnsettledRewardCount arithmetic after unlock and row lookup.\n'
        '\tpublic static long CalcUnsettledRewardCount(HangUpInfo hangUpInfo, HangUpRewardInfo hangUpRewardInfo, '
        'BattleFormulaHandler formula, IReadOnlyDictionary<int, long> props, double levelNum, '
        'double elapsedSeconds, double lastNotSettleSeconds, double extraRewardSeconds, out double remainderSeconds)\n'
        '\t{\n\t\tdouble num2 = elapsedSeconds + lastNotSettleSeconds + extraRewardSeconds;\n'
        '\t\tdouble scaledNum = num2 / 3600.0;\n\t\tlong num = 0L;\n' + arithmetic
        + '\n\t\treturn num;\n\t}\n'
        '\tpublic static long ClampAwardCount(long settledRewardCount, long unsettledRewardCount, long hangUpMaxRewardStore)\n'
        '\t{\n' + clamp + '\n\t}')
    emit('HangUpUtils', 'public static class HangUpUtils\n{\n' + body + '\n}')

    path = ROOT / 'unity/Assets/Official/Partners/partner-property-ui.json'
    inputs[path.relative_to(ROOT).as_posix()] = sha(path)
    property_proof = ROOT / 'assets/reference/partner-property-ui-source-manifest.json'
    upstream = json.loads(property_proof.read_bytes())
    assert upstream['manifest'] == '884_156692' and upstream['output']['path'] == path.relative_to(ROOT).as_posix()
    assert upstream['output']['sha256'] == sha(path)
    inputs[property_proof.relative_to(ROOT).as_posix()] = sha(property_proof)
    payload = json.loads(path.read_bytes())
    assert payload['manifest'] == '884_156692'
    rows = payload['prop_cfg']
    assert len({row['PropType'] for row in rows}) == len(rows)
    emit('HomeIncomePropertyCatalog', 'public static class HomeIncomePropertyCatalog\n{\n'
         '    private static readonly Dictionary<PropType, PropCfgInfo> rows = new Dictionary<PropType, PropCfgInfo>\n    {\n'
         + ''.join('        { (PropType)' + str(row['PropType']) + ', new PropCfgInfo { Float = '
                   + str(row['Float']).lower() + ' } },\n' for row in rows) + '    };\n'
         '    public static PropCfgInfo Get(PropType type) => rows.TryGetValue(type, out var row) ? row : null;\n'
         '    public static ISet<int> CreateFloatPropertyIds()\n    {\n'
         '        var result = new HashSet<int>();\n'
         '        foreach (var row in rows) if (row.Value.Float) result.Add((int)row.Key);\n'
         '        return result;\n    }\n}')
    write(DEST / 'Zjcs.Official.HomeIncomeRules.asmdef', json.dumps(dict(
        name='Zjcs.Official.HomeIncomeRules', rootNamespace='Zjcs.Official.HomeIncomeRules',
        references=[], autoReferenced=False, noEngineReferences=True), indent=2) + '\n')
    write(DEST, '', folder=True)
    manifest = dict(manifest='884_156692', generator_sha256=sha(Path(__file__)), inputs=inputs,
                    outputs=outputs, adaptation='Original arithmetic, float casts and execution order; explicit reward row, property dictionary and property parser boundaries. Original unsettled-preview argument order, truncation, fractional seconds and award clamp retained. Full original enums; selected prop_cfg flags. Offline checkpoint and award transactions remain separate.')
    (ROOT / 'assets/reference/home-income-formula-source-manifest.json').write_text(
        json.dumps(manifest, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    print('Recovered original income formula:', len(outputs), 'outputs;', len(rows), 'property definitions')


if __name__ == '__main__':
    build()
