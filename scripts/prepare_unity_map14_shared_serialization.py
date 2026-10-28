"""Bind unchanged Map14 shared records and generated formatter IL locally."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import shutil
import subprocess
import urllib.request
import zipfile

from official_apk import Apk, ROOT
from prepare_unity_map14_shared_ec_runtime import meta
from prepare_unity_story_sound_runtime import runtime_meta

VERSION = '871_156598'
CACHE = ROOT / '.tools/nuget-cache/map14-shared-ec'
RUNTIME = ROOT / 'unity/Assets/Game/Runtime/Official/SharedSerialization'
PAYLOAD = ROOT / 'unity/Assets/Official/World/Map/14/SharedSerialization'
RESOURCES = PAYLOAD / 'Resources/Map14/SharedSerialization'
RECEIPT = ROOT / 'assets/reference/official-map/map14-shared-serialization-source-manifest.json'
PACKAGE_HASH = 'f2d84de1f3a3f5422c7b5f75ad97f71f8722254b943c12b11fbcf4589956863f'
PUBLISHER = '6cbd8196e7dbe9b8810d7620f4542c3db9388753'
SOURCE_HASHES = {
    'Formatters.cs': 'a958cacc77b636316b22d823b6a524e3db006c4ba4afa0eec3fbf425b91bb351',
    'UnityResolver.cs': 'b43a04797edea68279da22818348d15d09d8667ed89af7be4005c630ff19614e',
}


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def record(path):
    return dict(path=path.relative_to(ROOT).as_posix(), sha256=sha(path))


def remove_formatter(source, name):
    start = source.index('    public sealed class ' + name + ' ')
    opening = source.index('{', start)
    depth = 1
    end = opening + 1
    while depth:
        depth += (source[end] == '{') - (source[end] == '}')
        end += 1
    return source[:start] + source[end:]


def prepare(native_cache):
    code = ROOT / '.tools/source-packages/current' / VERSION / 'official-code'
    metadata = code / 'global-metadata.dat'
    image = ROOT / '.tools/source-packages/commerce-native/libil2cpp-restored.bin'
    native = json.loads(native_cache.read_bytes())
    if native['metadata_sha256'] != '58884b94b56ef35665f44d3f186fce8b8c854cf3bab0a74e4733a18b6a9175b4' or native['image_sha256'] != '8e49906e085ba3819c4b47fd3bd12dcdd594ff0fcc75d418a87ac35a4566305b':
        raise ValueError('Native serializer input identities differ')
    if sha(metadata) != native['metadata_sha256'] or sha(image) != native['image_sha256']:
        raise ValueError('Pinned native serializer inputs changed')
    with image.open('rb') as stream:
        for methods in native['types'].values():
            for method in methods:
                stream.seek(method['address'])
                if hashlib.sha256(stream.read(method['length'])).hexdigest() != method['sha256']:
                    raise ValueError('Selected native serializer method bytes differ')
    cache_dir = native_cache.resolve().parent
    validation = (ROOT / 'output/validation').resolve()
    if not cache_dir.is_relative_to(validation) or cache_dir == validation:
        raise ValueError('Use an existing owned validation session')
    build = cache_dir / 'serialization-build'
    build.mkdir(exist_ok=True)
    CACHE.mkdir(parents=True, exist_ok=True)
    package = CACHE / 'messagepack.1.9.11.nupkg'
    package_url = 'https://api.nuget.org/v3-flatcontainer/messagepack/1.9.11/messagepack.1.9.11.nupkg'
    if not package.exists():
        package.write_bytes(urllib.request.urlopen(package_url, timeout=45).read())
    if sha(package) != PACKAGE_HASH:
        raise ValueError('Pinned publisher MessagePack package differs')
    archive = zipfile.ZipFile(package)
    publisher_dll = CACHE / 'MessagePack.dll'
    publisher_dll.write_bytes(archive.read('lib/netstandard2.0/MessagePack.dll'))
    RUNTIME.mkdir(parents=True, exist_ok=True)
    RESOURCES.mkdir(parents=True, exist_ok=True)
    (RESOURCES / 'MessagePack-LICENSE.txt').write_bytes(archive.read('LICENSE'))
    publisher_sources = []
    for name, expected in SOURCE_HASHES.items():
        source = CACHE / ('Publisher' + name)
        url = f'https://api.github.com/repos/MessagePack-CSharp/MessagePack-CSharp/contents/src/MessagePack.UnityShims/{name}?ref={PUBLISHER}'
        if not source.exists():
            import base64
            request = urllib.request.Request(url, headers={'User-Agent': 'zjcs-offline-migration'})
            payload = json.load(urllib.request.urlopen(request, timeout=30))
            source.write_bytes(base64.b64decode(payload['content']))
        if sha(source) != expected:
            raise ValueError('Pinned publisher Unity serializer source differs: ' + name)
        text = source.read_text(encoding='utf-8-sig')
        if name == 'Formatters.cs':
            text = remove_formatter(remove_formatter(text, 'Vector3Formatter'), 'QuaternionFormatter')
        (RUNTIME / ('Publisher' + name)).write_text(text, encoding='utf-8')
        publisher_sources.append(dict(url=url, **record(source)))
    base = ROOT / 'unity/Assets/Official/World/Map/14/SharedRuntime/GFrameCore.bytes'
    seed = native['metadata_sha256'] + sha(publisher_dll)
    subprocess.run(['pwsh', '-NoProfile', '-File', str(ROOT / 'scripts/prepare_unity_shared_messagepack.ps1'),
                    '-OriginalConsole', str(code / 'managed/decoded/Console.dll'),
                    '-BaseContract', str(base), '-PublisherDll', str(publisher_dll),
                    '-Destination', str(build), '-IdentitySeed', seed], check=True)
    shutil.copyfile(build / 'GFrameCore.dll', base)
    plugin = PAYLOAD / 'MessagePack.dll'
    shutil.copyfile(build / 'MessagePack.dll', plugin)
    identities = json.loads((build / 'assemblies.json').read_text(encoding='utf-8-sig'))
    identity = next(row for row in identities if row['Name'] == 'GFrameCore')
    shared_catalog = ROOT / 'unity/Assets/Game/Resources/Map14/Native/shared-assembly-catalog.asset'
    text = shared_catalog.read_text(encoding='utf-8')
    text, count = re.subn(r'(  - Name: GFrameCore\n    Data: [^\n]+\n    Sha256: )[^\n]+(\n    ModuleId: )[^\n]+',
                         lambda match: match[1] + sha(base) + match[2] + identity['ModuleId'], text)
    if count != 1:
        raise ValueError('Original shared assembly catalog binding differs')
    shared_catalog.write_bytes(text.encode('utf-8'))
    lookup = RESOURCES / 'formatter-map.json'
    shutil.copyfile(build / 'formatter-map.json', lookup)
    apk = Apk.current()
    if apk.version != VERSION:
        raise ValueError('Selected map source version differs')
    source_receipt = ROOT / 'assets/reference/official-map/map14-source-manifest.json'
    source = json.loads(source_receipt.read_bytes())
    wanted = {row['asset']: row for row in source['ec_templates'].values()}
    entries = []

    def payload(obj, original, name, expected=None):
        raw = obj.read().m_Script.encode('utf-8', 'surrogateescape')
        digest = hashlib.sha256(raw).hexdigest()
        if expected and digest != expected:
            raise ValueError('Retained official record differs: ' + original)
        target = RESOURCES / name
        target.write_bytes(raw)
        entries.append(dict(OriginalPath=original, Resource='Map14/SharedSerialization/' + name.rsplit('.', 1)[0],
                            Sha256=digest, source=apk.provenance(obj), path=target.relative_to(ROOT).as_posix()))

    for bundle, suffix, original in (
            ('config_ec', '/map_14.g.bytes', 'Config/EC/map_14.g.bytes'),
            ('config_ec_maprootentitylite', '/14.g.bytes', 'Config/EC/MapRootEntityLite/14.g.bytes'),
            ('config_mapinfowrap', '/mapinfowrap_14.g.bytes', 'Config/MapInfoWrap/mapinfowrap_14.g.bytes')):
        payload(apk.asset(bundle, suffix), original, suffix.rsplit('/', 1)[1])
    for path, pointer in apk.container('config_ec'):
        if path not in wanted and not path.endswith('/MapRoot_14.g.bytes'):
            continue
        payload(pointer.deref(), path.removeprefix('Assets/'), path.rsplit('/', 1)[1],
                wanted[path]['payload_sha256'] if path in wanted else None)
    apk.archive.close()
    catalog = RESOURCES / 'catalog.json'
    catalog.write_text(json.dumps(dict(Entries=entries, LookupResource='Map14/SharedSerialization/formatter-map',
                                       LookupSha256=sha(lookup)), ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
    for file in RUNTIME.iterdir():
        if file.is_file() and file.suffix in ('.cs', '.asmdef'):
            runtime_meta(file)
    runtime_meta(RUNTIME)
    adapter = ROOT / 'unity/Assets/Game/Runtime/Offline/World/NativeMap14SharedEntities/NativeMap14SharedData.cs'
    runtime_meta(adapter)
    acceptance = ROOT / 'unity/Assets/Game/Editor/Map14/HeroStatues/OfficialMap14SharedDataAcceptance.cs'
    runtime_meta(acceptance)
    for file in PAYLOAD.rglob('*'):
        if not file.name.endswith('.meta'):
            if file.is_dir() or file.suffix != '.dll':
                meta(file)
    meta(PAYLOAD)
    # Managed dependency with reference validation; payload stays outside Game/.
    plugin_meta = Path(str(plugin) + '.meta')
    if not plugin_meta.exists():
        import uuid
        plugin_meta.write_text('fileFormatVersion: 2\nguid: '+uuid.uuid4().hex+'\nPluginImporter:\n  externalObjects: {}\n  serializedVersion: 2\n  isPreloaded: 0\n  isOverridable: 0\n  isExplicitlyReferenced: 0\n  validateReferences: 1\n  platformData:\n  - first:\n      Any:\n    second:\n      enabled: 1\n      settings: {}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n', encoding='utf-8')
    outputs = [file for folder in (RUNTIME, PAYLOAD) for file in folder.rglob('*') if file.is_file()]
    outputs += [Path(str(RUNTIME)+'.meta'), Path(str(PAYLOAD)+'.meta'), base, shared_catalog]
    outputs += [adapter, Path(str(adapter)+'.meta'), adapter.parent / 'Zjcs.Offline.World.NativeMap14SharedEntities.asmdef']
    outputs += [acceptance, Path(str(acceptance)+'.meta'), acceptance.parent / 'OfficialMap14HeroStatueAcceptance.cs']
    evidence = {name: [{key: value for key, value in method.items() if key != 'instructions'} for method in methods]
                for name, methods in native['types'].items()}
    tools = [ROOT / 'scripts' / name for name in ('prepare_unity_map14_shared_serialization.py',
             'prepare_unity_shared_messagepack.ps1', 'read_official_shared_serialization_native.py',
             'prepare_unity_map14_shared_ec_runtime.py')]
    RECEIPT.write_text(json.dumps(dict(manifest=VERSION, mapClassId=14,
        operation='Preserve original Console IL and generated formatter choices; forward the shared MessagePack ABI to a pinned publisher dependency with native raw-single encoding and original raw Vector3/Quaternion codecs; bind unchanged local map records.',
        inputs=[record(file) for file in (metadata, image, code / 'managed/decoded/Console.dll', source_receipt)],
        package=dict(url=package_url, sha256=PACKAGE_HASH, repository_commit=PUBLISHER,
                     original_dll=record(publisher_dll), converted_dll=record(plugin), sources=publisher_sources),
        native_types=evidence, assemblies=identities, source_records=entries,
        validation=dict(entry='Zjcs.Editor.Map14.OfficialMap14HeroStatueAcceptance.Run',
                        shared_data='OfficialMap14SharedDataAcceptance.Verify',
                        placements=2961, components=22979, regions=111,
                        template_files=32, template_rows=568, ground_cells=78400,
                        scope='Isolated graphical Unity Play Mode; source fields/order, exact full/lite ground types and coordinate round trips, loader restoration, and four original statue display regression.'),
        tools=[record(file) for file in tools], outputs=[record(file) for file in sorted(set(outputs))],
        limitations=['This binding restores shared data loading only; full serializers, map lifecycle, gameplay, UI, state/save and Player integration remain to be verified.']),
        ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
    print('Bound', len(entries), 'unchanged shared map payloads and original formatter lookup')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--native-cache', type=Path, required=True)
    prepare(parser.parse_args().native_cache)
