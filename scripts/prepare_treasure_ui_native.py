"""Prepare and recover the selected official treasure UI's original native hierarchy and fields."""
import argparse
import json
import os
import uuid
from pathlib import Path

from home_native_cache_verification import verify_native_cache
from home_native_furniture_builder import prepare_native_cache
from official_source import OfficialSource, ROOT
from prepare_current_partner_native import PartnerNativeRecovery

BUNDLE='defaultpackage_assets_prefabs_ui_treasure.bundle'
DETAIL_BUNDLE='defaultpackage_assets_prefabs_ui_itemdetail.bundle'
DETAILS=('TreasureDetailDialog','TreasurePieceDialog')


def prepare():
    source=OfficialSource()
    cache=source.prepared_path/'home-treasure-ui-native'
    if not (cache/'native-export-source-manifest.json').is_file() and not (cache/'native-export').exists():
        prepare_native_cache(cache,[BUNDLE,DETAIL_BUNDLE])
    return verify_native_cache(cache,['Prefabs/UI/Treasure/TreasurePanel.prefab']+
        ['Prefabs/UI/ItemDetail/'+name+'.prefab' for name in DETAILS],source.version)


def recover(cache,work):
    recovery=PartnerNativeRecovery(native_cache=cache,export_name='native-export',bundle=BUNDLE)
    try:
        for path,_ in recovery.apk.container(BUNDLE):
            if not path.endswith('.prefab'):continue
            prefix,relative=path.split('/',1)
            if prefix.casefold()!='assets':raise ValueError('Original prefab address has no Assets prefix: '+path)
            recovery.recover(Path(path).stem,work/'original',bundle=BUNDLE,native_relative=relative)
        for name in DETAILS:
            recovery.recover(name,work/'original',bundle=DETAIL_BUNDLE,native_relative='Prefabs/UI/ItemDetail/'+name+'.prefab')
        recovery.finalize()
        (work/'original/source-manifest.json').write_text(json.dumps(recovery.proof,ensure_ascii=False,indent=2)+'\n','utf8',newline='\n')
        print('Recovered original treasure UI prefabs:',len(recovery.outputs),flush=True)
    finally:recovery.apk.close()


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--recover',action='store_true');parser.add_argument('--work',type=Path)
    args=parser.parse_args();cache=prepare()
    if args.recover:
        work=(args.work or ROOT/'output/validation'/('treasure-ui-'+uuid.uuid4().hex)).resolve()
        if not work.is_relative_to((ROOT/'output/validation').resolve()) or work==ROOT/'output/validation':raise ValueError('Use an owned validation directory')
        work.mkdir(parents=True,exist_ok=True)
        marker=work/'.zjcs-validation.json'
        if not marker.exists():marker.write_text(json.dumps(dict(format=1,repository=str(ROOT),ownerPid=os.getpid()))+'\n','utf8')
        print('Original treasure UI recovery:',work,flush=True);recover(cache,work)
