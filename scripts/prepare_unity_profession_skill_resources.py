"""Select the exact native skill-icon/pool-item prefab dependency closure."""
import json
import re
import shutil
from pathlib import Path

from prepare_unity_profession_native_bindings import (
    ROOT, CACHE, TEXT, BUILTIN, GUID, SCRIPT, MANAGED, guid, sha, runtime_script, build as base_plan)
from prepare_unity_profession_character_resources import record


def build():
    original = base_plan('all')
    exports = CACHE / 'typed/export/ExportedProject/Assets'
    index = {}
    for path in exports.rglob('*.meta'):
        index[guid(path.with_suffix(''))] = path.with_suffix('').relative_to(exports).as_posix()
    known = dict(original['assets'])
    restored = json.loads((ROOT/'assets/reference/profession-ui/native-export-source-manifest.json').read_bytes())
    for row in restored['outputs']:
        path = ROOT/row['path']
        if sha(path) != row['sha256']:
            raise ValueError('Restored original native registry changed: '+str(path))
        rel = path.relative_to(CACHE/'typed/restored').as_posix()
        known[rel] = dict(path=row['path'],sha256=row['sha256'],owner='source-verified original managed-reference restoration')
    for rel, row in known.items():
        index[guid(ROOT/row['path'])] = rel
    # Registry restoration also contains converter PNGs. Native Texture2D
    # assets deliberately retain their GUIDs; prefer those verified payloads
    # when resolving an image reference instead of reinstalling duplicates.
    textures = json.loads((ROOT/original['verified_native_textures']['path']).read_bytes())
    for row in textures['native_assets']:
        path = ROOT/row['path']
        if sha(path) != row['sha256']:
            raise ValueError('Verified native texture changed: '+str(path))
        rel = row['native_relative_path']
        known[rel] = dict(path=row['path'], sha256=row['sha256'], owner='verified original native Texture2D')
        index[row['guid']] = rel
    seeds = ['Prefabs/UI/Item/ItemIconSkill.prefab', 'Prefabs/UI/Profession/ProfessionSkillItem.prefab',
             'Prefabs/UI/Item/Attachments/NotGotTip.prefab', 'Prefabs/UI/Item/Attachments/ProfessionMismatch.prefab',
             'Prefabs/UI/Item/Attachments/GMDisabled.prefab', 'Prefabs/UI/Item/Attachments/WearingAppendAppearanceGo.prefab']
    selected, scripts, missing, managed, shaders = {}, {}, [], {}, []
    queue = [(p, 'explicit original skill presentation prefab') for p in seeds]
    while queue:
        rel, owner = queue.pop()
        if rel in selected or rel in scripts:
            continue
        row = known.get(rel, dict(path=(exports/rel).relative_to(ROOT).as_posix(), sha256=sha(exports/rel), owner=owner)) if rel not in known else known[rel]
        path = ROOT/row['path']
        if path.suffix == '.cs':
            target = (ROOT/'unity/Library/PackageCache/com.unity.textmeshpro@3.0.6/Scripts/Runtime/TMP_ColorGradient.cs') if path.stem == 'TMP_ColorGradient' else runtime_script(path)
            scripts[rel] = None if target is None else dict(path=target.relative_to(ROOT).as_posix(), guid=guid(target), sha256=sha(target))
            continue
        if sha(path) != row['sha256']:
            raise ValueError('Original skill asset changed: '+rel)
        selected[rel] = dict(row, owner=owner)
        if path.suffix.lower() not in TEXT:
            continue
        text = path.read_text(encoding='utf-8')
        for m in re.finditer(r'(?m)^      type:\n        class: ([^\n]*)\n        ns:([^\n]*)\n        asm:([^\n]*)', text):
            cls, ns, asm = (v.strip() for v in m.groups())
            if cls:
                key = (asm, ns, cls); managed[key] = managed.get(key, 0)+1
        if path.suffix == '.shader':
            shaders.append(rel)
        for value in set(GUID.findall(text))-BUILTIN:
            if value not in index:
                missing.append(dict(owner=rel, guid=value))
            else:
                queue.append((index[value], rel))
    plan = dict(original, scope='info-skills', seeds=seeds, assets=selected, script_bindings=scripts,
        pending_scripts=[dict(script=k) for k,v in scripts.items() if v is None],
        managed_reference_types=[dict(assembly=k[0], namespace=k[1], type=k[2], count=v, binding=list(MANAGED[k]) if k in MANAGED else None) for k,v in sorted(managed.items())],
        shaders=shaders, shader_bindings={k:v for k,v in original['shader_bindings'].items() if k in shaders},
        pending_shaders=[s for s in shaders if s not in original['shader_bindings']], missing=missing,
        generator=record(Path(__file__)),
        operation='Take the full original dependency closure of six original skill-icon, slot and attachment prefabs. Preserve native GUIDs, object IDs, hierarchy, layout, animations, serialized bindings and managed references; no constructed replacement UI.')
    target = CACHE/'typed/native-bindings-info-skills.json'
    target.write_text(json.dumps(plan, ensure_ascii=False, indent=2)+'\n', encoding='utf-8', newline='\n')
    print('Skill native closure:', len(selected), 'assets;',len(scripts),'scripts;',plan['pending_scripts'],plan['pending_shaders'],missing)
    return plan


