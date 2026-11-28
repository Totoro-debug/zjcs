"""Stage the complete selected Bed building closure before replacing installed assets."""
import argparse
import hashlib
import json
from pathlib import Path

from home_native_furniture_builder import HomeNativeFurnitureBinding, PartnerNativeRecovery
from official_apk import ROOT
from official_source import OfficialSource
from prepare_home_bed_native import DEST, PREFABS, RECEIPT


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def stage(work):
    work = work.resolve()
    if work.parent != (ROOT/'output/validation').resolve() or not (work/'.zjcs-validation.json').is_file():
        raise ValueError('Use an owned repository validation workspace')
    source = OfficialSource()
    cache = source.prepared_path/'home-bed-native'
    receipt_path = cache/'native-export-source-manifest.json'
    exported = json.loads(receipt_path.read_bytes())
    if exported['manifest'] != source.version or not exported['completed']:
        raise ValueError('Complete the selected Bed native conversion first')
    previous_raw = RECEIPT.read_bytes()
    previous = json.loads(previous_raw)
    recovery = PartnerNativeRecovery(native_cache=cache, export_name='native-export',
        bundle='defaultpackage_assets_prefabs_home_dynamic_furniture_bed.bundle')
    try:
        binding = HomeNativeFurnitureBinding(work,recovery=recovery,guid_prefix='zjcs:home-bed-native:')
        outputs,folder,prefabs,selections = binding.build_furniture_prefabs(
            'defaultpackage_assets_prefabs_home_dynamic_furniture_bed.bundle',PREFABS,DEST)
        for row,expected in outputs.items():
            if sha((folder/row).read_bytes()) != expected:
                raise ValueError('A staged native Bed output changed: '+row)
        result = dict(manifest=source.version,apk_sha256=source.apk_sha256,
            generator_sha256=sha(Path(__file__).read_bytes()),
            previous_receipt=dict(path=RECEIPT.relative_to(ROOT).as_posix(),sha256=sha(previous_raw),manifest=previous['manifest']),
            inputs=binding.inputs,recovery=recovery.proof,native_scripts=binding.script_sources,
            resources=binding.proof,audio=getattr(binding,'audio_proof',{}),
            staged_folder=folder.relative_to(ROOT).as_posix(),outputs=outputs,prefabs=prefabs,selections=selections)
        target = work/'current-bed-source-manifest.json'
        target.write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n','utf8',newline='\n')
        print('Staged selected native Bed:',len(prefabs),'complete skins;',len(outputs),'resource files;',len(binding.proof),'original objects',flush=True)
        return result
    finally:
        recovery.apk.close()


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--work',type=Path,required=True)
    stage(parser.parse_args().work)
