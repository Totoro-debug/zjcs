"""Rebuild offline extraction inputs from the pinned APK; never run game DLLs.

Requires UnityPy, pycryptodome and ILSpyCmd (development dependencies only).
"""
import argparse
import hashlib
import json
from pathlib import Path
import re
import struct
import subprocess
import sys
import zipfile

from official_apk import Apk, ROOT, CACHE, UnityPy


def decode_assembly(raw, layout):
    """Mirror the selected Launch.HCC.A; never execute the decoded assembly."""
    from Crypto.Cipher import AES
    from Crypto.Util.Padding import unpad
    if raw.startswith(b'MZ'):
        return raw
    key = bytes(v-7 for v in [40,49,83,108,112,123,112,117,110,44,84,63,63,42,133,40])
    if layout == 'length-prefixed':
        if len(raw) < 4:
            raise ValueError('Assembly size error')
        length, = struct.unpack_from('<i', raw)
        if length <= 0 or length > len(raw)-4:
            raise ValueError('Assembly encrypted length error')
        encrypted, tail = raw[4:4+length], raw[4+length:]
    elif layout == 'whole':
        encrypted, tail = raw, b''
    else:
        raise ValueError('Unsupported official assembly encryption layout')
    decoded = unpad(AES.new(key, AES.MODE_CBC, key[::-1]).decrypt(encrypted), 16) + tail
    if not decoded.startswith(b'MZ'):
        raise ValueError('Invalid decoded assembly PE header')
    return decoded


def extract_native(destination):
    """Retain both APK IL2CPP images as native schema/recovery inputs."""
    from official_source import OfficialSource
    source = OfficialSource()
    destination = Path(destination).resolve()
    path = destination/'code-manifest.json'
    receipt = json.loads(path.read_bytes())
    if receipt['manifest'] != source.version or receipt['apk_sha256'] != source.apk_sha256:
        raise ValueError('Native extraction source differs from the prepared code')
    with source.apk.open('rb') as stream:
        if hashlib.file_digest(stream, 'sha256').hexdigest() != source.apk_sha256:
            raise ValueError('APK SHA256 mismatch')
    records = []
    with zipfile.ZipFile(source.apk) as archive:
        for abi in ('arm64-v8a', 'armeabi-v7a'):
            entry = 'lib/'+abi+'/libil2cpp.so'
            raw = archive.read(entry)
            target = destination/'official-code/native'/abi/'libil2cpp.so'
            target.parent.mkdir(parents=True, exist_ok=True)
            if target.exists() and target.read_bytes() != raw:
                from conversion_history import preserve
                preserve(target)
            target.write_bytes(raw)
            records.append(dict(apk_entry=entry, path=target.relative_to(ROOT).as_posix(),
                                sha256=hashlib.sha256(raw).hexdigest(), bytes=len(raw)))
    receipt['native_libraries'] = records
    path.write_text(json.dumps(receipt, ensure_ascii=False, indent=2)+'\n', 'utf8')
    return records


def activate_prepared(destination):
    """Install verified code/data in the selected version's single cache."""
    from official_source import OfficialSource
    from conversion_history import preserve
    destination = Path(destination).resolve()
    receipt = json.loads((destination / 'code-manifest.json').read_bytes())
    selected = OfficialSource()
    if receipt['manifest'] != selected.version:
        raise ValueError('Prepared code does not belong to the selected source')
    for name, row in receipt['assemblies'].items():
        if hashlib.sha256((destination/'official-code/managed/decoded'/name).read_bytes()).hexdigest() != row['decoded_sha256']:
            raise ValueError('Prepared assembly changed: ' + name)
    outputs = []
    for folder in ('official-code', 'official-data'):
        for source in sorted((destination / folder).rglob('*')):
            if not source.is_file():
                continue
            target = selected.prepared_path / source.relative_to(destination)
            if target.resolve() == source.resolve():
                continue
            raw = source.read_bytes()
            identical = target.exists() and target.read_bytes() == raw
            previous = preserve(target) if target.exists() and not identical else None
            target.parent.mkdir(parents=True, exist_ok=True)
            if not identical:
                temporary = target.with_name(target.name + '.prepared.tmp')
                temporary.write_bytes(raw)
                temporary.replace(target)
            outputs.append(dict(path=target.relative_to(ROOT).as_posix(), sha256=hashlib.sha256(raw).hexdigest(),
                                source=source.relative_to(ROOT).as_posix(), previous=previous))
    result = dict(manifest=receipt['manifest'], code_manifest_sha256=hashlib.sha256((destination/'code-manifest.json').read_bytes()).hexdigest(),
                  operation='Install into the selected version cache; preserve prior changed bytes and retain unmatched files.', outputs=outputs)
    (CACHE/'prepared-code-source-manifest.json').write_text(json.dumps(result, ensure_ascii=False, indent=2)+'\n', 'utf8')
    return result


