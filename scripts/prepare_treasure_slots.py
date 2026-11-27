"""Extract original treasure slot storage, queries, swaps and active-plan writeback."""
import argparse
import hashlib
import json
import re
from pathlib import Path

from official_apk import Apk, ROOT
from official_source import OfficialSource
from official_tables import Constructors
from prepare_pet_score_chest_preview import original_method

DEST = ROOT / 'unity/Assets/Game/Runtime/Official/TreasureCultivation/OfficialTreasureSlots.cs'
DATA = ROOT / 'unity/Assets/Game/Resources/Treasure/treasure-slots.json'
RECEIPT = ROOT / 'assets/reference/treasure-slots-source-manifest.json'
TEMPLATE = ROOT / 'scripts/templates/treasure_cultivation/TreasureSlotPorts.cs'
METHODS = {
    'PlayerTreasureSlotItemDataWrap': ('public int GetFinalLevel(', 'public int AddBaseLevel(', 'public int AddBlessLevel('),
    'PlayerTreasureElementSlotsDataWrap': ('public int GetShareLevel(', 'public int GetBaseShareLevel(',
        'public void SetShareLevel(', 'public void SetBaseShareLevel(', 'public void InsertTreasureToSlot(',
        'public PlayerTreasureSlotItemDataWrap GetTreasureSlotByIndex(', 'public void UpdateTreasureSlotsShareLevel('),
    'PlayerTreasureSlotDataWrap': ('public PlayerTreasureSlotItemDataWrap GetTreasureInSlots(',
        'public int GetElementTreasureShareLevel(', 'public int GetElementTreasureBaseShareLevel(',
        'public PlayerTreasureElementSlotsDataWrap GetElementSlotData(', 'public void ClearAllBlessLevel(',
        'public bool SetupTreasureInSlot(', 'public bool RemoveTreasureInSlot('),
    'Player': ('public int GetTreasureFinalLevel(', 'public int GetTreasureBaseLevel(',
        'public int GetTreasureSlotFinalLevel(', 'public int GetSlotTreasureBaseLevel(',
        'public PlayerTreasureElementSlotsDataWrap GetTreasureElementSlotData(',
        'public int GetTreasureInSlotIndex(', 'public PlayerTreasureSlotItemDataWrap GetTreasureSlotDataAndIndex(',
        'public PlayerTreasureSlotItemDataWrap GetTreasureSlotData(ElementType',
        'public PlayerTreasureSlotItemDataWrap GetTreasureSlotData(int', 'public bool IsTreasureInSlot(',
        'public bool SetupTreasureInSlot(', 'public bool RemoveTreasureInSlot(',
        'public int TreasureSlotBaseLevelUp(', 'private void UpdateTreasureSlotShareLevel(',
        'public int GetTreasureSlotShareLevel(', 'public int GetSlotTreasureLevel(',
        'public int GetCanSetupTreasureSlotIndex(', 'public void FireOnSetupTreasureSlot('),
    'PlayerTreasurePlanExt': ('public static void WriteWornTreasureElementToActivePlan(',
        'private static bool TryGetWornTreasureSlots(', 'private static int GetWornTreasureClassId('),
    'TreasureHelper': ('public static bool CanSetupTreasureSlot(',),
}


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def adapted(body, owner):
    # Logging is the only non-data infrastructure in these original methods.
    body = body.replace('GLoggerErrorInterpolatedStringHandler message;\n\t\tbool enabled;', '')
    pattern = r'(?:GLoggerErrorInterpolatedStringHandler )?message = new GLoggerErrorInterpolatedStringHandler\(.*?Log.Error\(ref message,.*?\);'
    def logging(match):
        literals = re.findall(r'message.AppendLiteral\(("(?:[^"\\]|\\.)*")\);', match.group())
        return ('player.ReportError' if owner == 'PlayerTreasurePlanExt' else 'reportError') + '?.Invoke(' + (literals[0] if literals else '"Treasure slot data error"') + ');'
    body = re.sub(pattern, logging, body, flags=re.S)
    body = body.replace('Parsers.GameSettings.', ('player.Settings.' if owner == 'PlayerTreasurePlanExt' else 'settings.'))
    body = body.replace('PlayerTreasureElementSlotsDataWrap.CreateNew()', 'PlayerTreasureElementSlotsDataWrap.CreateNew(settings, reportError)')
    if any(token in body for token in ('GLogger', 'Log.', 'Parsers.')):
        raise ValueError('Unbound original slot dependency: ' + owner)
    return '\n'.join(line.rstrip() for line in body.splitlines())


