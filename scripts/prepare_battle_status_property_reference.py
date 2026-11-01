"""Compile selected original methods against explicit, local validation adapters."""
import argparse
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
CURRENT = ROOT / '.tools/source-packages/current/884_156692'
SOURCE = CURRENT / 'official-code/common-source/Common'
USED = set()


def original(name):
    path = SOURCE / (name + '.cs')
    USED.add(path)
    return path.read_text(encoding='utf-8-sig')


def method(name, signature):
    source = original(name)
    start = source.index(signature)
    opening = source.index('{', start)
    depth = 1
    end = opening + 1
    # These selected method bodies contain no braces in string literals/comments.
    while depth:
        depth += (source[end] == '{') - (source[end] == '}')
        end += 1
    return source[start:end]


def identity(path):
    raw = path.read_bytes()
    return dict(path=path.relative_to(ROOT).as_posix(), bytes=len(raw),
                sha256=hashlib.sha256(raw).hexdigest())


def write(path, value):
    with path.open('r+b' if path.exists() else 'wb') as stream:
        stream.write(value.encode('utf-8'))
        stream.truncate()


ADAPTERS = r'''
public enum ValueType { Long, Float }
public enum SubRank { Norank }
public sealed class PropCfgInfo { public bool Float; }
public sealed class PropParser {
    public Dictionary<PropType, PropCfgInfo> Rows = new Dictionary<PropType, PropCfgInfo>();
    public PropCfgInfo Get(PropType type) { Rows.TryGetValue(type, out var row); return row; }
}
public sealed class EntityProp { public Dictionary<PropType, long> Raw; }
public sealed class EntityParser {
    public Dictionary<int, EntityProp> Rows = new Dictionary<int, EntityProp>();
    public EntityProp Get(int id) { Rows.TryGetValue(id, out var row); return row; }
}
public sealed class Parsers {
    public static Parsers Instance = new Parsers();
    public PropParser PropCfgInfoParser = new PropParser();
    public EntityParser EntityPropParser = new EntityParser();
}
public sealed class EcContainer {
    public EcEntity RootEntity;
    public Dictionary<int, EcEntity> Entities = new Dictionary<int, EcEntity>();
}
public sealed class EcEntity {
    public int Id, ClassId; public EcContainer Container;
    public PropComponent Prop; public FightRoleFightComponent Role;
    public T GetComponent<T>() where T : class {
        if (typeof(T) == typeof(PropComponent)) return Prop as T;
        if (typeof(T) == typeof(FightRoleFightComponent)) return Role as T;
        if (typeof(T) == typeof(FightComponent)) return new FightComponent() as T;
        throw new NotSupportedException(typeof(T).Name);
    }
}
public sealed class FightComponent {
    public bool IsPvpStage;
    public BattleFormula.ISkillPropsScaleOnBattleProcessor SkillPropsScaleOnBattleProcessor;
}
public sealed class FightRoleFightComponent { public long Hp; }
public sealed class PropComponent { public Dictionary<PropType, long> FinalProps; }
public sealed class FightSourceInfo {
    public int Id = 1;
    public Dictionary<PropType, long> Props;
    public Dictionary<int, Dictionary<PropType, double>> SkillBlessPropScaleFactors;
    public int GetSkillRank() { return 1; }
    public int GetSkillLevel() { return 1; }
    public SubRank GetSubRank() { return SubRank.Norank; }
}
public sealed class FightStatusComponent {
    public FightStatusComponentInfo Info = new FightStatusComponentInfo();
    public int StackedCount;
    public Dictionary<PropType, long> StatusConfigProps, StatusProps, Props;
    public Dictionary<PropType, KeyValuePair<long, ValueType>> ReplaceProps;
    public FightSourceInfo SourceInfo; public EcEntity Entity, ApplyEntity;
    public object ShareStackComponent;
    public ApplyStatusAgent ApplyStatusAgent;
    public Action<FightStatusComponent, int, int> OnStatusStackUpdate = (s, a, b) => {};
}
public sealed class ApplyStatusAgent {
    public void FireUpdateStatusStack(FightStatusComponent s, int a, int b) {}
}
public sealed class FightStatusPropComponentInfo {
    public List<FightStatusPropCfg> TransitionPropCfgs = new List<FightStatusPropCfg>();
}
public static partial class FightStatusUtils {
    public static void GetStatusProps(FightStatusComponent status, ref Dictionary<PropType, long> result) {
        GetStatusProps(status.Entity.ClassId, status.ApplyEntity, status.SourceInfo,
            status.ApplyEntity.Prop.FinalProps, ref result);
    }
    public static EcEntity GetStatusTargetByType(EcContainer container, int creator, int applicator, StatusTargetType type) {
        int id;
        if (type == StatusTargetType.Creator) id = creator;
        else if (type == StatusTargetType.Applicator) id = applicator;
        else throw new NotSupportedException("Reference fixture only supplies creator/applicator targets.");
        container.Entities.TryGetValue(id, out var entity); return entity;
    }
}
// Logging adapters are reached only on invalid null-destination inputs, which these
// comparisons do not supply. They retain the original methods' complete branches.
public struct GLoggerWarnInterpolatedStringHandler {
    public GLoggerWarnInterpolatedStringHandler(int a, int b, out bool enabled) { enabled = false; }
    public void AppendLiteral(string value) {}
}
public struct GLoggerErrorInterpolatedStringHandler {
    public GLoggerErrorInterpolatedStringHandler(int a, int b, out bool enabled) { enabled = false; }
    public void AppendLiteral(string value) {}
}
public static class Log {
    public static void Warn(ref GLoggerWarnInterpolatedStringHandler h, object a, string b, string c, int d) {}
    public static void Error(ref GLoggerErrorInterpolatedStringHandler h, object a, string b, string c, int d) {}
}
public sealed partial class BattleFormula {
    public static BattleFormula Instance = new BattleFormula();
    public interface ISkillPropsScaleOnBattleProcessor {}
    public struct PropCalculateParameter {
        public long TargetCurrentHp;
        public PropCalculateParameter(long hp) { TargetCurrentHp = hp; }
    }
    // Skill-growth arithmetic is checked separately. This adapter supplies its
    // result so the original status pipeline can be tested independently.
    public Dictionary<PropType, long> CalcSkillPropsOnBattle(EntityProp prop, int rank, int level,
        bool pvp, SubRank subRank, ISkillPropsScaleOnBattleProcessor processor) {
        return new Dictionary<PropType, long>(prop.Raw);
    }
}
public sealed partial class StatusImplementation {
    public FightStatusComponent Component;
    public void Initialize() { InitProps(); }
    private bool TryChangeShareStack(int delta, out int real) {
        throw new NotSupportedException("Shared-stack arbitration is outside this comparison.");
    }
}
public sealed class ReferenceState {
    private StatusImplementation implementation;
    private FightStatusPropComponentInfo propInfo;
    private bool hasPropertyComponent;
    public Dictionary<int, long> Config, Status, Final;
    private static Dictionary<PropType, long> Typed(Dictionary<int, long> raw) {
        var result = new Dictionary<PropType, long>();
        foreach (var p in raw) result.Add((PropType)p.Key, p.Value); return result;
    }
    private static Dictionary<int, long> Raw(Dictionary<PropType, long> props) {
        var result = new Dictionary<int, long>();
        foreach (var p in props) result.Add((int)p.Key, p.Value); return result;
    }
    public static void Configure(int[] ids, int[] floats) {
        var set = new HashSet<int>(floats);
        Parsers.Instance.PropCfgInfoParser.Rows.Clear();
        foreach (int id in ids) Parsers.Instance.PropCfgInfoParser.Rows.Add((PropType)id, new PropCfgInfo { Float = set.Contains(id) });
    }
    public static ReferenceState Create(int entityId, bool entityProps, bool propertyComponent,
        bool open, bool scale, int count, long hp, Dictionary<int, long> raw,
        Dictionary<int, long> source, Dictionary<int, long> target,
        Dictionary<int, long> replacements, Dictionary<int, double> blessings, int[][] transitions) {
        var container = new EcContainer();
        var owner = new EcEntity { Id = 2, Container = container,
            Prop = new PropComponent { FinalProps = Typed(target) }, Role = new FightRoleFightComponent { Hp = hp } };
        container.RootEntity = owner; container.Entities.Add(2, owner);
        container.Entities.Add(1, new EcEntity { Id = 1, Container = container, Prop = new PropComponent { FinalProps = Typed(source) } });
        if (entityProps) Parsers.Instance.EntityPropParser.Rows[entityId] = new EntityProp { Raw = Typed(raw) };
        else Parsers.Instance.EntityPropParser.Rows.Remove(entityId);
        var replace = new Dictionary<PropType, KeyValuePair<long, ValueType>>();
        foreach (var p in replacements) replace.Add((PropType)p.Key, new KeyValuePair<long, ValueType>(p.Value, ValueType.Long));
        var bless = new Dictionary<PropType, double>();
        foreach (var p in blessings) bless.Add((PropType)p.Key, p.Value);
        var component = new FightStatusComponent { Entity = new EcEntity { ClassId = entityId }, ApplyEntity = owner,
            StackedCount = count, ReplaceProps = replace,
            Info = new FightStatusComponentInfo { IsOpenStack = open, ScalePropWhenChangeStacked = scale },
            SourceInfo = new FightSourceInfo { Props = Typed(source),
                SkillBlessPropScaleFactors = new Dictionary<int, Dictionary<PropType, double>> { { entityId, bless } } } };
        var result = new ReferenceState { implementation = new StatusImplementation { Component = component },
            propInfo = new FightStatusPropComponentInfo(), hasPropertyComponent = propertyComponent };
        foreach (var t in transitions) result.propInfo.TransitionPropCfgs.Add(new FightStatusPropCfg {
            Prop = (PropType)t[0], Target = (StatusTargetType)t[1], TargetProp = (PropType)t[2],
            AdditionProp = (PropType)t[3], FixedAdditionProp = (PropType)t[4] });
        result.implementation.Initialize(); result.Snapshot(); return result;
    }
    public void SetStackCount(int count) { implementation.SetStackedCount(count); Snapshot(); }
    private void Snapshot() {
        var s = implementation.Component;
        Config = Raw(s.StatusConfigProps); Status = Raw(s.StatusProps);
        var result = new Dictionary<PropType, long>();
        if (hasPropertyComponent) StatusProjection.GetProps(propInfo, s.ApplyEntity,
            s.ApplyEntity.Prop.FinalProps, s.SourceInfo, s.StatusProps,
            s.Info.CanStackCount() ? s.StackedCount : 0, ref result);
        else result = s.StatusProps;
        Final = Raw(result);
    }
}
'''


