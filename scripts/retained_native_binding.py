"""Rebuild a migrated native closure using its own verified original inputs."""
import hashlib
import json
from pathlib import Path
from types import MappingProxyType

from official_apk import Apk, ROOT
from official_bundle_reader import OfficialBundleReader, VerifiedBundle, verify_payload
from official_source import OfficialSource
from official_bundle_directory import cab_names


def sha(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def rebuild(work, receipt_path, binding_factory, *, generator, record_unchanged=False, install=None,
            export_name='native-export-all', identity_sections=('roots',), script_section='script_bindings'):
    prior = json.loads(receipt_path.read_bytes())
    version = prior['manifest']
    if export_name not in ('native-export', 'native-export-all'):
        raise ValueError('Unsupported retained native export layout')
    export_key, = [key for key in prior['inputs'] if key.endswith('/' + export_name + '-source-manifest.json')]
    export = ROOT / export_key
    cache = export.parent
    if sha(export) != prior['inputs'][export_key]:
        raise ValueError('Original native export receipt differs')
    proof = json.loads(export.read_bytes())
    inputs = cache / 'source-manifest.json'
    if not proof['completed'] or proof['manifest'] != version or sha(inputs) != proof['input_manifest_sha256']:
        raise ValueError('Retained original conversion inputs differ')
    source = json.loads(inputs.read_bytes())
    if source['manifest'] != version:
        raise ValueError('Retained conversion source selection differs')
    retained = {(row['package'], row['name']): row for row in source['inputs'] if 'name' in row}
    original_current = Apk.__dict__['current']
    original_read = OfficialBundleReader.read
    original_source_init = OfficialSource.__init__
    used, raw_inputs = {}, {}

    def select_source(self, manifest=None):
        original_source_init(self, manifest or version)

    def read(reader, package, name):
        try:
            return original_read(reader, package, name)
        except FileNotFoundError:
            if reader.source.version != version or (package, name) not in retained:
                raise
            row = retained[package, name]
            path = (cache / row['output']).resolve()
            if not path.is_relative_to(cache.resolve()) or row['encrypted']:
                raise ValueError('Retained bundle is not an unchanged raw input: ' + name)
            data = path.read_bytes()
            digest = hashlib.sha256(data).hexdigest()
            if digest != row['raw_sha256'] or digest != row['decoded_sha256']:
                raise ValueError('Retained original bundle bytes differ: ' + name)
            reader.manifest(package)
            verify_payload(data, reader._indexes[package][name], identity=name)
            if row['source'].get('sha256', row['source'].get('raw_sha256')) != digest:
                raise ValueError('Retained original source receipt differs: ' + name)
            used[path.relative_to(ROOT).as_posix()] = digest
            raw_inputs[path.relative_to(ROOT).as_posix()] = digest
            return VerifiedBundle(data, MappingProxyType(row['source']))

    binding = None
    try:
        Apk.current = classmethod(lambda cls: cls(version, reuse_current_cache=True))
        OfficialSource.__init__ = select_source
        OfficialBundleReader.read = read
        binding = binding_factory(work)
        code_path = cache.parent / 'code-manifest.json'
        if sha(code_path) != proof['code_manifest_sha256']:
            raise ValueError('Retained original managed source receipt differs')
        managed = cache / 'inputs/Managed'
        code = json.loads(code_path.read_bytes())
        for name, row in code['assemblies'].items():
            path = managed / name
            if sha(path) != row['decoded_sha256']:
                raise ValueError('Retained original managed assembly differs: ' + name)
            used[path.relative_to(ROOT).as_posix()] = row['decoded_sha256']
        for row in proof['type_dependencies']:
            path = (cache / row['output']).resolve()
            if not path.is_relative_to(cache.resolve()) or sha(path) != row['sha256']:
                raise ValueError('Retained original type descriptor differs: ' + row['output'])
            used[path.relative_to(ROOT).as_posix()] = row['sha256']
        used[code_path.relative_to(ROOT).as_posix()] = sha(code_path)
        binding.recovery.schema_managed = managed
        apk = binding.recovery.apk
        if apk.source.apk_sha256 != source['apk_sha256'] or sha(apk.source.apk) != source['apk_sha256']:
            raise ValueError('Retained native source uses a different original APK')
        # MonoScript references can name the player-data CAB directly. Populate
        # that pinned APK container before resolving the first prefab component.
        apk.load_builtin('assets/bin/Data/data.unity3d')
        used[apk.source.apk.relative_to(ROOT).as_posix()] = source['apk_sha256']
        original_find = apk.env.find_file
        cab_directory = None

        def find_retained(name, *args, **kwargs):
            nonlocal cab_directory
            try:
                return original_find(name, *args, **kwargs)
            except FileNotFoundError:
                key = name.rsplit('/', 1)[-1].lower()
                if cab_directory is None:
                    cab_directory = {}
                    for row in retained.values():
                        if row.get('format') != 'UnityFS': continue
                        path = (cache / row['output']).resolve()
                        if not path.is_relative_to(cache.resolve()):
                            raise ValueError('Retained CAB directory escapes its source cache')
                        with path.open('rb') as stream:
                            for cab in cab_names(stream):
                                cab_directory.setdefault(cab.lower(), []).append(row)
                candidates = cab_directory.get(key, [])
                if len(candidates) != 1:
                    raise ValueError('Resolve one original retained CAB source: ' + key)
                row, = candidates
                if row['package'] != 'DefaultPackage':
                    raise ValueError('Bind the original cross-package CAB loader: ' + key)
                # Discovery uses the UnityFS directory only. Loading still goes
                # through the original manifest/MD5/size and raw-SHA verifier.
                apk.cabs[key] = dict(bundle=row['name'], hash=row['hash'])
                apk.load(row['name'])
                loaded = apk.env.get_cab(key)
                if loaded is None: raise ValueError('Verified retained bundle has a different CAB: ' + key)
                return loaded

        apk.env.find_file = find_retained
        binding.build()
    finally:
        if binding: binding.recovery.apk.reader.close()
        Apk.current = original_current
        OfficialBundleReader.read = original_read
        OfficialSource.__init__ = original_source_init
    staged = work / receipt_path.name
    result = json.loads(staged.read_bytes())
    result['inputs'].update(used)
    result['inputs'].update({path.relative_to(ROOT).as_posix(): sha(path) for path in (Path(__file__), generator, inputs)})
    result['retained_raw_input_replay'] = dict(source=version, raw_inputs=raw_inputs,
        validation='Existing native export chain, original raw SHA256 and decoded SHA256, pinned bundle MD5 and size.')
    staged.write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    if record_unchanged:
        if install is not None or result['outputs'] != prior['outputs'] or any(result[key] != prior[key] for key in identity_sections):
            raise ValueError('Recording source bindings requires identical original resource outputs and object identities')
        for name, row in prior[script_section].items():
            if result[script_section][name]['pointer'] != row['pointer']:
                raise ValueError('Native controller script identity changed: ' + name)
        for section in ('installed_output_sha256', 'folder_meta_sha256'):
            if section not in prior: continue
            for relative, expected in prior[section].items():
                if sha(ROOT / relative) != expected:
                    raise ValueError('Owned migrated resource changed: ' + relative)
            result[section] = prior[section]
        if 'reused' in prior: result['reused'] = prior['reused']
        receipt_path.write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    elif install is not None:
        install(work)
    print('Replayed original native source:', version, len(raw_inputs), 'retained raw bundles')
