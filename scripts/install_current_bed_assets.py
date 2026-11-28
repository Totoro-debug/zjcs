"""Replace the verified Bed closure while preserving installed Unity asset identities."""
import argparse
import hashlib
import json
from pathlib import Path
import re

from home_native_assets import install_staged_assets
from official_apk import ROOT
from official_source import OfficialSource
from prepare_home_bed_native import DEST, RECEIPT
from conversion_history import preserve


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def guid(raw):
    match = re.search(rb'(?m)^guid: ([0-9a-f]{32})\r?$',raw)
    if not match:
        raise ValueError('Native Bed importer GUID is missing')
    return match[1].decode()


def remap(value,mapping):
    if isinstance(value,dict):
        return {key:mapping.get(item,item) if key=='guid' and isinstance(item,str) else remap(item,mapping)
                for key,item in value.items()}
    if isinstance(value,list):
        return [remap(item,mapping) for item in value]
    return value


def install(work):
    work = work.resolve()
    if work.parent != (ROOT/'output/validation').resolve() or not (work/'.zjcs-validation.json').is_file():
        raise ValueError('Use an owned repository validation workspace')
    staged_path = work/'current-bed-source-manifest.json'
    staged = json.loads(staged_path.read_bytes())
    source = OfficialSource()
    if staged['manifest']!=source.version or staged['apk_sha256']!=source.apk_sha256:
        raise ValueError('Use the selected Bed source')
    old_raw = RECEIPT.read_bytes()
    if sha(old_raw)!=staged['previous_receipt']['sha256']:
        raise ValueError('Installed Bed receipt changed after staging')
    previous = json.loads(old_raw)
    for relative,expected in previous['outputs'].items():
        if sha((ROOT/relative).read_bytes())!=expected:
            raise ValueError('Preserve a changed Bed output: '+relative)
    for relative,expected in staged['inputs'].items():
        if sha((ROOT/relative).read_bytes())!=expected:
            raise ValueError('A selected native Bed input changed: '+relative)
    folder = ROOT/staged['staged_folder']
    mapped = work/'stable-bed-assets'
    mapping,old_metas = {},{}
    for relative,expected in staged['outputs'].items():
        raw = (folder/relative).read_bytes()
        if sha(raw)!=expected:
            raise ValueError('Staged Bed output changed: '+relative)
        if not relative.endswith('.meta'):
            continue
        target = DEST/relative
        if target.is_file():
            old = target.read_bytes()
            mapping[guid(raw)] = guid(old)
            old_metas[relative] = old
    outputs = {}
    for relative in staged['outputs']:
        raw = (folder/relative).read_bytes()
        if relative in old_metas:
            raw = old_metas[relative]
        elif raw.startswith(b'%YAML') or relative.endswith('.meta'):
            raw = re.sub(rb'guid: ([a-f0-9]{32})',lambda m:('guid: '+mapping.get(m[1].decode(),m[1].decode())).encode(),raw)
        path = mapped/relative
        path.parent.mkdir(parents=True,exist_ok=True)
        path.write_bytes(raw)
        target = DEST/relative
        if target.exists() and target.read_bytes()!=raw and sha(target.read_bytes())!=previous['outputs'].get(target.relative_to(ROOT).as_posix()):
            raise ValueError('Preserve an unowned Bed replacement: '+relative)
        outputs[relative] = sha(raw)
    # Complete the rollback snapshots before replacing any installed payload.
    snapshots = [preserve(ROOT/path) for path in previous['outputs']]
    previous_receipt = preserve(RECEIPT)
    installed = install_staged_assets(work,mapped,outputs,DEST,'native-bed-building',previous_outputs=previous['outputs'])
    prefabs = remap(staged['prefabs'],mapping)
    result = dict(manifest=source.version,inputs=staged['inputs'],
        selection=staged['selections']['Bed'],selections=staged['selections'],recovery=staged['recovery'],
        native_scripts=staged['native_scripts'],resources=remap(staged['resources'],mapping),
        audio=staged['audio'],prefab=prefabs[0]['pointer'],prefabs=prefabs,outputs=installed,
        scope='All seven original Bed building skin prefabs, exact dependency closure, controller fields, animation, textures and Windows shaders from the selected source.',
        replacement=dict(previous_receipt=previous_receipt,previous_outputs=snapshots,
            guid_rewrites=mapping,generator_sha256=sha(Path(__file__).read_bytes()),
            staged_generator_sha256=staged['generator_sha256']))
    # Keep obsolete owned files explicit until their references and runtime use are checked.
    result['retained_previous_outputs'] = {path:digest for path,digest in previous['outputs'].items() if path not in installed}
    RECEIPT.write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n','utf8',newline='\n')
    print('Installed selected Bed closure:',len(prefabs),'skins;',len(installed),'files;',len(result['retained_previous_outputs']),'previous files pending reference review',flush=True)
    return result


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--work',type=Path,required=True)
    install(parser.parse_args().work)
