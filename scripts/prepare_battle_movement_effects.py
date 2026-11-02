"""Extract original movement effect subgraphs and their native Windows assets.

Animation, trajectory and audio leaves have separate original-source adapters;
this projection retains effect leaves, original sequence nesting and cue times.
"""
import json
import os
import re
import struct
from pathlib import Path
from hashlib import sha256
from PIL import Image, ImageChops
from Crypto.Hash import MD4

from official_apk import Apk, ROOT
from official_manifest import read_asset_manifest
from current_component_schemas import CurrentSchemas
from UnityPy.classes import PPtr
from prepare_battle_scene_appearance_runtime import identity, meta, write_changed
from prepare_battle_ground_effect_assets import CACHE, EXPORTED, GUID, BUILTINS
from prepare_unity_bundle_inputs import serialized_files
from prepare_unity_map13_native_scene import native_texture
from prepare_unity_map14_native_scene import decode_mips

DEST = ROOT / 'unity/Assets/Official/Combat/MovementEffects/Resources/Combat/MovementEffects'
DATA = ROOT / 'unity/Assets/Game/Resources/Combat/movement-effects.json'
RECEIPT = ROOT / 'assets/reference/battle-animation/movement-effects-source-manifest.json'
SCRIPTS = {
    -253978515: ('StoryPlayables', 'MyPlayableSequence'),
    -1107144664: ('StoryFollowBone', 'MyPlayableFollowBone'),
    903818314: ('StoryGroundEffects', 'ApplyByGroundType'),
    -1247969075: ('StoryPlayables', 'ApplyEnableOnPlay'),
    768753278: ('StoryFollowBone', 'ApplyOnStopFadeOut'),
}
DOC = re.compile(r'(?m)^--- !u!(\d+) &(-?\d+)\n')
SCRIPT = re.compile(r'm_Script: \{fileID: (-?\d+), guid: ([a-f0-9]{32}), type: 3\}')
ROW = re.compile(r'  - myTime: [^\n]+\n    myPlayable: \{fileID: (-?\d+)\}\n')


def digest(raw):
    return sha256(raw).hexdigest()


def method_body(text, name):
    text=re.sub(r'SingleR\w+', 'SingleRemove',text)
    match=re.search(r'(?m)^\s*(?:public|private|protected)[^\n;=]*\b'+name+r'\s*\([^;\n]*\)\s*\{',text)
    assert match,name
    start=text.index('{',match.start());depth=1;end=start+1
    while depth:
        depth+=(text[end]=='{')-(text[end]=='}');end+=1
    return text[start:end]


def runtime_equivalence(source, migrated, name):
    methods = ('PlayImpl','Stop','RemoveImpl','ProcessFrameImpl','PostProcessFrameImpl','StopImpl',
               'GetDuration','ResetData','InitDataOnPlay','SinglePlay','SingleStop','SingleRemove') if name=='MyPlayableSequence' else (
               'OnDestroy','InitImpl','PlayImpl','SetInitBeforeActive','SetActive') if name=='ApplyEnableOnPlay' else ()
    result={}
    for method in methods:
        values=[]
        for text in (source,migrated):
            body=method_body(text,method)
            # Only SetActive diagnostics differ between current ILSpy output
            # and the existing source port. Retain its null guards and returns.
            if method=='SetActive':
                body=re.sub(r'\b(?:GLoggerErrorInterpolatedStringHandler message|bool enabled);','',body)
                body=re.sub(r'message = new GLoggerErrorInterpolatedStringHandler\([^;]+;','',body)
                body=re.sub(r'if \(enabled\)\s*\{[^{}]*\}','',body)
                body=re.sub(r'(?:Log.Error|UnityEngine.Debug.LogError)\([^;]+;','',body)
            values.append(re.sub(r'\s+','',re.sub(r'//[^\n]*','',body)))
        assert values[0]==values[1], 'Current original runtime method differs from migrated port: '+name+'.'+method
        result[method]=digest(values[0].encode())
    return result


def write_raw(path,raw):
    if not path.exists() or path.read_bytes()!=raw: path.write_bytes(raw)


