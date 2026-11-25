"""Recover the current official pet-gacha panel for independent validation."""
import argparse
import json
from pathlib import Path

from home_native_cache_verification import verify_native_cache
from home_native_furniture_builder import prepare_native_cache
from official_apk import ROOT
from official_source import OfficialSource
from prepare_current_partner_native import PartnerNativeRecovery, digest
from validation_workspace import validation_directory


BUNDLE = 'defaultpackage_assets_prefabs_ui_pet.bundle'
PREFAB = 'Prefabs/UI/Pet/PetGachaPanel.prefab'


def prepare():
    source = OfficialSource()
    cache = ROOT / '.tools/source-packages/current' / source.version / 'home-pet-gacha-native'
    if not (cache / 'native-export-source-manifest.json').exists() and not (cache / 'native-export').exists():
        prepare_native_cache(cache, [BUNDLE])
    return verify_native_cache(cache, [PREFAB], source.version)


def recover(cache, work):
    destination = work / 'pet-gacha-original'
    destination.mkdir(exist_ok=False)
    recovery = PartnerNativeRecovery(native_cache=cache, export_name='native-export', bundle=BUNDLE)
    try:
        recovery.recover('PetGachaPanel', destination, bundle=BUNDLE, native_relative=PREFAB)
        recovery.finalize()
        _, _, panel, _ = recovery.outputs[0]
        external = {}
        for key, pending in panel['unresolved'].items():
            cab, path_id = key.rsplit(':', 1)
            source_file = recovery.apk.env.get_cab(cab) or recovery.apk.env.find_file(cab)
            if source_file is None:
                raise ValueError('Original external file is unavailable: ' + cab)
            obj = source_file.objects[int(path_id)]
            if obj.type.name != pending['type'] or digest(obj.get_raw_data()) != pending['payload_sha256']:
                raise ValueError('Original external reference changed: ' + key)
            if obj.type.name != 'MonoBehaviour':
                raise ValueError('Inspect unexpected external object type: ' + obj.type.name)
            script = obj.read(check_read=False).m_Script.read()
            external[key] = dict(pending, namespace=script.m_Namespace, script=script.m_ClassName,
                                 source=recovery.apk.provenance(obj))
        recovery.proof['external_originals'] = external
        (destination / 'source-manifest.json').write_text(
            json.dumps(recovery.proof, ensure_ascii=False, indent=2) + '\n', encoding='utf8', newline='\n')
        print('Recovered original panel; external references awaiting binding:', len(external))
    finally:
        recovery.apk.reader.close()


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--recover', action='store_true', help='Recover into the current validation workspace')
    args = parser.parse_args()
    work = validation_directory()
    if args.recover and work == ROOT / 'output/validation':
        raise ValueError('Use an owned validation workspace')
    cache = prepare()
    if args.recover:
        recover(cache, work)