def install(project):
    from install_unity_profession_native_assets import install as install_closure
    from validation_workspace import require_temporary_unity_project
    project = project.resolve()
    require_temporary_unity_project(project)
    plan = json.loads((CACHE/'typed/native-bindings-info-skills.json').read_bytes())
    for row in list(plan['script_bindings'].values()) + list(plan['shader_bindings'].values()):
        relative = row['path'].removeprefix('unity/')
        source = ROOT/'unity'/relative
        target = project/relative
        if sha(source) != row['sha256']:
            raise ValueError('Native skill binding changed: '+relative)
        if not target.exists() or sha(target) != sha(source):
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(source, target)
            shutil.copyfile(source.with_name(source.name+'.meta'), target.with_name(target.name+'.meta'))
    staged = install_closure('info-skills', project)
    receipt_path = ROOT/'assets/reference/profession-ui/skill-resources-source-manifest.json'
    previous = json.loads(receipt_path.read_bytes()) if receipt_path.exists() else {}
    owned = set(previous.get('added', []))
    previous_outputs = {r['installed']['path'].removeprefix('unity/'):r for r in previous.get('installation', [])}
    outputs, added, reused = [], [], []
    for row in staged['outputs']:
        target = ROOT/'unity'/row['installed_path']
        temporary = project/row['installed_path']
        meta = target.with_name(target.name+'.meta')
        existed = target.exists()
        if existed and row['installed_path'] in owned:
            old = previous_outputs[row['installed_path']]
            if sha(target) != old['installed']['sha256'] or sha(meta) != old['meta']['sha256']:
                raise ValueError('This session original skill asset changed externally: '+str(target))
            shutil.copyfile(temporary, target)
            shutil.copyfile(temporary.with_name(temporary.name+'.meta'), meta)
            added.append(row['installed_path'])
        elif existed:
            if not meta.exists() or guid(target) != row['guid']:
                raise ValueError('Existing native skill asset identity differs: '+str(target))
            # Shared fonts/atlases may already contain Unity import or dynamic
            # glyph updates. Retain the current source project byte-for-byte,
            # and validate that same asset in the isolated project.
            shutil.copyfile(target, temporary)
            shutil.copyfile(meta, temporary.with_name(temporary.name+'.meta'))
            reused.append(row['installed_path'])
        else:
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(temporary, target)
            shutil.copyfile(temporary.with_name(temporary.name+'.meta'), meta)
            added.append(row['installed_path'])
        outputs.append(dict(native_relative_path=row['native_relative_path'],
            source=row['source'], original_transformed_sha256=row['sha256'],
            installed=record(target), meta=record(meta), guid=row['guid'], reused=row['installed_path'] in reused))
    folders = []
    for row in staged['folder_metadata']:
        target = ROOT/'unity'/row['path']
        if not target.exists():
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(project/row['path'], target)
        folders.append(record(target))
    proof = dict(format=1, manifest=plan['manifest'],
        generator=record(Path(__file__)), installer=staged['installer'], closure=plan, installation=outputs, folders=folders,
        added=added, reused=reused,
        operation='Install the complete original skill-icon/slot/attachment prefab closure with verified script/shader/managed-reference bindings. Add only absent assets to the source project; retain existing shared font/atlas/material assets and GUIDs byte-for-byte, and copy those retained assets into the isolated project for validation.',
        validated_scope='Pending actual native prefab Play Mode validation.',
        limitations=['Skill configuration/player/pool/popup services are consumed interfaces; full original information/upgrade page integration remains incomplete.', 'No complete profession pages, save validation or Windows release is claimed.'])
    receipt_path.write_text(json.dumps(proof, ensure_ascii=False, indent=2)+'\n', encoding='utf-8', newline='\n')
    print('Installed',len(added),'new original skill assets; retained',len(reused),'existing shared assets.')
    return proof


if __name__ == '__main__':
    import argparse
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--install-project', type=Path)
    args = parser.parse_args()
    build()
    if args.install_project:
        install(args.install_project)