def project(text):
    starts = list(DOC.finditer(text))
    docs = [(int(m[1]), int(m[2]), text[m.start():starts[i+1].start() if i+1 < len(starts) else len(text)])
            for i, m in enumerate(starts)]
    classes = {key: int(SCRIPT.search(value)[1]) for kind, key, value in docs if kind == 114}
    keep = {key for key, cls in classes.items() if cls in SCRIPTS}
    # Remove empty sequence containers from the projection, bottom-up. Never
    # flatten nested sequences or round their original single-precision times.
    while True:
        empty = {key for kind, key, value in docs if classes.get(key) == -253978515 and key in keep
                 and not any(int(v) in keep for v in ROW.findall(value))}
        if not empty: break
        keep -= empty
    bodies = {key:value for _,key,value in docs}
    root_go = next(int(re.search(r'm_GameObject: \{fileID: (-?\d+)\}',value)[1])
                   for kind,_,value in docs if kind==4 and 'm_Father: {fileID: 0}' in value)
    root_sequence = next((key for key in keep if classes[key]==-253978515
                          and 'm_GameObject: {fileID: '+str(root_go)+'}' in bodies[key]), None)
    reachable, pending = set(), [root_sequence] if root_sequence else []
    while pending:
        key = pending.pop()
        if key in reachable: continue
        reachable.add(key)
        pending += [int(v) for v in ROW.findall(bodies[key]) if int(v) in keep]
    follow_gos = {re.search(r'm_GameObject: \{fileID: (-?\d+)\}',bodies[key])[1]
                  for key in reachable if classes[key]==-1107144664}
    keep = reachable | {key for key in keep if classes[key] not in (-253978515,-1107144664)
                        and re.search(r'm_GameObject: \{fileID: (-?\d+)\}',bodies[key])[1] in follow_gos}
    output = [text[:starts[0].start()]]
    counts = {}
    for kind, key, value in docs:
        if kind == 114:
            if key not in keep: continue
            cls = classes[key]
            counts[SCRIPTS[cls][1]] = counts.get(SCRIPTS[cls][1], 0) + 1
            value = ROW.sub(lambda m: m[0] if int(m[1]) in keep else '', value)
            path = ROOT / 'unity/Assets/Game/Runtime/Official' / SCRIPTS[cls][0] / (SCRIPTS[cls][1]+'.cs')
            guid = GUID.search(path.with_name(path.name+'.meta').read_text(encoding='utf-8-sig'))[1]
            value = SCRIPT.sub('m_Script: {fileID: 11500000, guid: '+guid+', type: 3}', value)
        elif kind == 1:
            value = re.sub(r'  - component: \{fileID: (-?\d+)\}\n',
                lambda m: '' if int(m[1]) in classes and int(m[1]) not in keep else m[0], value)
        output.append(value)
    result = ''.join(output)
    # Local pointers must still target an actual native object, including slot
    # transforms and the five inline effect applicators.
    ids = {int(m[2]) for m in DOC.finditer(result)} | {0}
    assert all(int(v) in ids for v in re.findall(r'\{fileID: (-?\d+)\}', result))
    return result, counts


