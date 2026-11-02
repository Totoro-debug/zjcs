"""Prepare selected original NPC parser/formula execution with the native UI closure."""
import hashlib
import json
from pathlib import Path
import shutil
import subprocess
import io
import urllib.request
import zipfile

from official_apk import Apk, ROOT
from official_tables import Constructors
from prepare_partner_panel_validation import main as prepare_panel
from prepare_partner_star_god_validation import copy_modules
from validation_workspace import validation_directory


def prepare_litedb(work, destination):
    # Exact strong-named dependency requested by the selected Console module;
    # APK IL2CPP dummy DLLs contain signatures, not an executable implementation.
    url = 'https://api.nuget.org/v3-flatcontainer/litedb/5.0.20/litedb.5.0.20.nupkg'
    package = work / 'litedb.5.0.20.nupkg'
    if not package.exists():
        package.write_bytes(urllib.request.urlopen(url, timeout=45).read())
    raw = package.read_bytes()
    if hashlib.sha256(raw).hexdigest() != '5fa8d0e820a5acb7e408201c88be6de1191ac61888c677d476062eaf9025b566':
        raise ValueError('Pinned LiteDB publisher package differs')
    archive = zipfile.ZipFile(io.BytesIO(raw))
    dll = archive.read('lib/netstandard2.0/LiteDB.dll')
    (destination / 'LiteDB.bytes').write_bytes(dll)
    (destination / 'LiteDB-LICENSE.txt').write_bytes(archive.read('LICENSE'))
    return dict(url=url, package_sha256=hashlib.sha256(raw).hexdigest(),
        payload_sha256=hashlib.sha256(dll).hexdigest())


