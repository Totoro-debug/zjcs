"""Port the original treasure effect description query without coupling rules to UI globals."""
import argparse
import hashlib
import json
import re
from pathlib import Path

from official_source import OfficialSource, ROOT
from prepare_pet_score_chest_preview import original_method

DEST = ROOT/'unity/Assets/Game/Runtime/Official/TreasureCultivation/OfficialTreasureDescriptions.cs'
RECEIPT = ROOT/'assets/reference/treasure-descriptions-source-manifest.json'


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def prepare(check=False):
    source = OfficialSource()
    code = source.prepared_path/'official-code'
    paths = [code/'client-source/TreasureConfigUtils.cs',
             code/'client-source/TreasurePropDisplayOption.cs',
             code/'common-source/Common/LanguageUtils.cs']
    text = paths[0].read_text('utf-8-sig')
    body = '\n'.join(original_method(text, signature) for signature in (
        'public static (string Key, object[] Params) GenerateDesc(',
        'private static object[] GenerateTaskDesc(',
        'private static object[] GenerateDefaultDesc('))
    body = body.replace('public static ', 'public ').replace('private static ', 'private ')
    body = body.replace('this ITreasureEffect self', 'ITreasureEffect self')
    body = body.replace('LanguageUtils.GetTreasureFuncDescKey(', 'GetTreasureFuncDescKey(')
    body = body.replace('TreasureCalcPropData.CreateCalcPropDataByBaseLevel(baseLevel)',
                        'TreasureCalcPropData.CreateCalcPropDataByBaseLevel(Services.Tables, baseLevel)')
    body = body.replace('MeManager.MainPlayer', 'Services.Player').replace('PlanesInfoUtils.SeasonId', 'Services.SeasonId')
    body = body.replace('BattleFormula.Instance', 'calculator').replace('CultureNumberArg.Compact(', 'Services.Compact(')
    old = 'new LanguageUtils.PropValue(effectPropType, value, includeUnit: false, includePropDesc: true, -1L).SetNeedSpaceBeforeValue(needSpaceBeforeValue: true)'
    if body.count(old) != 2:
        raise ValueError('Review original description argument boundaries')
    body = body.replace(old, 'Services.PropertyValue(effectPropType, value, includeUnit: false, includePropDesc: true, targetValue: -1L, needSpaceBeforeValue: true)')
    key = original_method(paths[2].read_text('utf-8-sig'), 'public static string GetTreasureFuncDescKey(')
    mode = paths[1].read_text('utf-8-sig')
    raw = ('''// Original TreasureConfigUtils branches and argument order; explicit local owners replace globals.
using System;
using System.Collections.Generic;
using PropType = System.Int32;
namespace Zjcs.Official.TreasureProperties
{
    public interface ITreasureDescriptionServices
    {
        ITreasurePropertyTables Tables { get; }
        ITreasurePropertyPlayer Player { get; }
        int SeasonId { get; }
        object Compact(long value);
        object PropertyValue(int type, long value, bool includeUnit, bool includePropDesc,
            long targetValue, bool needSpaceBeforeValue);
    }
''' + mode + '''
    public sealed class TreasureDescriptionQuery
    {
        private readonly ITreasureDescriptionServices Services;
        private readonly TreasurePropertyCalculator calculator;
        private readonly Dictionary<int, long> sTmpPropsDict = new Dictionary<int, long>();
        public TreasureDescriptionQuery(ITreasureDescriptionServices services)
        {
            Services = services ?? throw new ArgumentNullException(nameof(services));
            calculator = new TreasurePropertyCalculator(services.Tables);
        }
''' + body + '\n' + key + '\n    }\n}\n').encode()
    outputs = {DEST: raw}
    token = hashlib.md5(('zjcs:treasure-descriptions:'+DEST.relative_to(ROOT).as_posix()).encode()).hexdigest()
    outputs[Path(str(DEST)+'.meta')] = ('fileFormatVersion: 2\nguid: '+token+'\nMonoImporter:\n  externalObjects: {}\n').encode()
    inputs = source.verify_code_files(paths)
    for path in (Path(__file__), ROOT/'scripts/prepare_pet_score_chest_preview.py',
                 ROOT/'unity/Assets/Game/Runtime/Official/TreasureCultivation/OfficialTreasureProperties.cs',
                 ROOT/'assets/reference/treasure-properties-source-manifest.json'):
        inputs[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
    record = dict(manifest=source.version, inputs=inputs,
                  adaptations=['Inject tables, current player and season, and the existing UI argument factories.',
                               'Keep the original null parameter array, provider override, task/default branches and argument order.',
                               'Keep scratch storage per query owner rather than across unrelated windows.'],
                  outputs={p.relative_to(ROOT).as_posix(): sha(data) for p, data in outputs.items()})
    prior = json.loads(RECEIPT.read_bytes()) if RECEIPT.exists() else {}
    outputs[RECEIPT] = (json.dumps(record, ensure_ascii=False, indent=2)+'\n').encode()
    for path, data in outputs.items():
        if check:
            if not path.is_file() or path.read_bytes() != data:
                raise ValueError('Treasure description source differs: '+str(path))
        else:
            if path != RECEIPT and path.exists() and path.read_bytes() != data and sha(path.read_bytes()) != prior.get('outputs', {}).get(path.relative_to(ROOT).as_posix()):
                raise ValueError('Preserve another description edit: '+str(path))
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(data)
    print('Verified original treasure description branches', flush=True)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    prepare(parser.parse_args().check)
