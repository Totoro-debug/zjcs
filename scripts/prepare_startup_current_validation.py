"""Stage the original startup resource closure for independent Unity validation."""
import json
from pathlib import Path
import shutil

from official_apk import ROOT
from validation_assemblies import stage_runtime_modules
from validation_workspace import validation_directory


def prepare():
    project = validation_directory() / 'startup-unity'
    if project.exists():
        raise FileExistsError('Keep the existing validation project')
    project.mkdir()
    for folder in ('Packages', 'ProjectSettings'):
        shutil.copytree(ROOT / 'unity' / folder, project / folder)
    roots = ['Zjcs.Offline.Startup', 'Zjcs.Official.WorldUI']
    stage_runtime_modules(project, ROOT / 'unity/Assets/Game/Runtime', roots,
                          allowed_external=['Unity.ugui', 'Unity.TextMeshPro'])
    for relative in ('Assets/Official/Startup', 'Assets/Game/Resources/Startup', 'Assets/TextMesh Pro'):
        target = project / relative
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copytree(ROOT / 'unity' / relative, target)
        shutil.copy2(ROOT / 'unity' / (relative + '.meta'), str(target) + '.meta')
    for relative in ('Assets/Game/Runtime/Official/Startup/StartupTextMeshPro.shader',
                     'Assets/Official/Commerce/Art/fonts/FZY4K.ttf'):
        target = project / relative
        target.parent.mkdir(parents=True, exist_ok=True)
        for suffix in ('', '.meta'):
            shutil.copy2(ROOT / 'unity' / (relative + suffix), str(target) + suffix)
    editor = project / 'Assets/StartupCurrent/Editor'
    editor.mkdir(parents=True)
    shutil.copy2(ROOT / 'unity/Assets/Game/Editor/Startup/OfficialStartupTools.cs', editor)
    shutil.copy2(ROOT / 'scripts/fixtures/startup_current_resources_validation.cs', editor)
    (editor / 'StartupCurrent.asmdef').write_text(json.dumps(dict(
        name='StartupCurrent', references=roots + ['Zjcs.Official.Startup', 'Unity.ugui', 'Unity.TextMeshPro'],
        includePlatforms=['Editor'])), 'utf8')
    print(project, flush=True)


if __name__ == '__main__':
    prepare()
