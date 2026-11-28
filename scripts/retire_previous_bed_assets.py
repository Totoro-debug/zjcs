"""Retire the prior Bed dependency files after proving their reference closure is unused."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import subprocess

from official_apk import ROOT
from prepare_home_bed_native import DEST, RECEIPT
from conversion_history import preserve


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def retire(work):
    work=work.resolve()
    if work.parent!=(ROOT/'output/validation').resolve() or not (work/'.zjcs-validation.json').is_file():
        raise ValueError('Use an owned validation workspace')
    proof=json.loads(RECEIPT.read_bytes())
    previous=proof.get('retained_previous_outputs',{})
    if not previous:
        return
    snapshots={row['path']:row for row in proof['replacement']['previous_outputs']}
    patterns=[]
    for name,expected in previous.items():
        path=ROOT/name
        if not path.resolve().is_relative_to(DEST.resolve()) or path.is_symlink() or sha(path.read_bytes())!=expected:
            raise ValueError('Preserve an edited or unowned previous Bed asset: '+name)
        backup=ROOT/snapshots[name]['preserved']
        if sha(backup.read_bytes())!=expected:
            raise ValueError('Complete the previous Bed snapshot first: '+name)
        if name.endswith('.meta'):
            patterns.append(re.search(rb'(?m)^guid: ([a-f0-9]{32})\r?$',path.read_bytes())[1].decode())
    pattern_path=work/'retired-bed-guids.txt'
    pattern_path.write_text('\n'.join(patterns)+'\n','ascii')
    scan=subprocess.run(['rg','-l','-F','--hidden','--no-ignore','-f',str(pattern_path),'unity/Assets'],
                        cwd=ROOT,capture_output=True,text=True,encoding='utf8')
    if scan.returncode not in (0,1):
        raise ValueError(scan.stderr)
    consumers=[name.replace('\\','/') for name in scan.stdout.splitlines() if name.replace('\\','/') not in previous]
    if consumers:
        raise ValueError('Migrate remaining previous Bed consumers first: '+repr(consumers))
    empty_folders=[Path(str(ROOT/name)[:-5]) for name in previous if name.endswith('.meta') and Path(str(ROOT/name)[:-5]).is_dir()]
    for name in previous:
        (ROOT/name).unlink()
    for folder in sorted(empty_folders,key=lambda p:len(p.parts),reverse=True):
        if not any(folder.iterdir()):
            folder.rmdir()
    preserve(RECEIPT)
    proof['retired_previous_outputs']=[snapshots[name] for name in previous]
    proof.pop('retained_previous_outputs',None)
    proof['retirement_generator_sha256']=sha(Path(__file__).read_bytes())
    RECEIPT.write_text(json.dumps(proof,ensure_ascii=False,indent=2)+'\n','utf8',newline='\n')
    print('Retired unused previous Bed closure files:',len(previous),flush=True)


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--work',type=Path,required=True)
    retire(parser.parse_args().work)