def generate():
    parts = ['using System;\nusing System.Collections.Generic;\nnamespace SourceStatusPropertyReference {']
    for name in ('PropType', 'StatusTargetType'):
        parts.append(original(name).replace('namespace Common;', ''))
    parts.append(ADAPTERS)
    parts.append('public sealed partial class BattleFormula {')
    for signature in (
        'public void HandleAndAdditionStatusProps(',
        'public Dictionary<PropType, long> HandleStatusStackedProps(',
        'public double GetProp(Dictionary<PropType, long> props, PropType type)',
        'public long GetRawProp(Dictionary<PropType, long> props, PropType type)',
        'public void AddProp(Dictionary<PropType, long> props, PropType propType, long value)',
        'public void ScaleProps(Dictionary<PropType, long> props, int scale, Dictionary<PropType, long> result)',
        'public double Long2Double(long val)',
        'public void MergeProps(IReadOnlyDictionary<PropType, long> props1,',
        'public void MergePropsInPlace(Dictionary<PropType, long> dest, IReadOnlyDictionary<PropType, long> add)'):
        parts.append(method('BattleFormulaHandler', signature))
    parts.append('}\npublic static class Formula {')
    parts.append(method('Formula', 'public static double Long2Double(long val)'))
    parts.append('}\npublic sealed class FightStatusComponentInfo { public bool IsOpenStack, ScalePropWhenChangeStacked;')
    parts.append(method('FightStatusComponentInfo', 'public bool CanStackCount()'))
    parts.append('}\npublic sealed partial class StatusImplementation {')
    for signature in ('private void InitProps()', 'public void SetStackedCount(int count)'):
        parts.append(method('FightStatusComponentImplement', signature))
    parts.append('}\npublic static partial class FightStatusUtils {')
    # The method itself is unchanged except the static wrapper needed by callers.
    parts.append(method('FightStatusUtilsHandler', 'public void GetStatusProps(int statusEntityClassId,').replace('public void ', 'public static void ', 1))
    parts.append('}\npublic sealed class FightStatusPropCfg { public PropType Prop, TargetProp, AdditionProp, FixedAdditionProp; public StatusTargetType Target;')
    parts.append(method('FightStatusPropCfg', 'public void CalcAndAdditionProp(EcContainer container,'))
    parts.append('}\npublic static class StatusProjection {')
    parts.append(method('FightStatusPropBaseComponent', 'public static void GetProps(FightStatusPropComponentInfo info,'))
    parts.append('}\n}')
    return '\n'.join(parts) + '\n'


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--output', type=Path)
    parser.add_argument('--record', action='store_true')
    args = parser.parse_args()
    code = json.loads((CURRENT / 'code-manifest.json').read_bytes())
    assembly = CURRENT / 'official-code/managed/decoded/Console.dll'
    assert code['manifest'] == '884_156692'
    assert identity(assembly)['sha256'] == code['assemblies']['Console.dll']['decoded_sha256']
    reference = generate()
    if args.output:
        output = args.output.resolve()
        assert output.parent == ROOT / 'output/validation' and (output / '.zjcs-validation.json').is_file()
        write(output / 'status-property-reference.cs', reference)
    if args.record:
        runtime = ROOT / 'unity/Assets/Game/Runtime'
        adapters = [runtime / p for p in (
            'Official/Combat/FightOfficialCombatCatalog.cs', 'Official/Combat/FightBattleState.cs',
            'Official/Combat/FightStatusPropertyRuntime.cs', 'Official/Combat/FightStatusPropertyRuntime.Initialization.cs',
            'Official/Combat/FightStatusLifecycle.cs', 'Offline/Combat/FightStatusPropertyModifiers.cs',
            'Offline/Combat/FightBattleExecutor.Replay.cs')]
        proof = dict(code_manifest='884_156692', configuration_manifest='854_156383',
            code_receipt=identity(CURRENT / 'code-manifest.json'), assembly=identity(assembly),
            original=[identity(p) for p in sorted(USED)], adapter=[identity(p) for p in adapters],
            input=[identity(ROOT / ('unity/Assets/Official/Combat/' + p)) for p in ('skill-rules.json', 'battle-rules.json', 'rules.json')],
            validation=identity(ROOT / 'scripts/test_battle_status_properties.ps1'),
            regression=[identity(ROOT / ('scripts/' + p)) for p in (
                'test_battle_status_application.ps1', 'test_battle_trigger_chains.ps1',
                'test_battle_report_averages.ps1', 'test_battle_report_shields.ps1')],
            play_mode=identity(ROOT / 'unity/Assets/Game/Editor/HomeBattleHostPlayModeAcceptance.StatusReplay.cs'),
            generator=identity(Path(__file__).resolve()),
            generated_reference_sha256=hashlib.sha256(reference.encode()).hexdigest(),
            operation='Run original InitProps, SetStackedCount, GetStatusProps, property projection and shared formulas '
                'against explicit local adapters. Compare initialization, raw replacement, stack/blessing order, '
                'zero-stack behavior, property transitions, shield conversion and checkpoint continuation. '
                'Adapters supply precomputed skill growth, local entities/props and logging; shared-stack arbitration '
                'and PVP scaling are outside this comparison.')
        write(ROOT / 'assets/reference/battle-rules/status-properties-source-manifest.json', json.dumps(proof, ensure_ascii=False, indent=2) + '\n')
    print('Prepared original status property methods and explicit validation adapters.')


if __name__ == '__main__':
    main()
