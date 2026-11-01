"""Compile original equipment selection methods with explicit local adapters."""
import argparse
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / '.tools/source-packages/current/884_156692/official-code/common-source/Common'


def method(name, signature):
    source = (SOURCE / (name + '.cs')).read_text(encoding='utf-8-sig')
    start = source.index(signature)
    opening = source.index('{', start)
    depth, end = 1, opening + 1
    while depth:
        depth += (source[end] == '{') - (source[end] == '}')
        end += 1
    return source[start:end]


ADAPTERS = r'''
public enum Rank { None }
public enum SubRank { None }
public enum Quality { None }
public enum PropType { None }
public enum ValueType { Long, Float }
public enum EquipAttributeType { None, Normal, SingleProp, MultipleProp, SpecialProp, PassiveSkill }
public enum AttributeImplType { Prop, PassiveSkill }
public class EquipPropBase { public int PropId; public AttributeImplType ImplementationType; }
public sealed class EquipSpecialPropValue : EquipPropBase {
    public int PropGroupIndex;
    public EquipSpecialPropValue(EquipAttributeType type, int id, int index) {
        PropId = id; PropGroupIndex = index; ImplementationType = AttributeImplType.Prop;
    }
}
public sealed class EquipPassiveSkillPropValue : EquipPropBase {
    public List<int> PropGroupIndexs; public Quality SkillQuality;
    public EquipPassiveSkillPropValue(EquipAttributeType type, int id, List<int> indices, Quality quality) {
        PropId = id; PropGroupIndexs = indices; SkillQuality = quality;
        ImplementationType = AttributeImplType.PassiveSkill;
    }
}
public sealed class EquipAttributeInfo {
    public int Id, ExclusiveGroup; public bool SelfExclusive, IsApplyInBattle;
    public EquipAttributeType Type; public Dictionary<PropType, long> EquivalentProps;
}
public sealed class EquipAttributesPassiveSkillInfo {
    public int Id; public List<EquipAttributeConfig> Props, PropsFactors;
    public List<Quality> Qualitys; public List<long> QualityFactors;
    public Dictionary<PropType, int> PropBlessScaleFactorIds;
    public Dictionary<int, Dictionary<PropType, int>> SkillPropMapFactorIds;
}
public sealed class EquipSuitInfo { public Dictionary<int, int> SuitAttributes; }
public sealed class Parser<T> where T : class {
    public Dictionary<int, T> Rows = new Dictionary<int, T>();
    public T Get(int id) { Rows.TryGetValue(id, out var value); return value; }
}
public partial class EquipAttributeInfoParser {
    public Dictionary<int, EquipAttributeInfo> Rows = new Dictionary<int, EquipAttributeInfo>();
    public EquipAttributeInfo Get(int id) { Rows.TryGetValue(id, out var value); return value; }
}
public sealed class EquipAttributeBlessScaleInfoParser {
    public Func<int, Rank, int, double> Resolve;
    public bool TryGetEquipAttributeBlessScale(int id, Rank rank, int level, out double factor) {
        factor = Resolve(id, rank, level); return factor != 1d;
    }
}
public sealed class Parsers {
    public static Parsers Instance = new Parsers();
    public EquipAttributeInfoParser EquipAttributeInfoParser = new EquipAttributeInfoParser();
    public Parser<EquipAttributesPropsInfo> EquipAttributesPropsInfoParser = new Parser<EquipAttributesPropsInfo>();
    public Parser<EquipAttributesPassiveSkillInfo> EquipAttributesPassiveSkillInfoParser = new Parser<EquipAttributesPassiveSkillInfo>();
    public Parser<EquipSuitInfo> EquipSuitInfoParser = new Parser<EquipSuitInfo>();
    public EquipAttributeBlessScaleInfoParser EquipAttributeBlessScaleInfoParser = new EquipAttributeBlessScaleInfoParser();
}
public sealed class ItemInfo { public Rank LimitRank; }
public sealed class ItemData { public ItemInfo Info; }
public sealed class PlayerItemEquipPropBaseWrap { public int PropId => Data.PropId; public EquipPropBase Data; }
public sealed class ItemParamEquip {
    public int EquipSuitId, BlessLevel;
    public Dictionary<int, PlayerItemEquipPropBaseWrap> OtherProps = new Dictionary<int, PlayerItemEquipPropBaseWrap>();
}
public sealed class PlayerItemDataWrap {
    public long InstanceId; public ItemInfo Info; public ItemData Data; public ItemParamEquip Param;
    public ItemParamEquip GetItemParamEquip() { return Param; }
}
public sealed class CombatRatingRefDataBase {
    public Func<Dictionary<PropType, long>, long> Common;
}
public static class Formula { public static double Long2Double(long value) { return value / 10000d; } }
public static class BattleFormula { public static BattleFormulaHandler Instance = new BattleFormulaHandler(); }
public partial class BattleFormulaHandler {
    public long CalcCombatRatingCommon(int level, SubRank rank, Dictionary<PropType, long> values,
        CombatRatingRefDataBase reference) { return reference.Common(values); }
    public void MergePropsInPlace(Dictionary<PropType, long> result, Dictionary<PropType, long> values) {
        foreach (var pair in values) { result.TryGetValue(pair.Key, out var old); result[pair.Key] = old + pair.Value; }
    }
}
// Logging does not participate in selection. Preserve the original error branches.
public struct GLoggerErrorInterpolatedStringHandler {
    public GLoggerErrorInterpolatedStringHandler(int a, int b, out bool enabled) { enabled = false; }
    public void AppendLiteral(string text) {}
    public void AppendFormatted<T>(T value, int width, string format, string expression) {}
}
public struct GLoggerWarnInterpolatedStringHandler {
    public GLoggerWarnInterpolatedStringHandler(int a, int b, out bool enabled) { enabled = false; }
    public void AppendLiteral(string text) {}
    public void AppendFormatted<T>(T value, int width, string format, string expression) {}
}
public static class Log {
    public static void Error(ref GLoggerErrorInterpolatedStringHandler message, object unused,
        string name, string path, int line) {}
    public static void Warn(ref GLoggerWarnInterpolatedStringHandler message, object unused,
        string name, string path, int line) {}
}
'''


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    parts = ['using System; using System.Collections.Generic;\nnamespace EquipmentSelectionReference {', ADAPTERS]
    parts += ['public partial class BattleFormulaHandler {']
    for signature in ('public void CheckEquipsAllEffectTerm(',
                      'private void CheckTermJoinEffective(', 'private long GetEquipTermCombatRating('):
        parts.append(method('BattleFormulaHandler', signature))
    parts += ['}', 'public partial class EquipAttributeInfoParser {',
              method('EquipAttributeInfoParser', 'public EquipPropBase GetSuitProps('), '}',
              'public sealed class EquipAttributeConfig { public ValueType Type; public List<long> Values;',
              method('EquipAttributeConfig', 'public double GetFloatValue('), '}',
              'public sealed class EquipAttributesPropsInfo { public List<Dictionary<PropType, long>> Props; public int PropBlessScaleFactorId;',
              method('EquipAttributesPropsInfo', 'public void GetEquipAttributeProps(int groupIndex, Rank planesRank, Rank equipRank, int blessLevel, ref Dictionary<PropType, long> totalPropsResult,'),
              '}', '}']
    args.output.write_text('\n'.join(parts) + '\n', encoding='utf-8', newline='\n')
    print('Prepared verbatim official selection, exclusivity, scoring, suit, property and factor methods.')


if __name__ == '__main__':
    main()
