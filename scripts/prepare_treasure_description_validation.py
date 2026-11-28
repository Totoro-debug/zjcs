"""Reuse the binary-backed property oracle and compile unchanged client description methods."""
import argparse
import hashlib
import json
import re
from pathlib import Path
from official_source import ROOT, OfficialSource
from prepare_pet_score_chest_preview import original_method
from prepare_treasure_property_reference import prepare as prepare_reference


def sha(data):
    return hashlib.sha256(data).hexdigest()


def prepare(work, reference):
    work = work.resolve()
    if work.parent != ROOT/'output/validation' or not (work/'.zjcs-validation.json').is_file():
        raise ValueError('Use an owned validation directory')
    identity = json.loads((reference/'reference-inputs.json').read_bytes())
    receipt = json.loads((ROOT/'assets/reference/treasure-properties-source-manifest.json').read_bytes())
    runtime = ROOT/'unity/Assets/Game/Resources/Treasure/treasure-properties.json'
    if identity['manifest'] != OfficialSource().version or identity['binary_sha256'] != receipt['binary']['sha256'] or identity['runtime_payload_sha256'] != sha(runtime.read_bytes()):
        raise ValueError('Retained original table reference no longer matches the selected property payload')
    inputs = {}
    for name, expected in (('raw-tables.json', identity['raw_tables_sha256']),
                           ('reference-level-prop.g.bytes', identity['binary_sha256'])):
        data = (reference/name).read_bytes()
        if sha(data) != expected:
            raise ValueError('Original table reference changed: '+name)
        (work/name).write_bytes(data)
        inputs[(reference/name).relative_to(ROOT).as_posix()] = expected
    prepare_reference(work)
    code = OfficialSource().prepared_path/'official-code'
    original = code/'client-source/TreasureConfigUtils.cs'
    mode = code/'client-source/TreasurePropDisplayOption.cs'
    language = code/'common-source/Common/LanguageUtils.cs'
    body = re.sub(r'^using [^\n]+\n', '', original.read_text('utf-8-sig'), flags=re.M)
    key = original_method(language.read_text('utf-8-sig'), 'public static string GetTreasureFuncDescKey(')
    # Numeric and property formatting remain observable argument objects. The query
    # oracle itself keeps original globals, dictionaries, enum and provider calls.
    ports = '''
public static class CultureNumberArg
{ public static object Compact(long value) => new DescriptionNumber(value); }
public sealed class DescriptionNumber
{
    public readonly long Value;
    public DescriptionNumber(long value) { Value=value; }
    public override string ToString() => "number:"+Value;
}
public static class LanguageUtils
{
/*KEY*/
    public sealed class PropValue
    {
        public readonly PropType Type; public readonly long Value, Target;
        public readonly bool Unit, Desc; public bool Space;
        public PropValue(PropType type,long value,bool includeUnit=false,bool includePropDesc=false,long targetValue=-1)
        {Type=type;Value=value;Unit=includeUnit;Desc=includePropDesc;Target=targetValue;}
        public PropValue SetNeedSpaceBeforeValue(bool needSpaceBeforeValue) {Space=needSpaceBeforeValue;return this;}
        public override string ToString()=>"property:"+(int)Type+":"+Value+":"+Unit+":"+Desc+":"+Target+":"+Space;
    }
}
'''.replace('/*KEY*/', key)
    generated = ('using System.Collections.Generic;\nusing PropType=Zjcs.Official.HomeIncomeRules.PropType;\n'
                 'namespace TreasurePropertyOriginal {\n'+mode.read_text('utf-8-sig')+body+ports+'\n}\n')
    (work/'OriginalTreasureDescriptions.cs').write_text(generated, 'utf8', newline='\n')
    base = ROOT/'scripts/fixtures/treasure_properties_validation.cs'
    text = base.read_text('utf-8-sig').replace('public static class TreasurePropertiesValidation',
        'public static partial class TreasureDescriptionsValidation')
    text = text.replace('ZJCS_TREASURE_PROPERTIES_', 'ZJCS_TREASURE_DESCRIPTIONS_').replace('TREASURE_PROPERTIES_PASSED:', 'TREASURE_DESCRIPTIONS_PASSED:')
    old = 'CheckBinaryRows();CheckProperties();CheckBlessing();CheckSuits();CheckProviders();CheckNumericalBoundaries();CheckSavedPlayer();'
    if text.count(old) != 1:
        raise ValueError('Review binary oracle initialization boundary')
    text = text.replace(old, 'CheckDescriptions();')
    (work/'TreasureDescriptionsValidation.cs').write_text(text, 'utf8', newline='\n')
    inputs.update(OfficialSource().verify_code_files([original, mode, language]))
    for p in (Path(__file__), base, ROOT/'scripts/fixtures/treasure_descriptions_validation.cs'):
        inputs[p.relative_to(ROOT).as_posix()] = sha(p.read_bytes())
    (work/'description-reference-source.json').write_text(json.dumps(dict(inputs=inputs,
        oracle_sha256=sha(generated.encode())), indent=2)+'\n', 'utf8')
    print('Prepared unchanged treasure description oracle', flush=True)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--work', type=Path, required=True)
    parser.add_argument('--reference', type=Path, required=True)
    args = parser.parse_args()
    prepare(args.work, args.reference)
