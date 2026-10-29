"""Recover the official Map14 client root and its complete native asset closure."""
import argparse,json,os
from pathlib import Path
from official_apk import Apk,ROOT
from map13_native_schemas import NativeSchemas
from map14_entity_native_schemas import install
from prepare_unity_bundle_inputs import collect,read_source,serialized_files
from prepare_unity_map13_water_entity_inputs import hydrate
from prepare_unity_map13_water_entity_export import convert,build as verify_export
import prepare_unity_map13_water_entity_export as export_verification
from prepare_unity_map14_sources import encode,sha,source_path,write
from prepare_unity_map14_client_view_bindings import record

BUNDLE='defaultpackage_assets_prefabs_ecentity_map_maproot_maproot_14.bundle'
ADDRESS='Assets/Prefabs/EcEntity/Map/MapRoot/MapRoot_14/14.prefab'
RECEIPT=ROOT/'assets/reference/official-map/map14-native-root-input-source-manifest.json'

def workspace():
    value=os.environ.get('ZJCS_VALIDATION_DIR')
    if not value or Path(os.environ.get('ZJCS_REPOSITORY_ROOT','')).resolve()!=ROOT.resolve():raise ValueError('Set the session validation directory and repository root')
    path=Path(value).resolve()
    if path.parent!=(ROOT/'output/validation').resolve() or not (path/'.zjcs-validation.json').is_file():raise ValueError('Use the repository validation workspace')
    return path/'map-root-native'

def build(run_converter=False,verify_existing=False):
    cache=workspace();apk=Apk.current()
    # Apk.current checks the APK first, then verifies the pinned downloaded CDN
    # payload and each manifest hash; all historical aliases retain provenance.
    roots=list(apk.container(BUNDLE))
    matches=[ptr.deref() for path,ptr in roots if path.casefold()==ADDRESS.casefold()]
    if len(matches)!=1 or matches[0].type.name!='GameObject':raise ValueError('Original Map14 root address/type differs')
    root=matches[0];root_record=dict(path=ADDRESS,payload_sha256=sha(root.get_raw_data()),source=apk.provenance(root))
    assets,missing=collect(apk,[BUNDLE])
    if missing:raise ValueError('Resolve original root CAB dependencies: '+str(missing))
    code=ROOT/'.tools/source-packages/current'/apk.version/'official-code'
    code_manifest=code.parent/'code-manifest.json';manifest=json.loads(code_manifest.read_bytes());assemblies=[]
    for name,entry in manifest['assemblies'].items():
        p=code/'managed/decoded'/name
        if sha(p.read_bytes())!=entry['decoded_sha256']:raise ValueError('Original root assembly version differs: '+name)
        assemblies.append(record(p))
    schemas=NativeSchemas(code);proof=install(schemas,code)
    inputs=[];components=[];changes=[];unions=[];count=0
    for name in sorted(apk.loaded):
        raw,entry=read_source(apk,name)
        if name.startswith('assets/bin/Data/'):
            converted=raw;fields=[];changed=[];typed=[];checked=sum(len(file.objects) for file in serialized_files(apk.loaded[name]))
        else:converted,fields,changed,typed,checked=hydrate(apk.loaded[name],schemas,include_source_identity=True)
        entry.update(output=Path(name).name,converter_input_sha256=sha(converted),converter_input_bytes=len(converted),checked_objects=checked)
        write(cache/'inputs'/entry['output'],converted)
        inputs.append(entry);components.extend(fields);changes.extend(changed);unions.extend(typed);count+=checked
        print('Verified original root dependency:',name,len(fields),'components',flush=True)
    component_path=cache/'components.json';write(component_path,encode(dict(manifest=apk.version,mapClassId=14,components=components)))
    tools=[Path(__file__).resolve(),ROOT/'scripts/map14_entity_native_schemas.py',ROOT/'scripts/map13_entity_native_schemas.py',ROOT/'scripts/map13_native_schemas.py',ROOT/'scripts/prepare_unity_map13_water_entity_inputs.py',ROOT/'scripts/prepare_unity_map13_water_entity_export.py',ROOT/'scripts/official_native_yaml_verification.py',ROOT/'scripts/prepare_unity_bundle_inputs.py',ROOT/'scripts/official_apk.py']
    write(RECEIPT,encode(dict(manifest=apk.version,mapClassId=14,selection=dict(roots=[BUNDLE],prefabs=[root_record]),inputs=inputs,serialized_files=list(assets.values()),assemblies=assemblies,tools=[record(p) for p in tools],native_schema=proof,converted_objects=changes,managed_reference_unions=unions,counts=dict(roots=1,bundles=len(inputs),components=len(components),checked_objects=count),operation='Read the exact original Map14 root GameObject and complete pinned CAB dependencies using APK-first lookup. Restore stripped native serialization descriptions and managed reference registries; preserve original values, pointer identities and source provenance. These converter inputs do not replace the Windows prefab/script/shader binding.')))
    apk.archive.close();print('Original root input closure:',len(inputs),'bundles;',count,'objects',flush=True)
    if run_converter:convert(cache)
    if run_converter or verify_existing:
        long_cache=Path('\\\\?\\'+str(cache)) if os.name=='nt' else cache
        previous=export_verification.source_path
        export_verification.source_path=lambda p:previous(Path(str(p).removeprefix('\\\\?\\')))
        try:verify_export(receipt_path=RECEIPT,component_path=component_path,roots=[BUNDLE],cache_root=long_cache,proof_path=long_cache/'export-source-manifest.json',expected_counts=None,entry_path=Path(__file__).resolve(),map_class_id=14,subject='map root')
        finally:export_verification.source_path=previous

if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--convert',action='store_true');parser.add_argument('--verify-export',action='store_true');args=parser.parse_args();build(args.convert,args.verify_export)
