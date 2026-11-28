"""Stage current native HUD images with real catalog/rendering and round flow."""
import argparse
import json
from pathlib import Path
import re
import shutil
import subprocess

from official_apk import ROOT
from prepare_current_round_ui_validation import stage


def prepare(work):
    proof = json.loads((work/'current-hud-images-source-manifest.json').read_bytes())
    project = stage(work)
    staged = {row['path']:ROOT/row['staged'] for row in proof['outputs']}
    metas = subprocess.check_output(['rg','--no-heading','--with-filename',r'^guid: [a-f0-9]{32}\r?$',
        'unity/Assets','unity/Library/PackageCache','-g','*.meta'],cwd=ROOT,text=True,encoding='utf-8').splitlines()
    index = {}
    for row in metas:
        name,key = row.rsplit(':guid: ',1)
        index[key.strip()] = Path(str(ROOT/name)[:-5])
    for name,source in staged.items():
        if name.endswith('.meta'):
            match = re.search(rb'(?m)^guid: ([a-f0-9]{32})',source.read_bytes())
            if match:
                index[match[1].decode()] = ROOT/name[:-5]
    pending = [ROOT/'unity/Assets/Game/Resources/Combat'/name for name in (
        'OfficialBattleHudCatalog.asset',
        'battle-hud-graphic-bindings.json','battle-hud-graphic-bindings-source-manifest.json')]
    pending.append(ROOT/'unity/Assets/Official/Combat/HudMaterials/Resources/Combat/OfficialBattleHudMaterialAssets.asset')
    copied = set()
    while pending:
        path = pending.pop()
        if path in copied or path.suffix in ('.cs','.asmdef','.asmref'):
            continue
        copied.add(path)
        name = path.relative_to(ROOT).as_posix()
        source = staged.get(name,path)
        target = project/path.relative_to(ROOT/'unity')
        target.parent.mkdir(parents=True,exist_ok=True)
        shutil.copyfile(source,target)
        meta = staged.get(name+'.meta',Path(str(path)+'.meta'))
        if meta.is_file():
            shutil.copyfile(meta,Path(str(target)+'.meta'))
        # Asset PPtrs are the dependency boundary. Raw font/image bytes are not
        # scanned as YAML and source-code assembly ownership stays explicit.
        if source.suffix not in ('.png','.ttf','.otf','.woff','.wav','.mp3','.ogg'):
            for value in set(re.findall(rb'\bguid: ([a-f0-9]{32})',source.read_bytes())):
                key = value.decode()
                if key in index:
                    pending.append(index[key])
                elif key not in ('00000000000000000000000000000000','0000000000000000e000000000000000','0000000000000000f000000000000000'):
                    # TMP/UGUI package scripts were staged by the round project.
                    if not any(p.read_bytes().find(value)>=0 for p in (project/'Assets').rglob('*.cs.meta')):
                        raise ValueError('Restore an actual HUD asset dependency: '+key+' from '+name)
    editor = project/'Assets/Editor'
    definition = editor/'RoundUiValidation.asmdef'
    assembly = json.loads(definition.read_bytes())
    assembly['references'] = sorted(set(assembly['references'])|{'Unity.TextMeshPro'})
    definition.write_text(json.dumps(assembly,indent=2)+'\n',encoding='utf-8')
    combat = ROOT/'unity/Assets/Game/Runtime/Presentation/Combat'
    for name in ('OfficialBattleHudCatalog','OfficialBattleHudGraphicBindings','OfficialBattleHudRenderer',
                 'OfficialBattleHudAnimationPlayer','OfficialBattleHudMaterialAssets'):
        source = combat/(name+'.cs')
        shutil.copyfile(source,editor/source.name)
        shutil.copyfile(Path(str(source)+'.meta'),editor/(source.name+'.meta'))
    timeline = (combat/'OfficialBattleTimelineAssets.cs').read_text('utf-8-sig')
    start = timeline.index('    [Serializable] public sealed class BattleTimelineMaterial\n')
    end = timeline.index('    [Serializable] public sealed class BattleTimelineShader',start)
    (editor/'ActualBattleTimelineMaterialData.cs').write_text('using System;\nnamespace Zjcs.Presentation.Combat\n{\n'+timeline[start:end]+'\n}\n',encoding='utf-8')
    source = (ROOT/'scripts/fixtures/current_npc_icons_validation.cs').read_text('utf-8-sig')
    source = source.replace('CurrentNpcIconsValidation','CurrentHudImagesValidation')
    source = source.replace('Current NPC images:','Current HUD images:').replace('npc-icons-fixture.json','hud-images-fixture.json')
    start = source.index('        var partner=Resources.Load<OfflineProfessionImageCatalog>')
    end = source.index('        ContactSheet(sprites);',start)
    source = source[:start]+'''        var catalog=OfficialBattleHudCatalog.LoadDefault();
        var proof=JsonUtility.FromJson<Bindings>(File.ReadAllText(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"),"hud-image-bindings.json")));
        foreach(var row in proof.bindings)
        {
            var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(row.path);
            Require(sprite!=null && catalog.FindSprite(row.file)==sprite,"actual HUD image alias "+row.file);
        }
        CheckHud(catalog);
'''+source[end:]
    source = source.replace('Current NPC image GPU validation passed:','Current HUD image GPU validation passed:')
    source = source.replace('real battle and partner catalogs.','real HUD alias catalog, synthetic button callbacks and current round flow.')
    source = source.replace('for(int i=0;i<sprites.Count;i++)','for(int page=0;page*160<sprites.Count;page++)\n        {\n        for(int i=page*160;i<Math.Min(sprites.Count,(page+1)*160);i++)')
    source = source.replace('(i%16-7.5f)*2.5f,(5-i/16)*2.5f','(i%16-7.5f)*2.5f,(4.5f-(i%160)/16)*2.5f')
    source = source.replace('var renderer=item.AddComponent<SpriteRenderer>();','float size=Mathf.Max(sprites[i].bounds.size.x,sprites[i].bounds.size.y); if(size>0) item.transform.localScale=Vector3.one*(1.8f/size);\n            var renderer=item.AddComponent<SpriteRenderer>();')
    source = source.replace('"current-npc-images.png"','"current-hud-images-"+page+".png"')
    source = source.replace('Object.Destroy(root); Object.Destroy(cameraObject); Object.Destroy(target); Object.Destroy(pixels);','Object.DestroyImmediate(target); Object.DestroyImmediate(pixels);\n        foreach(Transform child in root.transform.Cast<Transform>().ToArray()) Object.DestroyImmediate(child.gameObject);\n        }\n        Object.Destroy(root); Object.Destroy(cameraObject);')
    support = (ROOT/'scripts/fixtures/current_hud_images_render_validation.cs').read_text('utf-8-sig')
    source = source.replace('    static void ContactSheet(List<Sprite> sprites)',support+'\n    static void ContactSheet(List<Sprite> sprites)')
    (editor/'CurrentHudImagesValidation.cs').write_text(source,encoding='utf-8')
    fixture = editor/'CurrentRoundUiValidation.cs'
    text = fixture.read_text('utf-8-sig')
    anchor = 'var assets = Resources.Load<BattleRoundPortraitAssets>("Combat/RoundUI/PortraitAssets");'
    if text.count(anchor)!=1:
        raise ValueError('Review the original round validation entry')
    fixture.write_text(text.replace(anchor,'CurrentHudImagesValidation.Check();\n        '+anchor),encoding='utf-8')
    by_identity = {row['identity']:row for row in proof['sprites']}
    (work/'hud-image-bindings.json').write_text(json.dumps(dict(bindings=[dict(file=row['file'],path=by_identity[row['original']]['path'].removeprefix('unity/')) for row in proof['bindings']]),indent=2)+'\n',encoding='utf-8')
    print('Prepared current HUD image validation:',len(copied),'actual asset dependencies;',project,flush=True)
    return project


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--work',type=Path,required=True)
    prepare(parser.parse_args().work.resolve())