def main():
    work = validation_directory()
    project = work / 'star-god-unity'
    if not project.exists(): prepare_panel()
    packages = project / 'Packages/manifest.json'
    package_config = json.loads(packages.read_bytes())
    source_packages = json.loads((ROOT / 'unity/Packages/manifest.json').read_bytes())
    package_config['dependencies']['com.unity.nuget.newtonsoft-json'] = source_packages['dependencies']['com.unity.nuget.newtonsoft-json']
    packages.write_text(json.dumps(package_config, indent=2) + '\n', 'utf8')
    runtime = ROOT / 'unity/Assets/Game/Runtime'
    modules = {json.loads(path.read_bytes())['name']: path for path in runtime.rglob('*.asmdef')}
    pending, selected = ['Zjcs.Offline.World.NativeMap14SharedEntities', 'Zjcs.Offline.PartnersNpc'], set()
    while pending:
        name = pending.pop()
        if name in selected or name not in modules: continue
        selected.add(name); pending.extend(json.loads(modules[name].read_bytes()).get('references', []))
    copy_modules(project, runtime, modules, selected)
    editor = project / 'Assets/Editor'
    for name in ('PartnerPanelNativeGraphicValidation.cs', 'PartnerStarGodLocalValidation.cs'):
        (editor / name).unlink(missing_ok=True)
    shutil.copy2(ROOT / 'scripts/fixtures/partner_npc_shared_validation.cs', editor / 'PartnerNpcSharedValidation.cs')
    definition = editor / 'Validation.asmdef'
    value = json.loads(definition.read_bytes())
    value['references'] = list(dict.fromkeys(value['references'] + ['Config', 'Zjcs.Offline.World.NativeMap14SharedEntities', 'Zjcs.Offline.PartnersNpc']))
    definition.write_text(json.dumps(value), 'utf8', newline='\n')
    original = ROOT / 'unity/Assets/Official/World/Map/14'
    target = project / 'Assets/Official/World/Map/14'
    shutil.copytree(original / 'SharedRuntime', target / 'SharedRuntime', dirs_exist_ok=True)
    for suffix in ('', '.meta'):
        source = original / ('SharedSerialization/MessagePack.dll' + suffix)
        (target / 'SharedSerialization').mkdir(parents=True, exist_ok=True)
        shutil.copy2(source, target / 'SharedSerialization' / source.name)
    resources = project / 'Assets/Game/Resources/Map14/Native'
    resources.mkdir(parents=True, exist_ok=True)
    for suffix in ('', '.meta'):
        shutil.copy2(ROOT / 'unity/Assets/Game/Resources/Map14/Native' / ('shared-assembly-catalog.asset' + suffix),
            resources / ('shared-assembly-catalog.asset' + suffix))
    npc_assets = ROOT / 'unity/Assets/Official/Partners/NpcRuntime'
    npc_catalog = ROOT / 'unity/Assets/Game/Resources/Partners/OfficialPartnerNpcRuntimeCatalog.asset'
    if npc_catalog.exists():
        shutil.copytree(npc_assets, project / 'Assets/Official/Partners/NpcRuntime', dirs_exist_ok=True)
        for suffix in ('', '.meta'):
            source = Path(str(npc_catalog) + suffix)
            shutil.copy2(source, project / 'Assets/Game/Resources/Partners' / source.name)
    selected_code = ROOT / '.tools/source-packages/current/884_156692/official-code'
    assemblies = editor / 'NpcSharedPayload'
    assemblies.mkdir(exist_ok=True)
    identities = {}
    package = prepare_litedb(work, assemblies)
    code_manifest = json.loads((selected_code.parent / 'code-manifest.json').read_bytes())
    if code_manifest['manifest'] != '884_156692':
        raise ValueError('Selected original code manifest differs')
    for name in ('Console', 'Luban', 'Framework.FirstPass.Runtime'):
        source = selected_code / 'managed/decoded' / (name + '.dll')
        if source.read_bytes() != (ROOT / '.tools/source-packages/official-code/managed/decoded' / source.name).read_bytes():
            raise ValueError('Selected original module differs: ' + name)
        raw = source.read_bytes()
        if hashlib.sha256(raw).hexdigest() != code_manifest['assemblies'][source.name]['decoded_sha256']:
            raise ValueError('Decoded official module hash differs: ' + name)
        shutil.copyfile(source, assemblies / (name + '.bytes'))
        identities[name] = hashlib.sha256(raw).hexdigest()
    subprocess.run(['pwsh', '-NoProfile', '-File', str(ROOT / 'scripts/prepare_partner_shared_identity.ps1'),
        '-OriginalDirectory', str(selected_code / 'managed/decoded'), '-Destination', str(assemblies)], check=True)
    apk = Apk.current()
    if apk.version != '884_156692': raise ValueError('Review selected NPC configuration version')
    tables = project / 'Assets/NpcSharedConfiguration/Resources/PartnerNpcShared'
    tables.mkdir(parents=True, exist_ok=True)
    proofs = {}
    for obj in apk.objects('defaultpackage_assets_config_luban.bundle'):
        if obj.type.name != 'TextAsset': continue
        data = obj.read()
        name = data.m_Name.rsplit('/', 1)[-1]
        raw = data.m_Script.encode('utf8', 'surrogateescape')
        (tables / (name + '.bytes')).write_bytes(raw)
        proofs[name] = dict(source=apk.provenance(obj), payload_sha256=hashlib.sha256(raw).hexdigest())
    name = 'wwisebnk_assets_config_binary_level_prop_g_bytes.rawfile'
    record, = [row for row in apk.source.manifest('WwiseBnk', apk.archive)['bundles'] if row['name'] == name]
    entry = 'assets/yoo/WwiseBnk/' + record['file']
    if entry in apk.available:
        raw = apk.archive.read(entry)
        origin = dict(origin='apk_zip', apk_entry=entry, apk_sha256=apk.source.apk_sha256)
    else:
        cached = apk.cloud.read('WwiseBnk', record)
        if cached is None:
            raise FileNotFoundError('Selected raw property binary is not cached')
        raw, origin = cached
    if len(raw) != record['size'] or hashlib.md5(raw).hexdigest() != record['hash']:
        raise ValueError('Selected raw property binary differs from its manifest')
    binary = project / 'Assets/StreamingAssets/PartnerNpcShared/Config/Binary/level_prop.g.bytes'
    binary.parent.mkdir(parents=True, exist_ok=True)
    binary.write_bytes(raw)
    proofs['level_prop.g.bytes'] = dict(source=origin, bundle=record, payload_sha256=hashlib.sha256(raw).hexdigest())
    apk.archive.close()
    constructors = Constructors(selected_code)
    fixture = dict(npc_classes=sorted(row['Id'] for row in constructors.table((tables / 'npc.g.bytes').read_bytes(), 'TbNpcInfo')),
        combat_npc_classes=sorted({row['ClassId'] for row in constructors.table((tables / 'npc_level_detail.g.bytes').read_bytes(), 'TbNpcLevelDetailInfo')}))
    settings = constructors.table((tables / 'game_settings.g.bytes').read_bytes(), 'TbGameSettings')
    groups = constructors.table((tables / 'role_prop_group.g.bytes').read_bytes(), 'TbRolePropGroupInfo')
    fixture['reference_ranks'] = sorted({row['SubRank'] for row in groups if row['ID'] == settings['CalcCombatPropGroupId']})
    (work / 'npc-runtime-expected.json').write_text(json.dumps(fixture, indent=2) + '\n', 'utf8')
    (work / 'npc-shared-inputs.json').write_text(json.dumps(dict(manifest='884_156692', assemblies=identities, tables=proofs, litedb=package), ensure_ascii=False, indent=2), 'utf8')
    print('Prepared selected original NPC shared modules and', len(proofs), 'local Luban inputs', flush=True)


if __name__ == '__main__': main()