def main():
    work = Path(os.environ['ZJCS_VALIDATION_DIR']).resolve()
    assert work.parent == (ROOT/'output/validation').resolve() and (work/'.zjcs-validation.json').is_file()
    export_receipt = CACHE/'native-export-all-source-manifest.json'
    proof = json.loads(export_receipt.read_bytes())
    assert proof['manifest'] == '884_156692'
    verified = {v['path']: v['sha256'] for v in proof['outputs']}
    inputs, objects, roots, projected = {}, [], [], {}

    def read(path):
        raw = path.read_bytes()
        assert digest(raw) == verified[path.relative_to(CACHE/'native-export-all').as_posix()], path
        inputs[path.as_posix()] = identity(path)
        return raw

    index_path = work/'movement-asset-guid-index.json'
    if not index_path.exists():
        index = {}
        for category in ('Effect', 'Shaders', 'Prefabs/EcEntity/Fight/FightSkill/Move'):
            for path in (EXPORTED/category).rglob('*.meta'):
                match = GUID.search(path.read_text(encoding='utf-8-sig'))
                if match: index[match[1]] = path.with_name(path.name[:-5]).relative_to(EXPORTED).as_posix()
        index_path.write_bytes(json.dumps(index).encode())
    index = {key:EXPORTED/value for key,value in json.loads(index_path.read_bytes()).items()}
    extra_index_path = work/'movement-extra-guid-index.json'
    extra = json.loads(extra_index_path.read_bytes()) if extra_index_path.exists() else dict(categories=[],assets={})
    index.update({key:EXPORTED/value for key,value in extra['assets'].items()})

    def resolve(guid):
        if guid in index: return index[guid]
        for category in ('Material','Materials','Mesh','Meshes','Texture2D','Textures','Shader','effect_shader','Light','Image','Resources','Packages','Partition','Prefabs'):
            if category in extra['categories']: continue
            print('Indexing native dependency category:',category,flush=True)
            for path in (EXPORTED/category).rglob('*.meta'):
                match=GUID.search(path.read_text(encoding='utf-8-sig'))
                if match:
                    source=path.with_name(path.name[:-5]); index[match[1]]=source
                    extra['assets'][match[1]]=source.relative_to(EXPORTED).as_posix()
            extra['categories'].append(category)
            extra_index_path.write_bytes(json.dumps(extra).encode())
            if guid in index: return index[guid]
        raise ValueError('Unresolved original native reference: '+guid)
    apk, schemas = Apk.current(), CurrentSchemas()
    try:
        assets = read_asset_manifest(apk.source.manifest_bytes('DefaultPackage', apk.archive))
        rules_path = ROOT/'unity/Assets/Official/Combat/battle-rules.json'
        entities = json.loads(rules_path.read_bytes())['entities']
        skills = {int(v['FightRoleCharacterComponent'].get(key, 0)) for v in entities.values()
                  if 'FightRoleCharacterComponent' in v for key in ('MoveSkillId','BackMoveSkillId','FixSpeedMoveSkillId')} - {0}
        actions = {int(row['MoveBehaviourId']) for key in skills for row in entities[str(key)]['FightSkillComponent']['SourceMoveList']}

        def component(go, name):
            return next(ref.component.deref() for ref in go.read().m_Component
                if ref.component.deref().type.name == 'MonoBehaviour'
                and ref.component.deref().read(check_read=False).m_Script.read().m_ClassName == name)

        for kind, ids, cls in (('skills', skills, 'MyFightSkill'), ('actions', actions, 'MyFightActionSkillMove')):
            for key in sorted(ids):
                asset = next(row for row in assets if row['path'].endswith('/'+str(key)+'.prefab'))
                owner = component(apk.asset(asset['bundle'], '/'+str(key)+'.prefab'), cls)
                fields = schemas.read(owner)
                objects.append(dict(source=apk.provenance(owner), type=cls, fields=fields))
                ptr = fields['myPlayablePrefab']
                if not ptr['m_PathID']: continue
                go = PPtr(**ptr, assetsfile=owner.assets_file).deref()
                root_path = EXPORTED / Path(asset['path']).relative_to('Assets').parent / (go.read().m_Name+'.prefab')
                raw = read(root_path).decode('utf-8-sig')
                result, counts = project(raw)
                if not counts.get('MyPlayableFollowBone'): continue
                projected[root_path] = result
                roots.append(dict(kind=kind, id=key, prefab=root_path.relative_to(EXPORTED).as_posix(), counts=counts))
                for obj in go.assets_file.objects.values():
                    if obj.type.name == 'MonoBehaviour':
                        name = obj.read(check_read=False).m_Script.read().m_ClassName
                        if name in {v[1] for v in SCRIPTS.values()}:
                            objects.append(dict(source=apk.provenance(obj), type=name, fields=schemas.read(obj)))
                    elif obj.type.name == 'ParticleSystemRenderer':
                        for ref in obj.read().m_Materials:
                            if not ref.m_PathID: continue
                            material = ref.deref().read(); material.m_Shader.deref()
                            for _, env in material.m_SavedProperties.m_TexEnvs:
                                if env.m_Texture.m_PathID: env.m_Texture.deref()
        (work/'movement-graph-selection.json').write_bytes(json.dumps(roots,indent=2).encode())
        assert sum(r['counts'].get('MyPlayableFollowBone',0) for r in roots) == 17, [(r['kind'],r['id'],r['counts']) for r in roots]
        local_scripts = {GUID.search((ROOT/'unity/Assets/Game/Runtime/Official'/folder/(name+'.cs.meta')).read_text(encoding='utf-8-sig'))[1]
                         for folder, name in SCRIPTS.values()}
        pending, found = list(projected), set()
        while pending:
            p = pending.pop()
            if p in found: continue
            found.add(p); raw = read(p); read(p.with_name(p.name+'.meta'))
            if p.suffix == '.png': continue
            text = projected.get(p, raw.decode('utf-8-sig'))
            for guid in set(GUID.findall(text)) - BUILTINS - local_scripts:
                pending.append(resolve(guid))
        native = {}
        for bundle in list(apk.loaded.values()):
            for file in serialized_files(bundle):
                for obj in file.objects.values():
                    if obj.type.name not in ('Material','Texture2D','Shader','Mesh'): continue
                    value = obj.read()
                    name = value.m_ParsedForm.m_Name if obj.type.name == 'Shader' else value.m_Name
                    native.setdefault((obj.type.name,name),{})[digest(obj.get_raw_data())] = (obj,value)
        shader_records = {}
        conversions = CACHE/'all-assets/windows-shaders'
        for p in (conversions/'Records').glob('*.json'):
            record = json.loads(p.read_bytes())
            shader_records[record['source']['object_sha256']] = (p,record)
        destinations, replacements, shaders, textures = {}, {}, [], []
        for p in sorted(found):
            target = DEST / p.relative_to(EXPORTED)
            if p.suffix == '.png': target = target.with_name(target.stem+'.texture.asset')
            elif '/Shaders/' in p.as_posix():
                target = ROOT/'unity/Assets/Game/Runtime/Presentation/Combat/Shaders'/('MovementEffect_'+p.stem+'.shader')
            target.parent.mkdir(parents=True,exist_ok=True); meta(target)
            old = GUID.search(read(p.with_name(p.name+'.meta')).decode('utf-8-sig'))[1]
            new = GUID.search(target.with_name(target.name+'.meta').read_text(encoding='utf-8-sig'))[1]
            destinations[p] = target; replacements[old] = new
        texture_guids = {replacements[GUID.search(read(p.with_name(p.name+'.meta')).decode())[1]] for p in found if p.suffix=='.png'}
        shader_guids = {GUID.search(t.with_name(t.name+'.meta').read_text(encoding='utf-8-sig'))[1] for t in destinations.values() if t.suffix=='.shader'}
        template_path = ROOT/'unity/Assets/Official/World/Map/13/NativeScene/Effect/CustomWater/Textures/default_water_mask.texture.asset'
        template = template_path.read_text(encoding='utf-8-sig').replace('--- !u!28 &2800000\n','--- !u!28 &1\n')
        for p, target in destinations.items():
            raw = read(p)
            if target.suffix == '.shader':
                name = re.search(r'(?m)^  m_Name: (.+)$', raw.decode('utf-8-sig'))[1]
                candidates = native['Shader',name]; assert len(candidates)==1,(name,len(candidates))
                obj,_ = next(iter(candidates.values()))
                record_path, record = shader_records[digest(obj.get_raw_data())]
                assert record['stripped_passes']==[], record_path
                source = conversions/record['path']; assert digest(source.read_bytes())==record['sha256']
                text=source.read_text(encoding='utf-8-sig')
                text=text.replace('Shader "'+name+'"','Shader "Zjcs/Official/Combat/MovementEffects/'+p.stem+'"',1)
                raw=text.encode()
                shaders.append(dict(source=apk.provenance(obj),object_sha256=digest(obj.get_raw_data()),conversion=identity(record_path),converted=identity(source)))
            elif p.suffix == '.png':
                name=p.stem
                if ('Texture2D',name) not in native: name=re.sub(r'_\d+$','',name)
                candidates=native['Texture2D',name]; assert len(candidates)==1,(p.stem,len(candidates))
                obj,value=next(iter(candidates.values())); rgba,mips,profile=decode_mips(value)
                exported_image=Image.open(p).convert('RGBA')
                decoded_image=Image.frombytes('RGBA',(value.m_Width,value.m_Height),rgba[:value.m_Width*value.m_Height*4])
                assert exported_image.size==decoded_image.size
                comparisons=[ImageChops.difference(exported_image,image).getextrema()
                             for image in (decoded_image,decoded_image.transpose(Image.Transpose.FLIP_TOP_BOTTOM))]
                matching=[i for i,value in enumerate(comparisons) if max(v[1] for v in value)<=1]
                # AssetRipper and astc_encoder round a small fraction of ASTC
                # channel values to adjacent bytes. This cross-check identifies
                # duplicate-name textures; source object/PNG hashes stay exact.
                # Output uses every original mip decoded below, never PNG pixels.
                assert matching, ('Converted texture differs from original',str(p),comparisons)
                orientation=['native','vertical_flip'][matching[0]]
                raw=native_texture(template,value,rgba).replace(b': \n',b':\n')
                textures.append(dict(source=apk.provenance(obj),object_sha256=digest(obj.get_raw_data()),compressed_sha256=digest(value.get_image_data()),rgba_sha256=digest(rgba),profile=profile,mips=mips,exported_png_orientation=orientation,exported_png_rgba_channel_max_delta=[v[1] for v in comparisons[matching[0]]],output=target.relative_to(ROOT).as_posix()))
            else:
                text=projected.get(p,raw.decode('utf-8-sig'))
                text=GUID.sub(lambda m:'guid: '+(m[1] if m[1] in BUILTINS|local_scripts else replacements[m[1]]),text)
                text=re.sub(r'(fileID: 2800000, guid: )([a-f0-9]{32})(, type: )3',lambda m:m[1]+m[2]+m[3]+('2' if m[2] in texture_guids else '3'),text)
                text=re.sub(r'(fileID: 4800000, guid: )([a-f0-9]{32})(, type: )2',lambda m:m[1]+m[2]+m[3]+('3' if m[2] in shader_guids else '2'),text)
                raw=text.encode()
            write_raw(target,raw)
            guid=GUID.search(target.with_name(target.name+'.meta').read_text(encoding='utf-8-sig'))[1]
            importer='ShaderImporter' if target.suffix=='.shader' else 'PrefabImporter' if target.suffix=='.prefab' else 'NativeFormatImporter'
            write_raw(target.with_name(target.name+'.meta'),('fileFormatVersion: 2\nguid: '+guid+'\n'+importer+':\n  externalObjects: {}\n').encode())
        result=dict(manifest=apk.version,skills=[],actions=[])
        for row in roots:
            result[row['kind']].append(dict(id=row['id'],resource='Combat/MovementEffects/'+Path(row['prefab']).with_suffix('').as_posix(),followNodes=row['counts']['MyPlayableFollowBone']))
        write_changed(DATA,json.dumps(result,ensure_ascii=False,indent=2)+'\n'); meta(DATA)
        paths=[DATA,DATA.with_name(DATA.name+'.meta')]
        paths += [p for t in destinations.values() for p in (t,t.with_name(t.name+'.meta'))]
        for path in list(destinations.values())+[DATA]:
            parent=path.parent
            while parent.name!='Assets':
                if not parent.with_name(parent.name+'.meta').exists(): meta(parent,folder=True)
                if parent.is_relative_to(DEST.parent.parent.parent): paths.append(parent.with_name(parent.name+'.meta'))
                parent=parent.parent
        originals=ROOT/'.tools/source-packages/current/884_156692/official-code'
        code=[]
        for file_id,(folder,name) in SCRIPTS.items():
            matches=list((originals/'client-source').glob(name+'.cs'))+list((originals/'game-framework-source/GameFramework').glob(name+'.cs'))
            assert len(matches)==1,(name,matches)
            source=matches[0].read_text(encoding='utf-8-sig')
            namespace=re.search(r'(?m)^namespace ([\w.]+)',source)
            script_identity='s\0\0\0'+(namespace[1] if namespace else '')+name
            assert struct.unpack('<i',MD4.new(script_identity.encode()).digest()[:4])[0]==file_id,(name,file_id)
            migrated=ROOT/'unity/Assets/Game/Runtime/Official'/folder/(name+'.cs')
            code.append(dict(original_file_id=file_id,original=identity(matches[0]),migrated=identity(migrated),
                current_runtime_method_equivalence=runtime_equivalence(source,migrated.read_text(encoding='utf-8-sig'),name)))
        receipt=dict(manifest=apk.version,selection=identity(rules_path),roots=roots,objects=objects,application=code,
            native_export_receipt=identity(export_receipt),native_inputs=list(inputs.values()),shaders=shaders,textures=textures,
            serialization_template=identity(template_path),generator=identity(Path(__file__)),
            conversion_helpers=[identity(ROOT/'scripts'/name) for name in ('prepare_unity_map13_native_scene.py','prepare_unity_map14_native_scene.py')],
            bindings=[identity(ROOT/path) for path in (
                'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleMovementEffects.cs',
                'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleSceneView.cs',
                'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleGridMapper.cs',
                'unity/Assets/Game/Runtime/Presentation/Combat/Zjcs.Presentation.Combat.asmdef',
                'unity/Assets/Game/Editor/BattleMovementEffects/OfficialBattleMovementEffectAcceptance.cs',
                'unity/Assets/Game/Editor/BattleMovementEffects/Zjcs.Editor.BattleMovementEffects.asmdef',
                'scripts/prepare_battle_movement_effect_validation.py')],
            outputs=[identity(p) for p in sorted(set(paths))],
            operation='Preserve native effect hierarchy, particle modules and renderer bindings; project effect leaves with original nested sequences and cue times. Animator, track and sound leaves use separate original-source adapters. GUID/script references adapted to Windows native assets; all texture mips decoded without resampling.',
            limits=['Movement effect subgraphs require a host with original entity body attachment and grid-ground adapters. Full combat skill/hit/scene graph migration remains separate.'])
        write_changed(RECEIPT,json.dumps(receipt,ensure_ascii=False,indent=2)+'\n')
        print('Movement effect graphs:',len(roots),'follow nodes:',sum(r['followNodes'] for k in ('skills','actions') for r in result[k]),'native assets:',len(found),'textures:',len(textures),'shaders:',len(shaders))
    finally:
        apk.archive.close()


if __name__ == '__main__':
    main()
