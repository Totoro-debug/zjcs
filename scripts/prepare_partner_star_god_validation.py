"""Create an owned Play Mode project for the installed original star-god page."""
import hashlib
import json
import shutil
import subprocess

from official_apk import ROOT
from validation_assemblies import stage_runtime_modules
from validation_workspace import validation_directory


def verified_installed_bytes(source, expected, relative):
    data = source.read_bytes()
    if hashlib.sha256(data).hexdigest() == expected:
        return data
    tracked = subprocess.run(['git', 'show', 'HEAD:' + relative], cwd=ROOT,
        check=True, capture_output=True).stdout
    if hashlib.sha256(tracked).hexdigest() == expected and data.splitlines() == tracked.splitlines():
        return tracked
    raise ValueError('Installed star-god asset changed: ' + relative)


def main():
    work = validation_directory()
    if not (work / '.zjcs-validation.json').is_file(): raise ValueError('Owned validation workspace required')
    project = work / 'star-god-unity'
    if project.exists(): raise ValueError('Preserve the existing validation project')
    runtime = ROOT / 'unity/Assets/Game/Runtime'
    # Native effect components are reached through prefab GUIDs, not C# references.
    pending = ['Zjcs.Official.PartnerRosterUI', 'Coffee.UIParticle', 'Zjcs.Offline.ProfessionUI']
    selected = stage_runtime_modules(
        project, runtime, pending,
        allowed_external={'Unity.ugui', 'UnityEngine.UI', 'Unity.TextMeshPro', 'Unity.Newtonsoft.Json', 'UniTask', 'Unity.RenderPipelines.Universal.Runtime', 'Unity.RenderPipelines.Core.Runtime'})
    editor = project / 'Assets/Editor'
    editor.mkdir(parents=True)
    shutil.copy2(ROOT / 'scripts/fixtures/partner_star_god_validation.cs', editor / 'PartnerStarGodValidation.cs')
    (editor / 'Validation.asmdef').write_text(json.dumps(dict(name='PartnerStarGodValidation', includePlatforms=['Editor'],
        references=['Unity.ugui', 'Unity.TextMeshPro', 'Unity.Newtonsoft.Json', 'Unity.RenderPipelines.Universal.Runtime', 'Unity.RenderPipelines.Core.Runtime',
            'Zjcs.Official.PartnerRosterUI', 'Zjcs.Official.Partners', 'Zjcs.Official.WorldUI',
            'Zjcs.Official.PartnersUI', 'Zjcs.Official.ProfessionUI', 'Zjcs.Offline.ProfessionUI'])), 'utf8', newline='\n')
    installed = json.loads((ROOT / 'assets/reference/partner-star-god-native-source-manifest.json').read_bytes())
    for name, expected in installed['installed_output_sha256'].items():
        source = ROOT / name
        data = verified_installed_bytes(source, expected, name)
        target = editor / source.name if '/Runtime/' in name else project / source.relative_to(ROOT / 'unity')
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(data)
    # AkEvent retains its original event ID; route the common click to its
    # already verified original PCM, shared with the native dialogue entry.
    clicks = json.loads((ROOT / 'assets/reference/partner-dialogue-entry-native-source-manifest.json').read_bytes())
    for name, expected in clicks['installed_output_sha256'].items():
        if '/Audio/Main_Cursor_Normal.wav' not in name: continue
        source = ROOT / name
        data = verified_installed_bytes(source, expected, name)
        target = project / source.relative_to(ROOT / 'unity')
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(data)
    (editor / 'star-god-fixture.json').write_text(json.dumps(dict(root=installed['roots']['NpcPanelStarGodContent'],
        **installed['star_god_images']), ensure_ascii=False), 'utf8', newline='\n')
    shutil.copytree(ROOT / 'unity/Assets/TextMesh Pro', project / 'Assets/TextMesh Pro')
    shutil.copy2(ROOT / 'unity/Assets/Official/World/Map/12/UI/Team/FZY4K.ttf', editor / 'FZY4K.ttf')
    packages = project / 'Packages'
    packages.mkdir()
    dependencies = {'com.unity.ugui': '1.0.0', 'com.unity.textmeshpro': '3.0.6',
        'com.unity.nuget.newtonsoft-json': '3.2.1'}
    for name in ('com.cysharp.unitask', 'com.unity.render-pipelines.universal'):
        shutil.copytree(ROOT / 'unity/Packages' / name, packages / name)
        dependencies[name] = 'file:' + name
    for name in ('ui', 'imgui', 'animation', 'audio', 'jsonserialize', 'imageconversion', 'screencapture',
                 'director', 'assetbundle', 'physics', 'physics2d', 'particlesystem'):
        dependencies['com.unity.modules.' + name] = '1.0.0'
    (packages / 'manifest.json').write_text(json.dumps(dict(dependencies=dependencies)), 'utf8', newline='\n')
    settings = project / 'ProjectSettings'
    settings.mkdir()
    shutil.copy2(ROOT / 'unity/ProjectSettings/ProjectVersion.txt', settings)
    print('Prepared installed star-god page and', len(selected), 'runtime assemblies:', project, flush=True)


if __name__ == '__main__':
    main()
