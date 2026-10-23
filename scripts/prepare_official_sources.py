"""Rebuild offline extraction inputs from the pinned APK; never run game DLLs.

Requires UnityPy, pycryptodome and ILSpyCmd (development dependencies only).
"""
import argparse
import hashlib
import json
from pathlib import Path
import re
import subprocess
import sys

from official_apk import Apk, ROOT, CACHE, UnityPy


def build(dotnet, ilspy, client=False, manifest=None, output_root=None):
    from Crypto.Cipher import AES
    from Crypto.Util.Padding import unpad
    apk = Apk(manifest=manifest)
    destination = Path(output_root).resolve() if output_root else CACHE
    code = destination/'official-code'
    managed = code/'managed/decoded'
    managed.mkdir(parents=True,exist_ok=True)
    metadata = apk.archive.read('assets/bin/Data/Managed/Metadata/global-metadata.dat')
    (code/'global-metadata.dat').write_bytes(metadata)
    entries = apk.source.manifest('HybridCLR', apk.archive)['bundles']
    # Launch.dll/HCC.B contains this key derivation and reversed-key CBC IV.
    key = bytes(v-7 for v in [40,49,83,108,112,123,112,117,110,44,84,63,63,42,133,40])
    evidence = {}
    for bundle in entries:
        name,digest = bundle['name'],bundle['hash']
        path = f'assets/yoo/HybridCLR/{bundle["file"]}'
        if path in apk.available:
            content = apk.archive.read(path)
            source = dict(origin='apk_zip', apk_entry=path, manifest=apk.version)
        else:
            cached = apk.cloud.read('HybridCLR', bundle)
            if cached is None:raise FileNotFoundError('Required official assembly bundle: '+name)
            content,source = cached
        if len(content) != bundle['size'] or hashlib.md5(content).hexdigest() != digest:
            raise ValueError('Official assembly bundle size/MD5 mismatch: '+name)
        env = UnityPy.load(apk.decode(content))
        for obj in env.objects:
            if obj.type.name!='TextAsset':continue
            text = obj.read()
            if not text.m_Name.endswith('.dll'):continue
            raw = text.m_Script.encode('utf8','surrogateescape')
            decoded = raw if raw.startswith(b'MZ') else unpad(AES.new(key,AES.MODE_CBC,key[::-1]).decrypt(raw),16)
            if not decoded.startswith(b'MZ'):raise ValueError('Invalid decoded assembly: '+text.m_Name)
            (managed/text.m_Name).write_bytes(decoded)
            evidence[text.m_Name]={'bundle':name,'bundle_hash':digest,'path_id':str(obj.path_id),'source':source,
                'text_asset_sha256':hashlib.sha256(raw).hexdigest(),'decoded_sha256':hashlib.sha256(decoded).hexdigest()}
    wanted = [('Console.dll','common-source'),('Framework.dll','framework-source'),
              ('Launch.dll','launch-source'),('Framework.FirstPass.Runtime.dll','framework-firstpass-source')]
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
    reference=ROOT/'assets/reference/official-city' if destination == CACHE else destination
    reference.mkdir(exist_ok=True)
    from fetch_official_assets import SHA256
    (reference/'code-manifest.json').write_text(json.dumps({'manifest':apk.version,'apk_sha256':SHA256,'assemblies':evidence,
        'metadata_sha256':hashlib.sha256(metadata).hexdigest(),'operation':'Static IL decompilation; no game assembly execution'},indent=2),encoding='utf8')
    print('Prepared',len(evidence),'assemblies and packaged Luban TextAssets')


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--dotnet',default='dotnet')
    parser.add_argument('--ilspy',type=Path,default=ROOT/'.tools/ilspycmd/tools/net10.0/any/ilspycmd.dll')
    parser.add_argument('--decompile-client',action='store_true')
    parser.add_argument('--manifest',help='Explicit historical version; defaults to active official source')
    parser.add_argument('--output-root', type=Path, help='Stage current code and tables without replacing another source version')
    args=parser.parse_args()
    if not args.ilspy.exists():parser.error('Install ILSpyCmd 11.1.0.9782 into .tools/ilspycmd or supply --ilspy')
    build(args.dotnet,args.ilspy,args.decompile_client,args.manifest,args.output_root)