def build(dotnet, ilspy, client=False, manifest=None, output_root=None):
    apk = Apk(manifest=manifest)
    destination = Path(output_root).resolve() if output_root else apk.source.prepared_path
    code = destination/'official-code'
    managed = code/'managed/decoded'
    managed.mkdir(parents=True,exist_ok=True)
    metadata = apk.archive.read('assets/bin/Data/Managed/Metadata/global-metadata.dat')
    (code/'global-metadata.dat').write_bytes(metadata)
    entries = apk.source.manifest('HybridCLR', apk.archive)['bundles']
    evidence = {}
    assembly_bytes = {}
    for bundle in entries:
        name,digest = bundle['name'],bundle['hash']
        path = f'assets/yoo/HybridCLR/{bundle["file"]}'
        verified = apk.reader.read('HybridCLR', bundle["name"])
        content, source = verified.data, dict(verified.provenance)
        env = UnityPy.load(apk.decode(content))
        for obj in env.objects:
            if obj.type.name!='TextAsset':continue
            text = obj.read()
            if not text.m_Name.endswith('.dll'):continue
            raw = text.m_Script.encode('utf8','surrogateescape')
            if text.m_Name in assembly_bytes:
                raise ValueError('Duplicate official assembly: ' + text.m_Name)
            assembly_bytes[text.m_Name] = raw
            evidence[text.m_Name]={'bundle':name,'bundle_hash':digest,'path_id':str(obj.path_id),'source':source,
                'text_asset_sha256':hashlib.sha256(raw).hexdigest()}
    launch = assembly_bytes['Launch.dll']
    if not launch.startswith(b'MZ'):
        raise ValueError('The official bootstrap must be readable before selecting its decryptor')
    (managed/'Launch.dll').write_bytes(launch)
    subprocess.run([dotnet,str(ilspy),'-p',str(managed/'Launch.dll'),'-o',str(code/'launch-source')],check=True)
    decryptor = code/'launch-source/Launch/HCC.cs'
    decryptor_text = decryptor.read_text(encoding='utf8')
    if 'BitConverter.ToInt32(bytes, 0)' in decryptor_text and 'Buffer.BlockCopy(bytes, num + num2' in decryptor_text:
        layout = 'length-prefixed'
    elif 'TransformFinalBlock(bytes, 0, bytes.Length)' in decryptor_text:
        layout = 'whole'
    else:
        raise ValueError('Inspect the selected official Launch.HCC.A encryption layout')
    for name, raw in assembly_bytes.items():
        decoded = decode_assembly(raw, layout)
        (managed/name).write_bytes(decoded)
        evidence[name]['decoded_sha256'] = hashlib.sha256(decoded).hexdigest()
    wanted = [('Console.dll','common-source'),('Framework.dll','framework-source'),
              ('Framework.FirstPass.Runtime.dll','framework-firstpass-source')]
    if client:wanted += [('Assembly-CSharp.dll','client-source')]
    for name,folder in wanted:
        subprocess.run([dotnet,str(ilspy),'-p',str(managed/name),'-o',str(code/folder)],check=True)
    data_folder = destination/'official-data'
    data_folder.mkdir(exist_ok=True)
    for obj in apk.objects('config_luban'):
        if obj.type.name!='TextAsset':continue
        text=obj.read()
        if '/' in text.m_Name or '\\' in text.m_Name:raise ValueError('Unexpected TextAsset name')
        (data_folder/(text.m_Name+'.bytes')).write_bytes(text.m_Script.encode('utf8','surrogateescape'))
    reference=destination
    reference.mkdir(exist_ok=True)
    (reference/'code-manifest.json').write_text(json.dumps({'manifest':apk.version,'apk_sha256':apk.source.apk_sha256,'assemblies':evidence,
        'metadata_sha256':hashlib.sha256(metadata).hexdigest(),
        'assembly_encryption':dict(layout=layout,source=decryptor.relative_to(ROOT).as_posix(),
            sha256=hashlib.sha256(decryptor.read_bytes()).hexdigest()),
        'operation':'Static IL decompilation; no game assembly execution'},indent=2),encoding='utf8')
    print('Prepared',len(evidence),'assemblies and packaged Luban TextAssets')


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--dotnet',default='dotnet')
    parser.add_argument('--ilspy',type=Path,default=ROOT/'.tools/ilspycmd/tools/net10.0/any/ilspycmd.dll')
    parser.add_argument('--decompile-client',action='store_true')
    parser.add_argument('--manifest',help='Explicit historical version; defaults to active official source')
    parser.add_argument('--output-root', type=Path, help='Stage current code and tables without replacing another source version')
    parser.add_argument('--activate-prepared', type=Path, help='Install prepared code/data into the selected version cache without deleting unmatched files')
    parser.add_argument('--extract-native-to', type=Path, help='Add original APK IL2CPP images to an already prepared source cache')
    args=parser.parse_args()
    if args.extract_native_to:
        print('Extracted native input images:', len(extract_native(args.extract_native_to)))
        raise SystemExit(0)
    if args.activate_prepared:
        print('Activated prepared files:', len(activate_prepared(args.activate_prepared)['outputs']))
        raise SystemExit(0)
    if not args.ilspy.exists():parser.error('Install ILSpyCmd 11.1.0.9782 into .tools/ilspycmd or supply --ilspy')
    build(args.dotnet,args.ilspy,args.decompile_client,args.manifest,args.output_root)
