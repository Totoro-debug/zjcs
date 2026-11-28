"""Retire verified historical inventory PNGs after native binding and Play validation."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import subprocess

from official_apk import ROOT
from inventory_sprite_support import PROOF, PAYLOAD, atomic_write
from conversion_history import preserve


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def retire(workspace):
    workspace = workspace.resolve()
    validation_root = (ROOT/'output/validation').resolve()
    if workspace.parent != validation_root:
        raise ValueError('Use one explicit repository validation workspace')
    log = (workspace/'native-play.log').read_text('utf8', errors='replace')
    for marker in ('INVENTORY_CURRENT_SPRITES_NATIVE_PASSED:', 'INVENTORY_CURRENT_SPRITES_PLAY_PASSED:'):
        if marker not in log:
            raise ValueError('Complete current native art and actual Play validation before retiring old PNGs')
    proof_path = ROOT/PROOF
    proof_raw = proof_path.read_bytes()
    proof = json.loads(proof_raw)
    for row in proof['outputs']+proof['shared_dependencies']:
        if sha((ROOT/row['path']).read_bytes()) != row['sha256']:
            raise ValueError('Current native output changed after validation: '+row['path'])
    if 'retired_previous' in proof:
        for row in proof['retired_previous']:
            if (ROOT/row['path']).exists():
                raise ValueError('Historical inventory PNG reappeared: '+row['path'])
        return
    success = json.loads((workspace/'inventory-native-play-success.json').read_bytes())
    if not success.get('native_and_play_passed') or success.get('receipt_sha256') != sha(proof_raw):
        raise ValueError('Native/Play validation must cover this exact installed source receipt')
    art = (ROOT/PAYLOAD/'Art/sprites').resolve()
    expected = {row['alias']: row['previous_source']['sha256'] for row in json.loads(
        (ROOT/proof['staged_source']).read_bytes())['aliases'] if 'sha256' in row['previous_source']}
    # Quality backgrounds have their own historical receipt instead of a resource entry.
    # Git's existing blob identity also protects the extra quality aliases and
    # their importer metadata, whose historical resource entries were separate.
    tree = subprocess.run(['git','ls-tree','-rz','HEAD','--',art.relative_to(ROOT).as_posix()],
                          cwd=ROOT,capture_output=True,check=True).stdout
    tracked = {}
    for entry in tree.split(b'\0'):
        if not entry:
            continue
        header,name = entry.split(b'\t',1)
        mode,kind,oid = header.split()
        if kind != b'blob' or mode != b'100644':
            raise ValueError('Inspect a non-file historical inventory entry')
        tracked[name.decode('utf8')] = oid.decode('ascii')
    planned, guids = {}, {}
    aliases = {row['alias'] for row in proof['aliases']}
    for path in sorted(art.glob('*.png')):
        if path.name not in aliases:
            raise ValueError('Inspect an unconverted historical inventory PNG: '+str(path))
        raw = path.read_bytes()
        if path.name in expected and sha(raw) != expected[path.name]:
            raise ValueError('Preserve an edited historical inventory PNG: '+path.name)
        for item in (path, Path(str(path)+'.meta')):
            if not item.resolve().is_relative_to(art) or item.is_symlink():
                raise ValueError('Historical inventory cleanup target escaped its exact folder')
            relative = item.relative_to(ROOT).as_posix()
            actual = item.read_bytes()
            blob_id = hashlib.sha1(b'blob '+str(len(actual)).encode()+b'\0'+actual).hexdigest()
            normalized = actual.replace(b'\r\n', b'\n') if item.suffix == '.meta' else actual
            normalized_id = hashlib.sha1(b'blob '+str(len(normalized)).encode()+b'\0'+normalized).hexdigest()
            if tracked.get(relative) not in (blob_id, normalized_id):
                raise ValueError('Preserve a changed historical inventory input: '+relative)
            planned[item] = actual
        match = re.search(rb'(?m)^guid: ([a-f0-9]{32})\r?$', planned[Path(str(path)+'.meta')])
        if not match:
            raise ValueError('Historical inventory PNG GUID is missing: '+path.name)
        guids[match[1].decode()] = path.name
    if len(planned) != len(aliases)*2 or len(guids) != len(aliases):
        raise ValueError('Historical inventory PNG inventory is incomplete')
    patterns = workspace/'retired-inventory-guid-patterns.txt'
    patterns.write_text(''.join(guid+'\n' for guid in sorted(guids)), 'ascii')
    scan = subprocess.run(['rg','--hidden','--no-ignore','-l','-F','-f',str(patterns),
                           '-g','*.asset','-g','*.prefab','-g','*.unity','-g','*.mat','-g','*.meta',
                           '-g','*.cs','-g','*.json','unity/Assets'], cwd=ROOT,
                          capture_output=True, encoding='utf8', check=False)
    if scan.returncode not in (0,1):
        raise ValueError(scan.stderr)
    consumers = [name for name in scan.stdout.splitlines() if (ROOT/name).resolve() not in planned]
    if consumers:
        raise ValueError('Migrate remaining historical inventory GUID consumers first: '+repr(consumers))
    retired = []
    # Finish every backup before removing any input. Only exact, verified files are unlinked.
    for path, raw in planned.items():
        if path.read_bytes() != raw:
            raise ValueError('Historical inventory input changed during preflight: '+str(path))
        backup = preserve(path)
        if sha((ROOT/backup['preserved']).read_bytes()) != sha(raw):
            raise ValueError('Historical inventory snapshot was not completed')
        retired.append(backup)
    for path, raw in planned.items():
        if path.read_bytes() != raw:
            raise ValueError('Historical inventory input changed before removal: '+str(path))
        path.unlink()
    proof.pop('retained_previous_sprites', None)
    proof.update(retired_previous=retired, retirement_generator_sha256=sha(Path(__file__).read_bytes()))
    preserve(proof_path)
    atomic_write(proof_path, (json.dumps(proof, ensure_ascii=False, indent=2)+'\n').encode('utf8'))
    print('Retired verified historical inventory PNGs:',len(aliases),flush=True)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--validation-workspace', type=Path, required=True)
    retire(parser.parse_args().validation_workspace)
