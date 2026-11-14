"""Recover selected menu configuration and the original Wiki/activity lookup code."""
import hashlib
import json
from pathlib import Path
import re
import shutil

from official_apk import Apk, ROOT
from official_tables import Constructors
from prepare_partner_gift_configuration import method, normalize, without_logging
from validation_workspace import validation_directory, require_temporary_unity_project


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def guid(name):
    return hashlib.md5(('zjcs:home-function-menu-configuration:' + name).encode()).hexdigest()


def meta(name, importer):
    return ('fileFormatVersion: 2\nguid: ' + guid(name) + '\n' + importer +
            ':\n  externalObjects: {}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n').encode()


def build():
    work = validation_directory()
    project = require_temporary_unity_project(work / 'unity')
    apk = Apk.current()
    assert apk.version == '884_156692'
    code = ROOT / '.tools/source-packages/current' / apk.version / 'official-code'
    constructors = Constructors(code)
    inputs, methods, objects, tables, table_sources = {}, {}, {}, {}, {}

    def source(name):
        value = constructors.source(name)
        assert value, name
        return value.replace('\r\n', '\n')

    def selected(name, signature):
        value = method(source(name), signature)
        methods.setdefault(name, {})[signature] = sha(value.encode())
        return value

    mapping = {name: kind for kind, name in
               re.findall(r'new (Tb\w+)\(loader\("([^"]+)"\)\)', source('Tables'))}
    names = ('wiki', 'activity_recommend', 'activity_entry', 'game_settings')
    for obj in apk.objects('defaultpackage_assets_config_luban.bundle'):
        if obj.type.name != 'TextAsset':
            continue
        name = obj.read().m_Name.rsplit('/', 1)[-1]
        if name not in {name + '.g' for name in names}:
            continue
        assert name not in objects, name
        objects[name] = obj
    for name in names:
        obj = objects[name + '.g']
        raw = obj.read().m_Script.encode('utf8', 'surrogateescape')
        decoded = constructors.table(raw, mapping[name])
        assert decoded, name
        tables[name] = ({'DisplayServerTimeRegionList': decoded['DisplayServerTimeRegionList']}
                        if name == 'game_settings' else decoded)
        table_sources[name] = dict(source=apk.provenance(obj), constructor=mapping[name],
            object_sha256=sha(obj.get_raw_data()), payload_sha256=sha(raw), bytes=len(raw),
            projection=['DisplayServerTimeRegionList'] if name == 'game_settings' else None,
            rows=1 if isinstance(decoded, dict) else len(decoded))
        print(name, table_sources[name]['rows'], flush=True)

    parts = ['using System;\nusing System.Collections.Generic;\nusing System.Linq;\n'
             'namespace Zjcs.Official.HomeFunctionMenuUI\n{\n'
             '// Original parser methods; binary input is materialized by the local asset owner.\n'
             'public sealed class HomeMenuConfigurationRules\n{']
    for name in ('RegionCodeEnum', 'ActivityCategoryType'):
        raw = source(name)
        parts.append(raw.split('namespace Common.LubanGen;\n' if name == 'RegionCodeEnum'
                               else 'namespace Common;\n', 1)[1].strip())
    for name in ('WikiInfo', 'ActivityRecommendInfo', 'ActivityEntryInfo'):
        fields = re.findall(r'^\tpublic readonly ([\w<>]+) (\w+);', source(name), re.M)
        assert fields, name
        parts.append('public sealed class ' + name + '\n{\n' + '\n'.join(
            'public ' + kind + ' ' + field + ';' for kind, field in fields) + '\n}')
    parts.append('''private Dictionary<string,WikiInfo> mWikiInfoDict;
private readonly Dictionary<ActivityEntryType,Dictionary<int,ActivityRecommendInfo>> recommendations;
private readonly Dictionary<ActivityEntryType,Dictionary<int,ActivityEntryInfo>> entries;
public HomeMenuConfigurationRules(WikiInfo[] wiki,ActivityRecommendInfo[] recommendations,ActivityEntryInfo[] entries)
{
    ClientPostParse(wiki);
    this.recommendations=IndexRecommendations(recommendations.ToDictionary(row=>(row.ActivityEntryType,row.Id),row=>row));
    this.entries=IndexEntries(entries.ToDictionary(row=>(row.ActivityType,row.Id),row=>row));
}
public ActivityRecommendInfo GetRecommend(ActivityEntryType type,int id)
    => recommendations.TryGetValue(type,out var group)&&group.TryGetValue(id,out var row)?row:null;
public IReadOnlyDictionary<int,ActivityEntryInfo> GetEntries(ActivityEntryType type)
    => entries.TryGetValue(type,out var group)?group:null;
''')
    wiki = selected('WikiInfoParser', 'private void ClientPostParse(WikiInfo[] configs)')
    wiki = without_logging(wiki.replace('GLoggerWarnInterpolatedStringHandler',
        'GLoggerErrorInterpolatedStringHandler').replace('Log.Warn(', 'Log.Error('), 1)
    parts.append(wiki)
    for signature in ('public string GetTitleKey(string type)', 'public string GetURL(string type)',
                      'public WikiInfo GetWikiInfo(string type)'):
        parts.append(selected('WikiInfoParser', signature))
    for name, row, method_name in (
        ('ActivityRecommendInfoParser', 'ActivityRecommendInfo', 'IndexRecommendations'),
        ('ActivityEntryInfoParser', 'ActivityEntryInfo', 'IndexEntries')):
        signature = 'protected override Dictionary<ActivityEntryType, Dictionary<int, ' + row + '>> LoadLubanTable(ByteBuf buf)'
        value = selected(name, signature)
        native_input = ('IReadOnlyDictionary<(ActivityEntryType, int), ' + row +
                        '> dataMapUnion = new Tb' + row + '(buf).DataMapUnion;')
        assert value.count(native_input) == 1
        value = value.replace(native_input, '')
        value = value.replace(signature, 'private static Dictionary<ActivityEntryType, Dictionary<int, ' +
            row + '>> ' + method_name + '(IReadOnlyDictionary<(ActivityEntryType, int), ' + row + '> dataMapUnion)')
        parts.append(value)
    # Record the generic lookup methods behind the two original keyed parsers.
    for signature in ('public T Get(K0 key0, K1 key1)', 'public Dictionary<K1, T> Get(K0 key0)'):
        selected('LubanDoubleKeyCsvParser', signature)
    parts.append('}\n}')
    runtime = normalize('\n\n'.join(parts)).encode()
    assert not any(token in runtime for token in (b'ByteBuf', b'GLogger', b'Log.Error', b'Parsers.'))
    payload = (json.dumps(dict(manifest=apk.version, tables=tables), ensure_ascii=False,
                          separators=(',', ':'), allow_nan=False) + '\n').encode()
    asset = 'unity/Assets/Official/Home/UI/FunctionMenu/home-function-menu-rules.json'
    script = 'unity/Assets/Game/Runtime/Official/HomeFunctionMenuUI/HomeMenuConfigurationRules.cs'
    catalog = 'unity/Assets/Game/Resources/Home/OfflineHomeFunctionMenuConfiguration.asset'
    catalog_script = ROOT / 'unity/Assets/Game/Runtime/Offline/HomeUI/OfficialCurrentHomeRuleAssetCatalog.cs.meta'
    script_guid = re.search(r'^guid: (\w+)$', catalog_script.read_text('utf8'), re.M)[1]
    binding = ('''%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 0}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: ''' + script_guid + ''', type: 3}
  m_Name: OfflineHomeFunctionMenuConfiguration
  m_EditorClassIdentifier:
  configuration: {fileID: 4900000, guid: ''' + guid(asset) + ''', type: 3}
''').encode()
    outputs = {asset: payload, asset + '.meta': meta(asset, 'TextScriptImporter'),
        script: runtime, script + '.meta': meta(script, 'MonoImporter'),
        catalog: binding, catalog + '.meta': meta(catalog, 'NativeFormatImporter')}
    manifest = ROOT / 'assets/reference/home-function-menu-configuration-source-manifest.json'
    previous = json.loads(manifest.read_bytes()).get('outputs', {}) if manifest.exists() else {}
    for relative, raw in outputs.items():
        target = ROOT / relative
        if target.exists() and sha(target.read_bytes()) not in (sha(raw), previous.get(relative)):
            raise ValueError('Preserve edited menu configuration output: ' + relative)
    for relative, raw in outputs.items():
        target = ROOT / relative
        target.write_bytes(raw)
        staged = project / target.relative_to(ROOT / 'unity')
        staged.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(target, staged)
    for name in constructors.sources:
        candidate = code / 'common-source/Common' / (name + '.cs')
        if not candidate.exists():
            candidate = code / 'common-source/Common.LubanGen' / (name + '.cs')
        assert candidate.exists(), name
        inputs[candidate.relative_to(ROOT).as_posix()] = sha(candidate.read_bytes())
    for path in (Path(__file__), catalog_script, code / 'global-metadata.dat',
                 ROOT / 'scripts/official_tables.py', ROOT / 'scripts/official_apk.py',
                 ROOT / 'scripts/official_metadata.py', ROOT / 'scripts/official_manifest.py',
                 ROOT / 'official-source.json',
                 ROOT / 'scripts/prepare_partner_gift_configuration.py',
                 ROOT / 'scripts/fixtures/home_function_menu_configuration_validation.cs',
                 ROOT / 'scripts/prepare_home_function_menu_configuration_validation.py',
                 ROOT / 'unity/Assets/Game/Runtime/Offline/HomeFunctionMenuUI/OfflineHomeFunctionMenuConfiguration.cs',
                 ROOT / 'unity/Assets/Game/Runtime/Offline/HomeFunctionMenuUI/Zjcs.Offline.HomeFunctionMenuUI.asmdef',
                 ROOT / 'unity/Assets/Game/Runtime/Offline/HomeUI/OfficialCurrentHomeRuleAssetCatalog.cs'):
        inputs[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
    receipt = dict(manifest=apk.version, tables=table_sources, inputs=inputs, methods=methods,
        outputs={path:sha(raw) for path,raw in outputs.items()},
        scope='Selected configuration and original parser lookup only. Activity records, Wiki HTML '
              'content/navigation and production menu services are owned by their respective modules.')
    manifest.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    return receipt


if __name__ == '__main__':
    build()
