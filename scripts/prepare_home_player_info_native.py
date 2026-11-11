"""Recover the three original text/controller subtrees from MainCanvas."""
import copy
import hashlib
import json
import re
import shutil
from pathlib import Path

from official_apk import ROOT
from prepare_current_partner_native import documents
from prepare_home_bottom_bar_native import HomeBottomBarBinding
from prepare_home_player_info_runtime import build as runtime
from prepare_partner_card_native import guid, script_id, sha
from prepare_partner_gift_native import PartnerGiftBinding
from npc_travel_language import language
from official_manifest import read_asset_manifest
from validation_workspace import validation_directory


class HomePlayerInfoBinding(HomeBottomBarBinding):
    def gradient_evidence(self):
        return []

    def register_scripts(self):
        super().register_scripts()
        folder = ROOT / 'unity/Assets/Game/Runtime/Official/HomePlayerInfoUI'
        for name in ('PlayerNameText', 'PlayerLevelText', 'PlayerCombatRatingText'):
            path = folder / ('Home'+name+'.cs'); meta = Path(str(path)+'.meta')
            ref = dict(fileID=11500000, guid=guid(meta), type=3)
            self.scripts[name] = ref
            self.script_sources[name] = dict(path=path.relative_to(ROOT).as_posix(), pointer=ref,
                sha256=sha(path.read_bytes()), meta_sha256=sha(meta.read_bytes()))
            self.class_ids[script_id('', name)] = ref; self.script_guids.add(ref['guid'])

    def friendship_templates(self):
        recovery = self.recovery
        bundle = 'defaultpackage_assets_prefabs_ui_main.bundle'
        native = recovery.native / 'Prefabs/UI/Main/MainCanvas.prefab'
        native_docs = documents(native.read_text('utf8'))
        main = recovery.apk.asset(bundle, '/MainCanvas.prefab')
        pairs = {}

        def visit(source, native_id):
            go = source.read(); target = native_docs[native_id]['GameObject']
            assert go.m_Name == target['m_Name']
            pairs[native_id] = source
            transform = native_docs[target['m_Component'][0]['component']['fileID']]['RectTransform']
            children = go.m_Component[0].component.read().m_Children
            assert len(children) == len(transform['m_Children'])
            for child, other in zip(children, transform['m_Children']):
                visit(child.read().m_GameObject.deref(), native_docs[other['fileID']]['RectTransform']['m_GameObject']['fileID'])

        root, = [key for key, doc in native_docs.items() if doc.get('GameObject', {}).get('m_Name') == 'MainCanvas']
        visit(main, root)
        self.parent_context = {}
        for name, native_id in (('HomeNameText',1243052150384662), ('HomeLevelText',1411750186688204), ('HomeCombatRatingText',1416721661847891)):
            recovery.recover(name, self.work/'player-info-native', bundle=bundle,
                native_relative='Prefabs/UI/Main/MainCanvas.prefab', source_root=pairs[native_id], native_root_id=native_id)
            child = native_docs[native_id]['GameObject']['m_Component'][0]['component']['fileID']
            chain = []
            while child:
                rect = native_docs[child]['RectTransform']
                chain.append(dict(native_file_id=child, name=native_docs[rect['m_GameObject']['fileID']]['GameObject']['m_Name'], rect=copy.deepcopy(rect)))
                child = rect['m_Father']['fileID']
            self.parent_context[name] = chain
        recovery.finalize()
        self.home_template_pointers = {}
        result = []
        for native, output, panel, record in recovery.outputs:
            name = output.stem
            key = hashlib.md5(('zjcs:partner-current:'+name+'.prefab').encode()).hexdigest()
            self.home_template_pointers.update({source:dict(fileID=file_id,guid=key,type=3) for source,file_id in panel['objectMap'].items()})
            self.script_guids.add(key)
            for file in (native, Path(str(native)+'.meta')):
                self.inputs[file.relative_to(ROOT).as_posix()] = sha(file.read_bytes())
            for source, ref in panel['external'].items(): self.objects[ref['guid']] = self.original(source)
            missing = {'.'.join(filter(None,(s['namespace'],s['type']))) for s in panel['scripts']} - self.scripts.keys()
            if missing: raise ValueError('Migrate exact player-info scripts: '+repr(sorted(missing)))
            result.append((name,panel))
        cache = recovery.proof['component_cache']; self.inputs[cache['path']] = cache['sha256']
        self.template_selection = dict(scope='Complete original NameText, LevelTxt and CRText subtrees from MainCanvas, detached parent only; original ancestor RectTransforms retained as binding context.')
        return result

    def build(self):
        evidence = runtime()
        PartnerGiftBinding.build(self)
        receipt = json.loads((self.work/'partner-gift-native-source-manifest.json').read_bytes())
        queued=len(self.queue)
        font=self.chinese_font(read_asset_manifest(self.recovery.apk.source.manifest_bytes('DefaultPackage',self.recovery.apk.archive)))
        for target,key,raw in self.queue[queued:]:
            file=self.work/'gift-assets'/target;file.parent.mkdir(parents=True,exist_ok=True);file.write_bytes(raw)
            meta=Path(str(file)+'.meta')
            meta.write_text('fileFormatVersion: 2\nguid: '+key+'\nTrueTypeFontImporter:\n  externalObjects: {}\n  serializedVersion: 4\n  fontSize: 16\n  includeFontData: 1\n','utf8',newline='\n')
            for item in (file,meta):receipt['outputs'][item.relative_to(self.work/'gift-assets').as_posix()]=sha(item.read_bytes())
        config_obj=self.recovery.apk.asset('textmesh_pro_rawfonts_fontassetfontconfig','/FontAssetFontConfig.json')
        config_bytes=config_obj.read().m_Script.encode('utf8','surrogateescape')
        config_file=self.work/'gift-assets/FontAssetFontConfig.json';config_file.write_bytes(config_bytes)
        config_meta=Path(str(config_file)+'.meta');config_key=hashlib.md5(b'zjcs:home-player-info-font-config').hexdigest()
        config_meta.write_text('fileFormatVersion: 2\nguid: '+config_key+'\nTextScriptImporter:\n  externalObjects: {}\n','utf8',newline='\n')
        for file in (config_file,config_meta):receipt['outputs'][file.name]=sha(file.read_bytes())
        font.update(config_pointer=dict(fileID=4900000,guid=config_key,type=3),original_config=json.loads(config_bytes))
        receipt.update(font=font,objects=self.proof)
        receipt['inputs'].update(self.inputs)
        for name in ('FontAssetFontService','IFontAssetFontService','FontAssetFontConfig'):
            for suffix in ('.cs','.cs.meta'):
                file=ROOT/'unity/Assets/Game/Runtime/Official/ProfessionUI'/(name+suffix)
                receipt['inputs'][file.relative_to(ROOT).as_posix()]=sha(file.read_bytes())
        keys={'ui_common_combat_rating_style_empty','ui_common_level_style_3',
              'player_exp_max_desc1','player_exp_max_desc2','player_exp_max_title'}
        texts, sources=language(keys)
        if set(texts)!=keys: raise ValueError('Selected original player-info language entries are missing: '+repr(keys-texts.keys()))
        if any(receipt['inputs'].get(path)!=digest for path,digest in sources.items()):
            raise ValueError('Player-info language differs from the already verified selected Wwise manifest')
        for relative in ('partner-gift-texts.json','partner-gift-texts.json.meta'): receipt['outputs'].pop(relative)
        text_file=self.work/'gift-assets/home-player-info-texts.json'
        text_file.write_text(json.dumps(dict(manifest=self.recovery.apk.version,texts=[dict(key=key,text=texts[key]) for key in sorted(texts)]),ensure_ascii=False,indent=2)+'\n','utf8',newline='\n')
        text_meta=Path(str(text_file)+'.meta')
        text_meta.write_text('fileFormatVersion: 2\nguid: '+hashlib.md5(b'zjcs:home-player-info-texts').hexdigest()+'\nTextScriptImporter:\n  externalObjects: {}\n','utf8',newline='\n')
        for file in (text_file,text_meta):receipt['outputs'][file.name]=sha(file.read_bytes())
        used = set()
        for relative in receipt['outputs']:
            file = self.work/'gift-assets'/relative
            if file.suffix != '.meta' and file.read_bytes().startswith(b'%YAML'):
                used.update(re.findall(r'm_Script: \{[^\n}]*guid: ([0-9a-f]{32})',file.read_text('utf8')))
            target = self.work/'player-info-assets'/relative; target.parent.mkdir(parents=True,exist_ok=True)
            shutil.copyfile(file,target)
        receipt['script_bindings'] = {key:row for key,row in receipt['script_bindings'].items() if row['pointer']['guid'] in used}
        receipt['inputs'].update(evidence['sources'])
        receipt.update(runtime_code=evidence['outputs'], original_parent_context=self.parent_context,
            generator_sha256=sha(Path(__file__).read_bytes()),
            scope='Selected original player-info name/level/combat text subtrees and observers. Formal enclosing MainCanvas, blessing, HP and status controls remain pending.')
        for name in ('prepare_home_player_info_runtime.py','prepare_home_player_info_validation.py','fixtures/home_player_info_native_validation.cs','prepare_home_bottom_bar_native.py','prepare_home_repair_native.py'):
            file=ROOT/'scripts'/name; receipt['inputs'][file.relative_to(ROOT).as_posix()] = sha(file.read_bytes())
        (self.work/'home-player-info-native-source-manifest.json').write_text(json.dumps(receipt,ensure_ascii=False,indent=2)+'\n','utf8',newline='\n')
        print('Prepared original player-info observers:',len(receipt['outputs']),'files',flush=True)


if __name__ == '__main__':
    runtime()
    HomePlayerInfoBinding(validation_directory()).build()
