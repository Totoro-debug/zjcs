"""Write and install explicitly bound home-native resource closures."""
import hashlib
from pathlib import Path
import subprocess

import yaml
from UnityPy.enums import ClassIDType

from official_apk import ROOT
from prepare_partner_card_native import Dumper, guid, sha


def write_staged_assets(queue, folder):
    outputs = {}
    for target, target_guid, data in queue:
        path = folder/target
        path.parent.mkdir(parents=True, exist_ok=True)
        if isinstance(data, dict):
            text = '%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n'
            for file_id, content in data.items():
                kind, = content
                text += '--- !u!'+str(int(ClassIDType[kind]))+' &'+str(file_id)+'\n'
                text += yaml.dump(content,Dumper=Dumper,sort_keys=False,allow_unicode=True,width=1000000)
            raw = text.encode('utf8')
        else:
            raw = data
        path.write_bytes(raw)
        importer = 'PrefabImporter' if path.suffix == '.prefab' else 'ShaderImporter' if path.suffix == '.shader' else 'NativeFormatImporter'
        meta = Path(str(path)+'.meta')
        meta.write_text('fileFormatVersion: 2\nguid: '+target_guid+'\n'+importer+':\n  externalObjects: {}\n','utf8',newline='\n')
        outputs[target.as_posix()] = sha(raw)
        outputs[target.as_posix()+'.meta'] = sha(meta.read_bytes())
    return outputs


def install_staged_assets(work, folder, outputs, destination, prefix):
    for relative, expected in outputs.items():
        if sha((folder/relative).read_bytes()) != expected:
            raise ValueError('Staged native asset changed: '+relative)
    wanted = work/(prefix+'-guids.txt')
    wanted.write_text(''.join('guid: '+guid(folder/path)+'\n' for path in outputs if path.endswith('.meta')),'utf8')
    found = subprocess.run(['rg','-l','-F','-f',str(wanted),'unity/Assets','-g','*.meta'],cwd=ROOT,capture_output=True,text=True)
    if found.returncode not in (0,1):
        raise ValueError(found.stderr)
    existing = {}
    for name in found.stdout.splitlines():
        key = guid(ROOT/name)
        if key in existing: raise ValueError('Existing native GUID collision: '+key)
        existing[key] = ROOT/name[:-5]
    installed = {}
    for relative in outputs:
        if relative.endswith('.meta'): continue
        source = folder/relative
        key = guid(Path(str(source)+'.meta'))
        target = existing.get(key,destination/relative)
        if target.exists():
            meta = Path(str(target)+'.meta')
            if not meta.is_file() or guid(meta) != key:
                raise ValueError('Preserve existing native asset with a different GUID: '+str(target))
            if target.read_bytes() != source.read_bytes():
                raise ValueError('Preserve different existing native payload: '+str(target))
        else:
            target.parent.mkdir(parents=True,exist_ok=True)
            target.write_bytes(source.read_bytes())
            Path(str(target)+'.meta').write_bytes(Path(str(source)+'.meta').read_bytes())
        for path in (target,Path(str(target)+'.meta')):
            installed[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
        parent = target.parent
        while parent.is_relative_to(destination):
            meta = Path(str(parent)+'.meta')
            if not meta.exists():
                folder_guid = hashlib.md5((prefix+':folder:'+parent.relative_to(ROOT).as_posix()).encode()).hexdigest()
                meta.write_text('fileFormatVersion: 2\nguid: '+folder_guid+'\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n','utf8',newline='\n')
            installed[meta.relative_to(ROOT).as_posix()] = sha(meta.read_bytes())
            parent = parent.parent
    return installed
