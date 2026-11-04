"""Restore the active official hand-in dialog for the shared mainline story host."""
from __future__ import annotations

import hashlib
import json
from pathlib import Path
import re
import subprocess
import sys

from official_apk import Apk, ROOT
from official_unity_schema import ScriptSchemas
from map14_handin_ui_source import extract
from map14_audio_source import AudioSource
from prepare_map16_story_source import decode_localization
from validation_workspace import temporary_validation_directory

VERSION = "884_156692"
ART = ROOT / "unity/Assets/Official/World/Mainline/HandIn"
RUNTIME = ROOT / "unity/Assets/Game/Resources/World/Mainline/HandIn"
RECEIPT = ROOT / "assets/reference/official-map/mainline-handin-ui-source-manifest.json"
EVENTS = {"Main_Cursor_Normal": 3772241779, "Main_Cursor_Back": 3686182067}


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def relative(path):
    return path.relative_to(ROOT).as_posix()


def input_record(path):
    raw = path.read_bytes()
    result = dict(path=relative(path))
    if path.parent == ROOT / "scripts" and path.suffix == ".py":
        raw = raw.replace(b"\r\n", b"\n")
        result["hashLineEndings"] = "LF"
    result["sha256"] = sha(raw)
    return result


def render_audio(apk, output):
    """Verify current Event/Play/Sound rendering before reusing installed PCM."""
    source = AudioSource(expected_version=apk.version, work_directory=output)
    try:
        for name, identity in EVENTS.items():
            found, banks = source.load_event(name)
            if found != identity or not banks:
                raise ValueError("Official hand-in event mapping changed: " + name)
        work = Path(source.temporary.name)
        txtp = work / "txtp"
        txtp.mkdir()
        wwiser = ROOT / ".tools/wwiser/wwiser.py"
        decoder = ROOT / ".tools/vgmstream/vgmstream-cli.exe"
        command = [sys.executable, str(wwiser), *map(str, sorted(work.glob("*.bnk"))),
                   "-g", "-d", "none", "-go", str(txtp), "-gw", "../", "-gv", "1.0",
                   "-gf", *map(str, EVENTS.values())]
        subprocess.run(command, check=True, stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
        audio, witnesses = {}, []
        for path in sorted(txtp.glob("*.txtp")):
            text = path.read_text(encoding="utf8")
            match = re.search(r"CAkEvent\[\d+\] (\d+)", text)
            name = next((name for name, identity in EVENTS.items()
                         if match and identity == int(match[1])), None)
            if name is None or name in audio or "master volume: auto" in text:
                raise ValueError("Ambiguous or normalized hand-in event rendering")
            leaves = [line for line in text.splitlines() if line.strip() and not line.lstrip().startswith("#")]
            if len(leaves) != 1 or not re.fullmatch(
                    r"\.\./\d+\.bnk #s\d+ #i  #v -?[\d.]+dB  ##\d+\.wem", leaves[0].strip()):
                raise ValueError("Hand-in audio requires additional original playback behavior")
            rendered = work / (name + ".wav")
            subprocess.run([str(decoder), "-i", "-o", str(rendered), str(path)], check=True,
                           stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
            installed = ROOT / "unity/Assets/Official/World/Map/12/Resources/Map12/PopupSound" / (name + ".wav")
            raw = rendered.read_bytes()
            if installed.read_bytes() != raw:
                raise ValueError("Current hand-in PCM differs from installed official audio: " + name)
            audio[name] = dict(event=name, sha256=sha(raw), unityAssetPath=installed.relative_to(ROOT / "unity").as_posix())
            # Keep the actual playback graph, excluding only the temporary bank path comment.
            graph = "\n".join(line for line in text.splitlines()
                              if not (line.startswith("# - ") and line.endswith(".bnk")))
            witnesses.append(dict(name=name, id=EVENTS[name], graph=graph,
                                  renderedSha256=sha(raw), reusedAsset=relative(installed)))
        if set(audio) != set(EVENTS):
            raise ValueError("Both original hand-in audio events are required")
        return dict(audio=audio, audioProof=dict(manifest=apk.version, packages=source.receipts(), events=witnesses,
                    tools=[dict(path=relative(tool), sha256=sha(tool.read_bytes())) for tool in (wwiser, decoder)]))
    finally:
        source.close()


def build():
    apk = Apk.current()
    if apk.version != VERSION:
        raise ValueError("Inspect the selected official hand-in version before migration")
    code = ROOT / ".tools/source-packages/current" / VERSION / "official-code"
    previous = json.loads(RECEIPT.read_bytes()) if RECEIPT.exists() else {}
    owned = {row["path"]: row["sha256"] for row in previous.get("outputs", [])}
    outputs = []

    def write(path, raw):
        name = relative(path)
        if path.exists() and path.read_bytes() != raw and sha(path.read_bytes()) != owned.get(name):
            raise ValueError("Refusing to overwrite another asset: " + name)
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(raw)
        outputs.append(path)

    def guid(path):
        return hashlib.md5(("zjcs-mainline-handin:" + relative(path)).encode()).hexdigest()

    def meta(path, folder=False):
        target = path.with_name(path.name + ".meta")
        if not target.exists():
            kind = "folderAsset: yes\nDefaultImporter" if folder else "DefaultImporter"
            write(target, (f"fileFormatVersion: 2\nguid: {guid(path)}\n{kind}:\n  externalObjects: {{}}\n"
                           "  userData:\n  assetBundleName:\n  assetBundleVariant:\n").encode())
        else:
            outputs.append(target)

    def sprite_meta(path, sprite):
        # Use Unity's existing native importer serialization, with source Sprite geometry.
        template = ROOT / "unity/Assets/Official/World/Map/14/UI/HandIn/Sprites/ImgBg_PopUps-20ee64ac9f.png.meta"
        raw = template.read_text(encoding="utf8")
        raw = re.sub(r"(?m)^guid: .*", "guid: " + guid(path), raw)
        raw = re.sub(r"(?m)^  spritePixelsToUnits: .*", f"  spritePixelsToUnits: {sprite['pixelsPerUnit']:g}", raw)
        pivot = [sprite["pivot"][0] / sprite["width"], sprite["pivot"][1] / sprite["height"]]
        raw = re.sub(r"(?m)^  spritePivot: .*", f"  spritePivot: {{x: {pivot[0]}, y: {pivot[1]}}}", raw)
        border = ", ".join(f"{axis}: {value:g}" for axis, value in zip("xyzw", sprite["border"]))
        raw = re.sub(r"(?m)^  spriteBorder: .*", "  spriteBorder: {" + border + "}", raw)
        write(path.with_name(path.name + ".meta"), raw.encode("utf8"))

    try:
        with temporary_validation_directory("mainline-handin-source-") as work:
            data = extract(Path(work), apk=apk, schemas=ScriptSchemas(code_root=code),
                           localize=decode_localization, audio_loader=render_audio)
            if [len(panel["nodes"]) for panel in data["panels"]] != [43, 19] or len(data["components"]) != 101:
                raise ValueError("Inspect the changed official hand-in hierarchy")
            data["mapClassId"] = 0
            replacements = {}
            for sprite in data["sprites"]:
                source = Path(sprite["unityAssetPath"])
                if sha(source.read_bytes()) != sprite["sha256"]:
                    raise ValueError("Extracted hand-in Sprite changed")
                target = ART / "Sprites" / source.name
                write(target, source.read_bytes())
                sprite_meta(target, sprite)
                replacements[sprite["uri"]] = "res://" + relative(target)
                sprite["unityAssetPath"] = target.relative_to(ROOT / "unity").as_posix()

            def remap(value):
                if isinstance(value, str):
                    return replacements.get(value, value)
                if isinstance(value, list):
                    return [remap(item) for item in value]
                if isinstance(value, dict):
                    return {replacements.get(key, key): remap(item) for key, item in value.items()}
                return value

            data = remap(data)
            shader = ROOT / "unity/Assets/Game/Runtime/Official/UI/Map14HandInGray.shader"
            if Path(data["grayMaterial"]["shaderPath"]).read_bytes() != shader.read_bytes():
                raise ValueError("Current official hand-in gray shader requires conversion")
            data["grayMaterial"]["shaderPath"] = shader.relative_to(ROOT / "unity").as_posix()
            legacy = ROOT / "unity/Assets/Game/Resources/Map14/UI/HandIn"
            animations = [legacy / "Btn_Base_Click.controller", *[legacy / (name + ".anim")
                for name in ("Normal", "Highlighted", "Pressed", "Selected", "Disabled")]]
            old_guids = {re.search(r"^guid:\s*([a-f0-9]+)", path.with_name(path.name + ".meta").read_text(), re.M)[1]:
                         guid(RUNTIME / path.name) for path in animations}
            for path in animations:
                raw = path.read_text(encoding="utf8")
                for original, replacement in old_guids.items():
                    raw = raw.replace("guid: " + original, "guid: " + replacement)
                target = RUNTIME / path.name
                write(target, raw.encode("utf8"))
                native_meta = path.with_name(path.name + ".meta").read_text(encoding="utf8")
                native_meta = re.sub(r"(?m)^guid: .*", "guid: " + guid(target), native_meta)
                write(target.with_name(target.name + ".meta"), native_meta.encode("utf8"))
            payload = (json.dumps(data, ensure_ascii=False, indent=2) + "\n").encode("utf8")
            for target in (ART / "handin-ui.json", RUNTIME / "catalog.json"):
                write(target, payload)
                meta(target)
            for target in (ART / "Sprites", RUNTIME):
                folder = target
                while folder != ROOT / "unity/Assets":
                    meta(folder, True)
                    folder = folder.parent
            inputs = [Path(__file__), ROOT / "scripts/map14_handin_ui_source.py", ROOT / "scripts/map14_audio_source.py",
                      ROOT / "scripts/prepare_map16_story_source.py", shader, *animations,
                      ROOT / "unity/Assets/Official/World/Map/14/UI/HandIn/Sprites/ImgBg_PopUps-20ee64ac9f.png.meta",
                      *[path.with_name(path.name + ".meta") for path in animations],
                      *[ROOT / "unity" / record["unityAssetPath"] for record in data["audio"].values()],
                      *[code / "client-source" / (name + ".cs") for name in
                        ("DialogHandInItemDialog", "HandInItemNode", "CanHandInItemNode", "ItemIcon", "ItemProgress")]]
            receipt = dict(format=1, manifest=VERSION, operation=__doc__,
                source=data["source"], localizationInputs=data["languageInputs"], audioProof=data["audioProof"],
                counts=dict(dialogNodes=43, iconNodes=19, components=101, sprites=len(data["sprites"])),
                inputs=[input_record(path) for path in inputs],
                outputs=[dict(path=relative(path), sha256=sha(path.read_bytes())) for path in dict.fromkeys(outputs)])
            RECEIPT.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + "\n", encoding="utf8", newline="\n")
            print(json.dumps(receipt["counts"]))
            return receipt
    finally:
        apk.archive.close()


if __name__ == "__main__":
    build()
