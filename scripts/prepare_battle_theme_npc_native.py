"""Recover the selected original battle theme NPC and bind its Windows UI dependencies."""
import argparse, copy, hashlib, json, re, sys, uuid
from pathlib import Path
r = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(r / 'scripts'))
from prepare_partner_card_native import guid, sha, script_id, language
from prepare_current_partner_native import identity
from prepare_partner_detail_actor_native import PartnerDetailActorBinding
from prepare_partner_detail_window_native import PartnerDetailWindowBinding
from prepare_partner_gift_native import PartnerGiftBinding

class ThemeBinding(PartnerDetailActorBinding):

    def __init__(self, w):
        self.actor_rows = {}
        self.native_actors = {}
        self.native_atlases = {}
        self.actor_roots = []
        self.dynamic_assets = []
        self.texture_addresses = []
        self.texture_objects = {}
        self.prior_path = r / 'assets/reference/partner-detail-actor-native-source-manifest.json'
        self.prior = json.loads(self.prior_path.read_bytes())
        assert self.prior['manifest'] == '884_156692'
        self.reused = {}
        self.reused_files = {}
        self.reuse_pointers = {**self.prior['native_actor_assets'], **self.prior['native_atlases']}
        self.reuse_pointers.update({row['original']: row['asset'] for row in self.prior['dynamic_assets']})
        self.installed = {}
        for p, expected in self.prior['installed_output_sha256'].items():
            if not p.endswith('.meta'):
                continue
            path = r / p
            if path.is_file() and sha(path.read_bytes()) == expected:
                self.installed[guid(path)] = Path(str(path)[:-5])
        PartnerDetailWindowBinding.__init__(self, w)
        self.inputs[self.prior_path.relative_to(r).as_posix()] = sha(self.prior_path.read_bytes())

    def verify_reused_closure(self, path):
        relative = path.relative_to(r).as_posix()
        if relative in self.reused_files:
            return
        for file in (path, Path(str(path) + '.meta')):
            key = file.relative_to(r).as_posix()
            expected = self.prior['installed_output_sha256'][key]
            if sha(file.read_bytes()) != expected:
                raise ValueError('Shared original UI dependency changed: ' + key)
            self.reused_files[key] = expected
        raw = path.read_bytes()
        if raw.startswith(b'%YAML'):
            for key in set(re.findall(b'guid: ([0-9a-f]{32})', raw)):
                dependency = self.installed.get(key.decode())
                if dependency is not None:
                    self.verify_reused_closure(dependency)

    def register_scripts(self):
        super().register_scripts()
        for name, p in {'AnimationSequence': 'unity/Assets/Game/Runtime/Official/BattleThemeUI/AnimationSequence.cs', 'UIMesh': 'unity/Assets/Game/Runtime/Official/WorldUI/UIMesh.cs'}.items():
            path = r / p
            pointer = self.script_pointer(path)
            self.scripts[name] = pointer
            self.class_ids[script_id('', name)] = pointer

    def load(self, source_guid):
        if source_guid in self.redirects:
            return self.redirects[source_guid]
        obj = self.objects.get(source_guid)
        if obj is not None:
            key = identity(obj)
            if key in self.reuse_pointers:
                pointer = dict(self.reuse_pointers[key])
                pointer['guid'] = self.prior['isolated_serialization_guids'].get(pointer['guid'], pointer['guid'])
                path = self.installed[pointer['guid']]
                self.verify_reused_closure(path)
                assert sha(path.read_bytes()) == self.prior['installed_output_sha256'][path.relative_to(r).as_posix()]
                source = self.prior['actor_data']['sources'].get(key) or self.prior['objects'].get(key)
                if source is None:
                    source = next((row for row in self.prior['dynamic_assets'] if row['original'] == key), None)
                assert source is not None, ('No reusable original hash', key)
                expected = source.get('payload_sha256') or source.get('original_sha256')
                assert sha(obj.get_raw_data()) == expected, ('Different original object', key)
                self.redirects[source_guid] = pointer
                self.script_guids.add(pointer['guid'])
                self.reused[key] = dict(original_sha256=expected, source=self.recovery.apk.provenance(obj), path=path.relative_to(r).as_posix(), sha256=sha(path.read_bytes()), pointer=pointer)
                return pointer
            if obj.type.name == 'MonoBehaviour':
                script = obj.read(check_read=False).m_Script.deref().read()
                if script.m_ClassName == 'SpineAnimationSplitBinarySO':
                    rows = [row for row in self.prior['actor_data']['actors'] if row.get('splitSource') == key]
                    assert rows, key
                    return self.load_original(rows[0]['source'])
        return super().load(source_guid)

    def load_original(self, key):
        if key in self.reuse_pointers and (not any((identity(obj) == key for obj in self.objects.values()))):
            source_guid = hashlib.md5(('zjcs:theme-ui-reused-source:' + key).encode()).hexdigest()
            self.objects[source_guid] = self.original(key)
        return super().load_original(key)

    def friendship_templates(self):
        self.recovery.recover('FightThemeNpcLuoTianYi', self.work / 'partner-native', bundle='defaultpackage_assets_prefabs_ui_fight_fightmainpanel_themenpc_fightthemenpcluotianyi.bundle', native_relative='Prefabs/UI/Fight/FightMainPanel/ThemeNpc/FightThemeNpcLuoTianYi.prefab')
        self.recovery.finalize()
        templates = []
        self.theme_text_keys = set()
        for native, _, original, evidence in self.recovery.outputs:
            data = copy.deepcopy(original)
            self.theme_text_keys.update(
                data['documents'][row['nativeFileID']]['MonoBehaviour']['myKey']
                for row in data['scripts'] if row['type'] == 'MultiLanguageText'
            )
            render_root = data['documents'][data['root']]['GameObject']['m_Component'][0]['component']['fileID']
            boots = []
            for graphic in [row for row in data['scripts'] if row['type'] == 'SkeletonGraphic']:
                go = data['documents'][graphic['nativeFileID']]['MonoBehaviour']['m_GameObject']['fileID']
                boot = max(data['documents']) + 1
                data['documents'][go]['GameObject']['m_Component'].insert(1, dict(component=dict(fileID=boot)))
                data['documents'][boot] = dict(MonoBehaviour=dict(m_ObjectHideFlags=0, m_CorrespondingSourceObject=dict(fileID=0), m_PrefabInstance=dict(fileID=0), m_PrefabAsset=dict(fileID=0), m_GameObject=dict(fileID=go), m_Enabled=1, m_EditorHideFlags=0, m_Script=self.script_pointer(r / 'unity/Assets/Game/Runtime/Offline/PartnersUI/PartnerNativeUiActor.cs'), m_Name='', m_EditorClassIdentifier='', graphic=dict(fileID=graphic['nativeFileID']), renderRoot=dict(fileID=render_root)))
                boots.append(boot)
            for p in (native, Path(str(native) + '.meta')):
                self.inputs[p.relative_to(r).as_posix()] = sha(p.read_bytes())
            templates.append((evidence['name'], data))
            self.actor_roots.append(dict(source=evidence, bootstraps=boots))
        self.template_selection = self.actor_roots
        return templates

