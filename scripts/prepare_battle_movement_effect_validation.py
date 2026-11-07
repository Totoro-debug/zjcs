"""Prepare the actual effect/ground adapters and native assets in a small Unity project."""
import json
import os
import re
import shutil
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def prepare(work):
    assert work.parent == (ROOT/'output/validation').resolve() and (work/'.zjcs-validation.json').is_file()
    project = work/'movement-effects-unity'
    project.mkdir(exist_ok=True)
    obsolete=project/'Assets/Game/Runtime/Official/Combat/Editor'
    assert obsolete.resolve().is_relative_to(work) and not obsolete.is_symlink()
    if obsolete.is_dir(): shutil.rmtree(obsolete)

    def copy(path):
        target = project/path.relative_to(ROOT/'unity')
        target.parent.mkdir(parents=True,exist_ok=True)
        shutil.copy2(path,target)

    for module in ('Combat','StoryPlayables','StoryAttachment','StoryFollowBone','StoryGroundEffects'):
        source = ROOT/'unity/Assets/Game/Runtime/Official'/module
        for path in source.rglob('*'):
            if path.is_file() and 'Editor' not in path.relative_to(source).parts and path.suffix in ('.cs','.asmdef','.meta'): copy(path)
    source=ROOT/'unity/Assets/Game/Runtime/Offline/Combat'
    for path in source.rglob('*'):
        if path.is_file() and path.suffix in ('.cs','.asmref','.meta'): copy(path)
    presentation = ROOT/'unity/Assets/Game/Runtime/Presentation/Combat'
    for name in ('OfficialBattleMovementEffects.cs','OfficialBattleGroundEffectLibrary.cs',
                 'OfficialBattleGridMapper.cs','OfficialBattleSceneBindings.cs'):
        copy(presentation/name); copy(presentation/(name+'.meta'))
    asmdef = project/'Assets/Game/Runtime/Presentation/Combat/Zjcs.Presentation.Combat.asmdef'
    asmdef.write_bytes(json.dumps(dict(name='Zjcs.Presentation.Combat',autoReferenced=False,
        references=['Zjcs.Combat.Official','Zjcs.Official.StoryPlayables','Zjcs.Official.StoryGroundEffects',
                    'Zjcs.Official.StoryAttachment'])).encode())
    for folder in ('unity/Assets/Official/Combat/MovementEffects','unity/Assets/Official/Combat/GroundEffects',
                   'unity/Assets/Official/World/Map/13/NativePipeline','unity/Assets/Game/Editor/BattleMovementEffects'):
        for path in (ROOT/folder).rglob('*'):
            if path.is_file(): copy(path)
    for name in ('movement-effects.json','ground-effects.json'):
        path=ROOT/'unity/Assets/Game/Resources/Combat'/name; copy(path);copy(path.with_name(path.name+'.meta'))
    for name in ('MovementEffect_Noise_Mask_Pro_0.shader','MovementEffect_Sprite-Lit-Default_0.shader','GroundEffectNoiseMaskPro.shader'):
        path=presentation/'Shaders'/name;copy(path);copy(path.with_name(path.name+'.meta'))
    for name in ('UniversalRenderPipelineGlobalSettings.asset','UniversalRenderPipelineGlobalSettings.asset.meta'):
        copy(ROOT/'unity/Assets'/name)
    for name in ('ProjectVersion.txt','ProjectSettings.asset','GraphicsSettings.asset','QualitySettings.asset','TagManager.asset'):
        copy(ROOT/'unity/ProjectSettings'/name)
    # Native renderer resources include converted post-processing shaders and
    # LUT textures outside the pipeline folder. Copy their actual GUID closure.
    copied=set()
    pending=list((ROOT/'unity/Assets/Official/World/Map/13/NativePipeline').rglob('*.asset'))
    pending.append(ROOT/'unity/Assets/UniversalRenderPipelineGlobalSettings.asset')
    while pending:
        patterns=set()
        for path in pending:
            patterns.update('guid: '+v for v in re.findall(r'guid: ([a-f0-9]{32})',path.read_text(encoding='utf-8-sig')) if not v.startswith('0000000000000000'))
        if not patterns: break
        pattern_file=work/'movement-pipeline-guids.txt';pattern_file.write_bytes(('\n'.join(sorted(patterns))+'\n').encode())
        # Find tracked GUIDs in Git's object store, then verify each selected
        # live sidecar before copying. Opening every working-tree sidecar makes
        # this small fixture depend on the full repository's disk scan.
        result=subprocess.run(['git','grep','--cached','-l','-F','-f',str(pattern_file),'--','unity/**/*.meta'],cwd=ROOT,capture_output=True,check=False)
        assert result.returncode in (0,1),result.stderr.decode(errors='replace')
        pending=[]
        for value in result.stdout.decode().splitlines():
            path=ROOT/value[:-5]
            if path in copied or path.is_relative_to(ROOT/'unity/Packages'): continue
            live_guids={'guid: '+v for v in re.findall(r'guid: ([a-f0-9]{32})',path.with_name(path.name+'.meta').read_text(encoding='utf-8-sig'))}
            if not patterns.intersection(live_guids):
                raise ValueError('Tracked pipeline GUID differs from its live sidecar: '+str(path))
            copied.add(path);copy(path);copy(path.with_name(path.name+'.meta'))
            if path.suffix in ('.asset','.mat','.prefab'):pending.append(path)
    manifest=json.loads((ROOT/'unity/Packages/manifest.json').read_bytes())
    for name,value in list(manifest['dependencies'].items()):
        if value.startswith('file:'):
            manifest['dependencies'][name]='file:'+(ROOT/'unity/Packages'/value[5:]).as_posix()
    (project/'Packages').mkdir(exist_ok=True)
    (project/'Packages/manifest.json').write_bytes(json.dumps(manifest,indent=2).encode())
    return project


if __name__ == '__main__':
    print(prepare(Path(os.environ['ZJCS_VALIDATION_DIR']).resolve()))
