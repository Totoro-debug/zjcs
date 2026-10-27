"""Extract the original FightSettings.ReportSetting and record the unchanged scoring formula."""
import argparse
import hashlib
import json
from pathlib import Path
import sys

from restore_battle_report_dropdown import ROOT, guid, metadata, write

OUTPUT = 'unity/Assets/Official/Combat/report-score-settings.json'
RUNTIME = 'unity/Assets/Game/Runtime/Official/Combat/FightBattleReportScoreSettings.cs'
SOURCE = 'unity/Assets/Official/Combat/report-score-settings-source-manifest.json'
CATALOG = 'unity/Assets/Game/Resources/Combat/OfficialCombatAssetCatalog.asset'
RECEIPT = ROOT / 'assets/reference/battle-rules/report-score-settings-source-manifest.json'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def record_runtime():
    data = json.loads(RECEIPT.read_bytes())
    assert sha(ROOT / OUTPUT) == data['output_sha256']
    names = [RUNTIME, RUNTIME + '.meta', 'scripts/test_battle_report_scores.ps1',
             'unity/Assets/Game/Runtime/Presentation/Combat/OfficialCombatAssetCatalog.cs', CATALOG,
             'unity/Assets/Game/Editor/Combat/OfficialBattleReportStatisticsAcceptance.cs']
    data['runtime'] = {name: sha(ROOT / name) for name in names}
    data['user_confirmed'] = {
        'received_damage': 'BeAbsoluteDamage includes shield absorption, HP overflow and damage negated by defence/reduction.',
        'break_resilience': 'BreakResilience counts successful enemy breaks, not resilience points removed.'}
    data['implementation'] = 'Preserve the original ordered status-score list, unknown-type zero, null-dictionary early return and original multiplication order. Feed successful enemy break counts and accepted Buff/ordinary Debuff/AbnormalDebuff application counts; legacy missing histories remain unavailable.'
    data['pending'] = 'Formal player report data grouping, portrait/provider binding and the complete player-row runtime flow remain pending.'
    data['generator_sha256'] = sha(Path(__file__))
    write(RECEIPT, (json.dumps(data, ensure_ascii=False, indent=2) + '\n').encode())


def build(source_root):
    sys.path.insert(0, str(source_root / 'scripts'))
    from official_apk import Apk
    from official_unity_schema import ScriptSchemas
    apk = Apk(manifest='854_156383', reuse_current_cache=True)
    schemas = ScriptSchemas()
    bundle = 'defaultpackage_assets_scriptableconfig_modules_fight.bundle'
    objects = [obj for obj in apk.objects(bundle) if obj.type.name == 'MonoBehaviour'
               and obj.read(check_read=False).m_Script.read().m_ClassName == 'FightSettings']
    assert len(objects) == 1
    obj = objects[0]
    settings = schemas.read(obj)['ReportSetting']
    assert set(settings) == {'BreakResilienceScoreFactor', 'ApplyStatusScore', 'ReportAllPlayerStages'}
    assert all(set(row) == {'StageType', 'Score'} for row in settings['ApplyStatusScore'])
    payload = dict(manifest=apk.version, ReportSetting=settings)
    write(ROOT / OUTPUT, (json.dumps(payload, ensure_ascii=False, indent=2) + '\n').encode())
    metadata(ROOT / OUTPUT, 'TextScriptImporter')
    metadata(ROOT / RUNTIME, 'MonoImporter')
    source = dict(manifest=apk.version, output_sha256=sha(ROOT / OUTPUT),
                  source=apk.provenance(obj), source_field='FightSettings.ReportSetting')
    write(ROOT / SOURCE, (json.dumps(source, ensure_ascii=False, indent=2) + '\n').encode())
    metadata(ROOT / SOURCE, 'TextScriptImporter')
    import re
    catalog = (ROOT / CATALOG).read_text(encoding='utf-8')
    for field, path in [('reportScoreSettings', OUTPUT), ('reportScoreSettingsSourceManifest', SOURCE)]:
        line = '  ' + field + ': {fileID: 4900000, guid: ' + guid(ROOT / path) + ', type: 3}\n'
        pattern = r'(?m)^  ' + field + r':[^\n]*\n'
        if re.search(pattern, catalog): catalog = re.sub(pattern, lambda _: line, catalog)
        else: catalog += line
    write(ROOT / CATALOG, catalog.encode())
    official = source_root / '.tools/source-packages/official-code/client-source'
    names = ['FightSettings.cs', 'FightReportSetting.cs', 'FightReportUIUtils.cs',
             'FightEndReportPlayerTabViewItem.cs', 'FightEndReportPlayerTabView.cs']
    record = dict(manifest=apk.version, source=apk.provenance(obj),
                  object_sha256=hashlib.sha256(obj.get_raw_data()).hexdigest(),
                  source_field='FightSettings.ReportSetting', original_fields=settings,
                  official_code={name: sha(official / name) for name in names},
                  output=OUTPUT, output_sha256=sha(ROOT / OUTPUT),
                  outputs={name: sha(ROOT / name) for name in [OUTPUT, OUTPUT + '.meta', SOURCE, SOURCE + '.meta', CATALOG]},
                  implementation='Preserve the original ordered status-score list, unknown-type zero, null-dictionary early return and original multiplication order. The caller supplies BreakResilience; no local counter meaning is inferred.',
                  pending='BeAbsoluteDamage accumulation and BreakResilience counter meaning need user confirmation. Formal player report data, portrait/provider binding and completed player-row runtime flow remain pending.')
    write(RECEIPT, (json.dumps(record, ensure_ascii=False, indent=2) + '\n').encode())
    record_runtime()
    print('Extracted original report settings:', settings)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source-root', type=Path, default=ROOT)
    parser.add_argument('--record-runtime', action='store_true')
    args = parser.parse_args()
    record_runtime() if args.record_runtime else build(args.source_root)