def install(work):
    receipt = json.loads((work / 'theme-npc-staged-source-manifest.json').read_bytes())
    target = r / 'unity/Assets/Official/Combat/ThemeUI/Resources/Combat/Theme/LuoTianYi'
    staged = work / 'gift-assets'
    outputs = {key: value for key, value in receipt['outputs'].items() if not key.startswith('partner-gift-texts.json')}
    guids = {}
    for relative, expected in outputs.items():
        path = staged / relative
        if sha(path.read_bytes()) != expected:
            raise ValueError('Staged theme asset changed: ' + relative)
        if relative.endswith('.meta'):
            original = guid(path)
            guids[original] = uuid.uuid5(uuid.NAMESPACE_URL, 'zjcs:battle-theme-ui-native:' + original).hex
    installed = {}
    font_receipt_path = r / '.tools/source-packages/current/884_156692/native-conversion/native-export-all-source-manifest.json'
    font_receipt = json.loads(font_receipt_path.read_bytes())
    if font_receipt['manifest'] != receipt['manifest']:
        raise ValueError('The existing original UI font belongs to another selected version.')
    font_export, = [row for row in font_receipt['outputs']
        if row['path'] == 'ExportedProject/Assets/TextMesh Pro/Fonts/FZY4K.ttf']
    original_font = font_receipt_path.with_name('native-export-all') / font_export['path']
    font_path = r / 'unity/Assets/Official/Commerce/Art/fonts/FZY4K.ttf'
    font_source = dict(font_export, path=font_path.relative_to(r).as_posix())
    if sha(original_font.read_bytes()) != font_export['sha256'] or sha(font_path.read_bytes()) != font_export['sha256']:
        raise ValueError('The existing original FZY4K font changed.')
    font_meta = Path(str(font_path) + '.meta')
    font_guid = guid(font_meta)
    receipt['inputs'][font_receipt_path.relative_to(r).as_posix()] = sha(font_receipt_path.read_bytes())
    receipt['inputs'][original_font.relative_to(r).as_posix()] = font_export['sha256']
    receipt['reused_installed_output_sha256'][font_source['path']] = font_source['sha256']
    receipt['reused_installed_output_sha256'][font_meta.relative_to(r).as_posix()] = sha(font_meta.read_bytes())
    receipt['dynamic_font_source'] = dict(font_source, guid=font_guid, selected_export=font_export,
        adaptation='Restore the stripped runtime source Font of the original dynamic FZY4K TMP asset; retain its original face, material, atlas settings and typography.')

    def write(path, raw):
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(raw)
        installed[path.relative_to(r).as_posix()] = sha(raw)
    for relative in outputs:
        raw = (staged / relative).read_bytes()
        if relative.endswith('.meta') or raw.startswith(b'%YAML'):
            for original, replacement in guids.items():
                raw = raw.replace(('guid: ' + original).encode(), ('guid: ' + replacement).encode())
        if relative == 'TextMesh Pro/Assets/Fonts & Materials/FZY4K SDF - dynamic_0.asset':
            if b'm_SourceFontFile: {fileID: 0}' not in raw or b'm_AtlasPopulationMode: 1' not in raw:
                raise ValueError('Review the selected original dynamic theme font.')
            raw = raw.replace(b'm_SourceFontFile: {fileID: 0}',
                ('m_SourceFontFile: {fileID: 12800000, guid: ' + font_guid + ', type: 3}').encode())
            raw = raw.replace(b'm_SourceFontFileGUID: null', ('m_SourceFontFileGUID: ' + font_guid).encode())
        write(target / relative, raw)
    for folder in sorted({parent for key in outputs for parent in (target / key).parents if parent != r / 'unity/Assets/Official/Combat' and parent.is_relative_to(r / 'unity/Assets/Official/Combat')}, key=str):
        meta = Path(str(folder) + '.meta')
        key = uuid.uuid5(uuid.NAMESPACE_URL, 'zjcs:battle-theme-ui-folder:' + folder.relative_to(r).as_posix()).hex
        if meta.exists():
            if guid(meta) != key:
                raise ValueError('Theme folder belongs to another asset: ' + str(folder))
            installed[meta.relative_to(r).as_posix()] = sha(meta.read_bytes())
            continue
        write(meta, ('fileFormatVersion: 2\nguid: ' + key + '\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n').encode())
    receipt['installed_output_sha256'] = installed
    # Exported shader programs have sparse keyword domains. Register these exact
    # programs with OfficialSparseShaderVariants rather than compiling absent
    # combinations or inventing a replacement program.
    sparse_domains = {}
    for name, expected, expected_passes in (
        ('5dfd400a7875c92f.shader', 6, 1),
        ('59de19d261370c02.shader', 260, 2),
    ):
        path = target / 'Converted' / name
        blocks = re.findall(r'CGPROGRAM\s*\n(.*?)ENDCG', path.read_text(encoding='utf-8'), re.S)
        if len(blocks) != expected_passes:
            raise ValueError('Original theme shader pass count changed: ' + name)
        masks = []
        keywords = None
        for block in blocks:
            names = sorted(re.findall(r'(?m)^#pragma multi_compile(?:_local)? __ (\w+)\s*$', block))
            if keywords is not None and keywords != names:
                raise ValueError('Original theme shader pass keyword domains differ: ' + name)
            keywords = names
            depth, programs = 0, set()
            for branch in re.finditer(r'(?m)^\s*#(if|ifdef|ifndef|elif|else|endif)\b([^\r\n]*)', block):
                directive, condition = branch.groups()
                selects = directive == 'if' and depth == 0 or directive == 'elif' and depth == 1
                if directive == 'else' and depth == 1:
                    raise ValueError('Original theme shader gained an unbounded fallback: ' + name)
                if directive in ('if', 'ifdef', 'ifndef'):
                    depth += 1
                elif directive == 'endif':
                    depth -= 1
                if not selects:
                    continue
                terms = re.findall(r'(!?)defined\((\w+)\)', condition)
                if sorted(key for _, key in terms) != names or ' && '.join(
                    ('!' if negate else '') + 'defined(' + key + ')' for negate, key in terms
                ) != condition.strip():
                    raise ValueError('Original theme shader selection is not exact: ' + name)
                signature = sum(1 << names.index(key) for negate, key in terms if not negate)
                if signature in programs:
                    raise ValueError('Original theme shader selection is ambiguous: ' + name)
                programs.add(signature)
            if depth != 0 or len(programs) != expected or masks and masks[0] != sorted(programs):
                raise ValueError('Original theme shader program domain changed: ' + name)
            masks.append(sorted(programs))
        sparse_domains[path.relative_to(r).as_posix()] = dict(
            sha256=installed[path.relative_to(r).as_posix()], keywords=keywords,
            programs_per_pass=expected, passes=expected_passes, program_masks=masks[0],
            binding='Zjcs.Editor.OfficialSparseShaderVariants')
    receipt['windows_sparse_shader_domains'] = sparse_domains
    receipt['isolated_serialization_guids'] = guids
    receipt['generator_sha256'] = sha(Path(__file__).read_bytes())
    receipt['ui_pose_backend_sha256'] = {
        relative: sha((r / relative).read_bytes())
        for relative in (
            'unity/Assets/Game/Runtime/Offline/ProfessionUI/ProfessionNativeBonePose.cs',
            'unity/Assets/Game/Runtime/Offline/ProfessionUI/ProfessionNativeUiGraphic.cs',
        )
    }
    receipt['scope'] = 'Original theme NPC prefab and dependency conversion. Runtime host and render validation are recorded in battle-execution.md.'
    output = r / 'assets/reference/battle-animation/theme-npc-native-source-manifest.json'
    output.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', encoding='utf-8', newline='\n')
    print('Installed original theme NPC assets:', len(installed), flush=True)

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--work', type=Path, required=True)
    parser.add_argument('--install', action='store_true')
    parser.add_argument('--refresh-runtime-bindings', action='store_true',
                        help='Verify the installed payload and refresh only local UI binding identities.')
    args = parser.parse_args()
    work = args.work.resolve()
    if not work.is_relative_to(r / 'output/validation') or not (work / '.zjcs-validation.json').is_file():
        raise ValueError('Use a marked repository validation workspace.')
    if args.refresh_runtime_bindings:
        if args.install:
            raise ValueError('Refresh existing runtime bindings separately from asset installation.')
        output = r / 'assets/reference/battle-animation/theme-npc-native-source-manifest.json'
        receipt = json.loads(output.read_bytes())
        for group in ('installed_output_sha256', 'reused_installed_output_sha256'):
            for relative, expected in receipt[group].items():
                if sha((r / relative).read_bytes()) != expected:
                    raise ValueError('Installed original theme payload changed: ' + relative)
        receipt['ui_pose_backend_sha256'] = {
            relative: sha((r / relative).read_bytes())
            for relative in receipt['ui_pose_backend_sha256']
        }
        receipt['generator_sha256'] = sha(Path(__file__).read_bytes())
        output.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + '\n',
                          encoding='utf-8', newline='\n')
        print('Verified the original theme payload and refreshed local UI binding identities.', flush=True)
        return
    binding = ThemeBinding(work)
    try:
        PartnerGiftBinding.build(binding)
        receipt = json.loads((work / 'partner-gift-native-source-manifest.json').read_bytes())
        texts, sources = language(binding.theme_text_keys)
        if set(texts) != binding.theme_text_keys:
            raise ValueError('Selected original theme NPC localization entries are missing.')
        for path, digest in sources.items():
            if receipt['inputs'].get(path) != digest:
                raise ValueError('Theme localization differs from the verified selected language input: ' + path)
        payload = dict(manifest=receipt['manifest'], texts=[dict(key=key, text=texts[key]) for key in sorted(texts)])
        localized = work / 'gift-assets/theme-npc-texts.json'
        localized.write_text(json.dumps(payload, ensure_ascii=False, indent=2) + '\n', encoding='utf-8', newline='\n')
        localized_meta = Path(str(localized) + '.meta')
        localized_meta.write_text('fileFormatVersion: 2\nguid: ' + uuid.uuid5(uuid.NAMESPACE_URL, 'zjcs:battle-theme-npc-texts').hex + '\nTextScriptImporter:\n  externalObjects: {}\n', encoding='utf-8', newline='\n')
        receipt['outputs'][localized.name] = sha(localized.read_bytes())
        receipt['outputs'][localized_meta.name] = sha(localized_meta.read_bytes())
        receipt['reused_original_assets'] = binding.reused
        receipt['reused_installed_output_sha256'] = binding.reused_files
        receipt['generator_sha256'] = sha(Path(__file__).read_bytes())
        receipt['scope'] = 'Staged original theme NPC and exact original UI dependencies; not an assertion of runtime validation.'
        (work / 'theme-npc-staged-source-manifest.json').write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', encoding='utf-8', newline='\n')
    finally:
        binding.recovery.apk.archive.close()
    if args.install:
        install(work)
if __name__ == '__main__':
    main()
