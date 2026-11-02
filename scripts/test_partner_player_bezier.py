"""Verify the actual native player curve math using recorded numerical values.

No animation SDK is loaded or compiled by this regression test.
"""
import json
from pathlib import Path
import subprocess

from validation_workspace import validation_directory

ROOT = Path(__file__).resolve().parents[1]


def main():
    output = validation_directory() / 'player-bezier-rules'
    output.mkdir(exist_ok=True)
    config = json.loads((ROOT / 'unity-tools.json').read_bytes())
    mono_root = Path(config['editorExecutable']).parent / 'Data/MonoBleedingEdge'
    mono, csc = mono_root / 'bin/mono.exe', mono_root / 'lib/mono/4.5/csc.exe'
    exe = output / 'PlayerBezierValidation.exe'
    subprocess.run([str(mono), str(csc), '/nologo', '/out:' + str(exe),
        str(ROOT / 'unity/Assets/Game/Runtime/Offline/ProfessionUI/ProfessionNativeBezierPolyline.cs'),
        str(ROOT / 'scripts/fixtures/partner_player_bezier_validation.cs')], check=True)
    subprocess.run([str(mono), str(exe), str(ROOT / 'scripts/fixtures/partner_player_bezier_reference.txt')], check=True)


if __name__ == '__main__':
    main()
