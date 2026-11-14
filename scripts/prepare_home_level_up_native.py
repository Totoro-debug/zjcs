"""Recover the complete selected official level-up notification prefab."""
import argparse
import hashlib
import json
import re
from pathlib import Path

from official_apk import ROOT
from prepare_current_partner_native import identity, documents
from prepare_home_avatar_native import HomeAvatarBinding
from prepare_home_level_up_runtime import build as runtime
from prepare_partner_card_native import guid, script_id, sha
from prepare_partner_gift_native import PartnerGiftBinding
from prepare_home_bottom_bar_validation import equivalent
from prepare_partner_input_dialog import metadata
from validation_workspace import validation_directory
from npc_travel_language import language

RECEIPT = ROOT/'assets/reference/home-level-up-native-source-manifest.json'
DEST = ROOT/'unity/Assets/Official/Home/UI/LevelUp'


class HomeLevelUpBinding(HomeAvatarBinding):
    gradient_evidence = PartnerGiftBinding.gradient_evidence

    def register_scripts(self):
        super().register_scripts()
        paths = {'LevelUpNotifyTip': ROOT/'unity/Assets/Game/Runtime/Official/HomeLevelUpUI/LevelUpNotifyTip.cs',
            'UGUIJszx.HorizontalLayoutGroupJSZX': ROOT/'unity/Assets/Game/Runtime/Official/WorldUI/HorizontalLayoutGroupJSZX.cs'}
        for name, path in paths.items():
            ref = dict(fileID=11500000, guid=guid(Path(str(path)+'.meta')), type=3)
            namespace, _, leaf = name.rpartition('.')
            self.scripts[name] = self.class_ids[script_id(namespace, leaf)] = ref
            self.script_guids.add(ref['guid'])
            self.script_sources[name] = dict(path=path.relative_to(ROOT).as_posix(), pointer=ref,
                sha256=sha(path.read_bytes()), meta_sha256=sha(Path(str(path)+'.meta').read_bytes()))

    def friendship_templates(self):
        self.recovery.recover('LevelUpNotifyTip', self.work/'level-up-native',
            bundle='defaultpackage_assets_prefabs_ui_commonnotify.bundle',
            native_relative='Prefabs/UI/CommonNotify/LevelUpNotifyTip.prefab')
        self.recovery.finalize()
        self.home_template_pointers = {}; self.empty_values = []

        def restore(left, right, address):
            if isinstance(left, dict) and isinstance(right, dict):
                for key in left.keys() & right.keys():
                    if right[key] is None and left[key] in ('', [], {}):
                        right[key] = left[key]; self.empty_values.append(address+'/'+key)
                    else: restore(left[key], right[key], address+'/'+key)
            elif isinstance(left, list) and isinstance(right, list) and len(left) == len(right):
                for i, (a, b) in enumerate(zip(left, right)): restore(a, b, address+'/'+str(i))

        result = []
        for native, _, panel, record in self.recovery.outputs:
            key = hashlib.md5(('zjcs:partner-current:'+native.stem+'.prefab').encode()).hexdigest()
            self.home_template_pointers.update({original: dict(fileID=file_id, guid=key, type=3)
                for original, file_id in panel['objectMap'].items()})
            self.script_guids.add(key)
            for file in (native, Path(str(native)+'.meta')): self.inputs[file.relative_to(ROOT).as_posix()] = sha(file.read_bytes())
            for original, ref in panel['external'].items(): self.objects[ref['guid']] = self.original(original)
            for original, file_id in panel['objectMap'].items():
                owner = self.original(original)
                self.learn(self.recovery.fields(owner), next(iter(panel['documents'][file_id].values())), owner)
            for row in panel['scripts']:
                owner = self.original(row['original']); original = self.recovery.fields(owner)
                fields = panel['documents'][row['nativeFileID']]['MonoBehaviour']
                restore(original, fields, row['original'])
                if row['type'] == 'PopupWindowSound':
                    for field in ('ShowSound', 'HideSound'): fields[field] = self.sound(original[field], owner)
            missing = {'.'.join(filter(None, (row['namespace'], row['type']))) for row in panel['scripts']}-self.scripts.keys()
            if missing: raise ValueError('Migrate original level-up scripts: '+repr(sorted(missing)))
            self.template_selection = dict(source=record['originalRoot'], mapped_objects=len(panel['objectMap']),
                scope='Complete original level-up notification; no hierarchy, material, layout or animation substitution.')
            result.append((native.stem, panel))
        cache = self.recovery.proof['component_cache']; self.inputs[cache['path']] = cache['sha256']
        return result

    def build(self):
        evidence = runtime()
        PartnerGiftBinding.build(self)
        receipt = json.loads((self.work/'partner-gift-native-source-manifest.json').read_bytes())
        keys = {'PropType.BaseAttack', 'PropType.BaseDefence', 'PropType.BaseMaxHp', 'PropType.BaseSpeed'}
        for relative in receipt['outputs']:
            if not relative.endswith('.prefab'): continue
            keys.update(doc['MonoBehaviour']['myKey'] for doc in documents((self.work/'gift-assets'/relative).read_text('utf8')).values()
                if doc.get('MonoBehaviour', {}).get('myKey'))
        texts, sources = language(keys)
        if keys-texts.keys(): raise ValueError('Missing original level-up property text: '+repr(keys-texts.keys()))
        receipt['inputs'].update(sources)
        folder = self.work/'gift-assets'
        previous = json.loads((folder/'partner-gift-texts.json').read_bytes())
        texts.update({row['key']: row['text'] for row in previous['texts']})
        for name in ('partner-gift-texts.json', 'partner-gift-texts.json.meta'):
            receipt['outputs'].pop(name); (folder/name).unlink()
        path = folder/'home-level-up-texts.json'
        path.write_text(json.dumps(dict(manifest=receipt['manifest'], texts=[dict(key=k, text=v) for k,v in sorted(texts.items())]),
            ensure_ascii=False, indent=2)+'\n', 'utf8', newline='\n')
        Path(str(path)+'.meta').write_text('fileFormatVersion: 2\nguid: '+hashlib.md5(b'zjcs:home-level-up-texts').hexdigest()
            +'\nTextScriptImporter:\n  externalObjects: {}\n', 'utf8', newline='\n')
        for item in (path, Path(str(path)+'.meta')): receipt['outputs'][item.name] = sha(item.read_bytes())
        used = set()
        for relative in receipt['outputs']:
            raw = (self.work/'gift-assets'/relative).read_bytes()
            if raw.startswith(b'%YAML'): used.update(re.findall(r'm_Script: \{[^\n}]*guid: ([0-9a-f]{32})', raw.decode('utf8')))
        receipt['script_bindings'] = {name: row for name, row in receipt['script_bindings'].items() if row['pointer']['guid'] in used}
        receipt['inputs'].update(evidence['inputs']); receipt['runtime_code'] = evidence['outputs']
        for name in ('prepare_home_level_up_validation.py', 'fixtures/home_level_up_native_validation.cs'):
            path = ROOT/'scripts'/name; receipt['inputs'][path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
        receipt.update(generator_sha256=sha(Path(__file__).read_bytes()), original_empty_value_restorations=self.empty_values,
            scope='Selected original level-up controller and complete native notification. Formal player/formula binding and runtime verification are maintained in the home feature page.')
        (self.work/RECEIPT.name).write_text(json.dumps(receipt, ensure_ascii=False, indent=2)+'\n', 'utf8', newline='\n')
        print('Original level-up notification:', len(receipt['outputs']), 'outputs', flush=True)


def install(work):
    receipt = json.loads((work/RECEIPT.name).read_bytes())
    if sha(Path(__file__).read_bytes()) != receipt['generator_sha256']: raise ValueError('Rebuild changed generator')
    for relative, expected in {**receipt['inputs'], **receipt['runtime_code']}.items():
        if sha((ROOT/relative).read_bytes()) != expected: raise ValueError('Changed original level-up input: '+relative)
    for row in receipt['script_bindings'].values():
        if sha((ROOT/row['path']).read_bytes()) != row['sha256'] or sha((ROOT/(row['path']+'.meta')).read_bytes()) != row['meta_sha256']:
            raise ValueError('Changed level-up script binding: '+row['path'])
    previous = json.loads(RECEIPT.read_bytes()).get('installed_output_sha256', {}) if RECEIPT.exists() else {}
    existing = {}
    for meta in (ROOT/'unity/Assets').rglob('*.meta'):
        path = Path(str(meta)[:-5])
        if not path.is_file(): continue
        key = guid(meta)
        if key in existing: raise ValueError('Existing GUID collision: '+key)
        existing[key] = path
    # Earlier modules can own an identical native texture under a different
    # conversion GUID. Compare full bytes before rebinding any serialized pointer.
    by_name = {}
    for key, path in existing.items(): by_name.setdefault(path.name, []).append((key, path))
    aliases, alias_proof = {}, []
    textures = {row['guid']: row for row in receipt['objects'].values() if row.get('type') == 'Texture2D'}
    def alias(row, key, path):
        source = work/'gift-assets'/row['output']; raw = source.read_bytes()
        if path.read_bytes() != raw: return False
        if row['guid'] in aliases: return aliases[row['guid']] == key
        aliases[row['guid']] = key
        alias_proof.append(dict(original_guid=row['guid'], bound_guid=key,
            source=row['source'], output=row['output'], reused=path.relative_to(ROOT).as_posix(), sha256=sha(raw)))
        return True
    for row in textures.values():
        if row['guid'] in existing: continue
        for key, path in by_name.get(Path(row['output']).name, []):
            if alias(row, key, path): break
    # Shared material/sprite owners can point to a canonically named texture
    # rather than Converted/<identity>. Only alias byte-identical Texture2D data.
    for relative in receipt['outputs']:
        if relative.endswith('.meta'): continue
        source = work/'gift-assets'/relative; key = guid(Path(str(source)+'.meta'))
        if key not in existing: continue
        left, right = source.read_bytes(), existing[key].read_bytes()
        if not left.startswith(b'%YAML') or not right.startswith(b'%YAML'): continue
        pattern = rb'guid: ([0-9a-f]{32})'
        if re.sub(pattern, b'guid: <pointer>', left) != re.sub(pattern, b'guid: <pointer>', right): continue
        for before, after in zip(re.findall(pattern, left), re.findall(pattern, right)):
            a, b = before.decode(), after.decode()
            if a != b and a in textures and b in existing: alias(textures[a], b, existing[b])
    def bind(raw):
        if raw.startswith(b'%YAML'):
            for before, after in aliases.items(): raw = raw.replace(('guid: '+before).encode(), ('guid: '+after).encode())
        return raw
    dedicated = {}
    # A shared GUID can have been serialized by another module's Editor (for
    # example a populated dynamic font atlas). Preserve that file. Keep this
    # selected-original payload under a module-owned GUID and rebind its closure.
    changed = True
    while changed:
        changed = False
        for relative in receipt['outputs']:
            if relative.endswith('.meta'): continue
            source = work/'gift-assets'/relative; original_key = guid(Path(str(source)+'.meta'))
            key = aliases.get(original_key, original_key)
            if key not in existing or equivalent(bind(source.read_bytes()), existing[key].read_bytes()): continue
            if existing[key].is_relative_to(DEST) and sha(existing[key].read_bytes()) == previous.get(existing[key].relative_to(ROOT).as_posix()):
                continue
            local = hashlib.md5(('zjcs:home-level-up-original:'+original_key).encode()).hexdigest()
            if local in existing and (not existing[local].is_relative_to(DEST)
                or sha(existing[local].read_bytes()) != previous.get(existing[local].relative_to(ROOT).as_posix())):
                raise ValueError('Preserve changed module-owned level-up asset: '+str(existing[local]))
            aliases[original_key] = local; dedicated[original_key] = dict(bound_guid=local, output=relative,
                preserved=existing[key].relative_to(ROOT).as_posix(), original_payload_sha256=sha(source.read_bytes()),
                preserved_sha256=sha(existing[key].read_bytes()))
            changed = True
    plan, outputs, reused = [], {}, []
    for relative, expected in receipt['outputs'].items():
        source = work/'gift-assets'/relative
        if sha(source.read_bytes()) != expected: raise ValueError('Changed staged output: '+relative)
        if source.suffix == '.meta': continue
        original_key = guid(Path(str(source)+'.meta')); key = aliases.get(original_key, original_key)
        meta_raw = Path(str(source)+'.meta').read_bytes().replace(('guid: '+original_key).encode(), ('guid: '+key).encode())
        target = existing.get(key, DEST/relative); raw = bind(source.read_bytes())
        same = target.exists() and equivalent(raw, target.read_bytes())
        if target.exists():
            if not same and (not target.is_relative_to(DEST) or sha(target.read_bytes()) != previous.get(target.relative_to(ROOT).as_posix())):
                raise ValueError('Preserve different existing asset: '+str(target))
            if not target.is_relative_to(DEST): reused.append(dict(path=target.relative_to(ROOT).as_posix(), guid=key, sha256=sha(target.read_bytes())))
        if not same: plan.extend(((raw, target), (meta_raw, Path(str(target)+'.meta'))))
        if target.is_relative_to(DEST):
            for file, staged in ((target, source), (Path(str(target)+'.meta'), Path(str(source)+'.meta'))):
                outputs[file.relative_to(ROOT).as_posix()] = sha(file.read_bytes() if same else meta_raw if file.suffix == '.meta' else raw)
    for raw, path in plan: path.parent.mkdir(parents=True, exist_ok=True); path.write_bytes(raw)
    for folder in (DEST, *[p for p in DEST.rglob('*') if p.is_dir()]):
        file = metadata(folder, 'DefaultImporter', True); outputs[file.relative_to(ROOT).as_posix()] = sha(file.read_bytes())
    receipt.update(installed_output_sha256=outputs, reused=reused, identical_texture_aliases=alias_proof,
        dedicated_original_bindings=dedicated, installed_root=DEST.relative_to(ROOT).as_posix())
    RECEIPT.write_text(json.dumps(receipt, ensure_ascii=False, indent=2)+'\n', 'utf8', newline='\n')
    print('Installed level-up notification:', len(outputs), 'files; reused', len(reused), flush=True)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(__doc__); parser.add_argument('--install', action='store_true'); args = parser.parse_args()
    work = validation_directory()
    if args.install: install(work)
    else: HomeLevelUpBinding(work).build()
