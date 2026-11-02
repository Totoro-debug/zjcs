"""Run the independent player physics code against recorded numerical observations.

The test and production assembly do not load or compile any Spine SDK code.
"""
import json
import subprocess
from pathlib import Path

from validation_workspace import validation_directory

ROOT = Path(__file__).resolve().parents[1]


def main():
    workspace = validation_directory() / 'player-physics-rules'
    workspace.mkdir(exist_ok=True)
    config = json.loads((ROOT / 'unity-tools.json').read_bytes())
    editor = Path(config['editorExecutable'])
    mono_root = editor.parent / 'Data/MonoBleedingEdge'
    mono = mono_root / 'bin/mono.exe'
    csc = mono_root / 'lib/mono/4.5/csc.exe'
    output = workspace / 'PlayerPhysicsValidation.exe'
    subprocess.run([str(mono), str(csc), '/nologo', '/out:' + str(output),
        str(ROOT / 'unity/Assets/Game/Runtime/Offline/ProfessionUI/ProfessionNativeSpringConstraint.cs'),
        str(ROOT / 'scripts/fixtures/partner_player_physics_validation.cs')], check=True)
    subprocess.run([str(mono), str(output),
        str(ROOT / 'scripts/fixtures/partner_player_physics_reference.txt')], check=True)


if __name__ == '__main__':
    main()