def prepare(check=False, oracle=None):
    previous = json.loads(RECEIPT.read_bytes()) if RECEIPT.exists() else None
    source = OfficialSource(previous['manifest'] if previous else None)
    code = source.prepared_path / 'official-code'
    identity = source.prepared_path / 'code-manifest.json'
    assembly = code / 'managed/decoded/Console.dll'
    if sha(assembly.read_bytes()) != json.loads(identity.read_bytes())['assemblies']['Console.dll']['decoded_sha256']:
        raise ValueError('Original treasure slot assembly differs')
    originals = {}
    inputs = [identity, assembly, TEMPLATE, Path(__file__)]
    methods = {}
    for owner, signatures in METHODS.items():
        path = code / 'common-source' / ('TreasureHelper.cs' if owner == 'TreasureHelper' else 'Common/' + owner + '.cs')
        inputs.append(path)
        originals[owner] = [original_method(path.read_text('utf-8-sig'), signature) for signature in signatures]
        methods.update({owner + ':' + signature: sha(body.encode()) for signature, body in zip(signatures, originals[owner])})
    contracts = {}
    for relative, signatures in (
        ('common-source/PlayerServiceProxyImpl.cs', ('protected override void OperPlayerSetupTreasureSlotCallback(',)),
        ('common-source/Common/BattlePlanAccessor.cs', ('public static int GetActivePlanIndex(', 'public static PlayerBattlePlanContentBaseWrap GetOrCreatePlanContent(')),
        ('client-source/TreasureSlotItem.cs', ('private void OnReplace(',)),
    ):
        path = code / relative
        inputs.append(path)
        for signature in signatures:
            contracts[relative + ':' + signature] = sha(original_method(path.read_text('utf-8-sig'), signature).encode())
    for name in ('TreasureSlotData', 'TreasureSlotItemData', 'TreasureElementSlotsData', 'TreasurePlanContent',
                 'PlayerSetupTreasureSlotRequest', 'PlayerBattlePlanTreasurePlanContentWrap', 'BattlePlanType'):
        inputs.append(code / 'common-source/Common' / (name + '.cs'))
    apk = Apk(source.version, reuse_current_cache=True)
    constructors = Constructors(code)
    proof = {}
    tables = {}
    try:
        for name, kind in (('game_settings', 'TbGameSettings'), ('item', 'TbItemInfo'), ('treasure', 'TbTreasureInfo')):
            obj = apk.asset('defaultpackage_assets_config_luban.bundle', '/' + name + '.g.bytes')
            raw = obj.read().m_Script.encode('utf8', 'surrogateescape')
            proof[name] = dict(source=apk.provenance(obj), decoded_sha256=sha(raw))
            tables[name] = constructors.table(raw, kind)
    finally:
        apk.reader.close()
    ids = {row['ClassId'] for row in tables['treasure']}
    settings = tables['game_settings']
    payload = dict(TreasureMaxSlotCount=settings['TreasureMaxSlotCount'], TreasurePlanMaxCount=settings['TreasurePlanMaxCount'],
        TreasureSlotElementTypes=settings['TreasureSlotElementTypes'],
        Items=[dict(ClassId=row['ClassId'], ElementType=row['ElementType']) for row in tables['item'] if row['ClassId'] in ids])
    for name, text in constructors.sources.items():
        if text:
            path = constructors.source_root / (name + '.cs')
            if not path.is_file(): path = constructors.source_root.parent / 'Common.LubanGen' / (name + '.cs')
            inputs.append(path)
    text = TEMPLATE.read_text('utf8')
    for owner, bodies in originals.items():
        text = text.replace('/*' + owner + '*/', '\n\n'.join(bodies if oracle else [adapted(body, owner) for body in bodies]))
    if oracle:
        text = text.replace('Zjcs.Official.TreasureSlots', 'TreasureSlotOracle')
        text = text.replace('CreateNew(TreasureSlotSettings settings, Action<string> reportError)',
            'CreateNew()').replace('=> new PlayerTreasureElementSlotsDataWrap(settings, reportError);',
            '=> new PlayerTreasureElementSlotsDataWrap(Parsers.GameSettings, null);')
        text += '\nnamespace TreasureSlotOracle {\npublic static class Parsers { public static TreasureSlotSettings GameSettings; }\n'
        text += 'public struct GLoggerErrorInterpolatedStringHandler { public GLoggerErrorInterpolatedStringHandler(int a,int b,out bool enabled) { enabled=false; } public void AppendLiteral(string s) {} public void AppendFormatted<T>(T value,int a,object b,string name) {} }\n'
        text += 'public static class Log { public static int Errors; public static void Error(ref GLoggerErrorInterpolatedStringHandler m,object e,string member,string path,int line) { Errors++; } }\n}\n'
        oracle.parent.mkdir(parents=True, exist_ok=True)
        oracle.write_text(text, 'utf8', newline='\n')
        oracle.with_suffix('.json').write_text(json.dumps(payload, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
        return
    outputs = {DEST: text.encode(), DATA: (json.dumps(payload, ensure_ascii=False, indent=2) + '\n').encode()}
    for path in list(outputs):
        kind = 'MonoImporter' if path.suffix == '.cs' else 'TextScriptImporter'
        guid = hashlib.md5(('zjcs:treasure-slots:' + path.relative_to(ROOT).as_posix()).encode()).hexdigest()
        outputs[Path(str(path) + '.meta')] = ('fileFormatVersion: 2\nguid: ' + guid + '\n' + kind + ':\n  externalObjects: {}\n').encode()
    bindings = [ROOT / path for path in (
        'unity/Assets/Game/Runtime/Offline/Progression/OfflineTreasureSlotProgress.cs',
        'unity/Assets/Game/Runtime/Offline/Progression/OfflineTreasureSlotProgress.cs.meta',
        'unity/Assets/Game/Runtime/Offline/Progression/OfflineTreasureSlotService.cs',
        'unity/Assets/Game/Runtime/Offline/Progression/OfflineTreasureSlotService.cs.meta',
        'unity/Assets/Game/Runtime/Offline/Progression/OfflineCharacterPropertyProgress.cs',
        'unity/Assets/Game/Runtime/Offline/Progression/OfflineCharacterConfiguredPropSource.cs',
        'scripts/fixtures/treasure_slots_validation.cs', 'scripts/test_treasure_slots.ps1')]
    receipt = dict(manifest=source.version, inputs={p.relative_to(ROOT).as_posix(): sha(p.read_bytes()) for p in inputs},
        table_sources=proof, methods=methods, contracts=contracts,
        runtime_bindings={p.relative_to(ROOT).as_posix(): sha(p.read_bytes()) for p in bindings},
        outputs={p.relative_to(ROOT).as_posix(): sha(raw) for p, raw in outputs.items()},
        scope='Original slot storage, swapping, level/share queries, base-level state transition and manual active-plan writeback. Local durable setup and legacy-save import; level-up eligibility/fees, plan switching/clearing and native UI remain separate owners.')
    outputs[RECEIPT] = (json.dumps(receipt, ensure_ascii=False, indent=2) + '\n').encode()
    for path, raw in outputs.items():
        if check:
            if not path.is_file() or path.read_bytes() != raw: raise ValueError('Treasure slot output differs: ' + str(path))
        else:
            if path != RECEIPT and path.is_file() and path.read_bytes() != raw and sha(path.read_bytes()) != (previous or {}).get('outputs', {}).get(path.relative_to(ROOT).as_posix()):
                raise ValueError('Preserve another slot edit: ' + str(path))
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(raw)
    print('Verified original treasure slot methods:', len(methods), '; treasure elements:', len(ids))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    parser.add_argument('--oracle', type=Path)
    args = parser.parse_args()
    prepare(args.check, args.oracle)
