"""Extract the official character-creation prologue voice-over from the pinned Wwise bank.

Inputs come from the fixed APK (854_156383), WwiseBnk package:
  wwisebnk_assets_wwisedata_android_zh_cn_voice_prologue_pck.rawfile
The package is an AKPK v1 container holding the `voice_prologue` Wwise bank plus
the six streamed Vorbis WEM files.  Every `dialogue.voice` event of
`Opening/opening-story.json` is resolved through the bank HIRC graph
(event -> play action -> sound -> source media id), decoded with vgmstream and
re-encoded as a deterministic OGG Vorbis payload.

Outputs:
  unity/Assets/StreamingAssets/Opening/voice/<event>.ogg (+ <event>.ogg.meta)
  assets/reference/character-creation/opening-voice-source-manifest.json

Run with the repository Python 3.12 toolchain (the interpreter that matches
.tools/character-art-python, also required by the UnityPy preparation scripts):
  D:/miniconda3/envs/comm/python.exe scripts/prepare_opening_voice.py
"""
from __future__ import annotations

import hashlib
import json
import shutil
import struct
import subprocess
import sys
import uuid
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
TOOLCHAIN = ROOT / ".tools/character-art-python"
WWISER = ROOT / ".tools/wwiser"
VGMSTREAM = ROOT / ".tools/vgmstream/vgmstream-cli.exe"
for entry in (ROOT / "scripts", TOOLCHAIN, WWISER):
    if str(entry) not in sys.path:
        sys.path.insert(0, str(entry))

VERSION = "854_156383"
PACKAGE = "WwiseBnk"
PCK_BUNDLE = "wwisebnk_assets_wwisedata_android_zh_cn_voice_prologue_pck.rawfile"
PCK_SIZE = 236441
PCK_SHA256 = "eff6311c3106d6289bba7246a18537bd9cb74bb872edd4acb76d5b94c584ccad"
BANK_NAME = "voice_prologue"
BANK_VERSION = 145
BANK_ID = 1597066971  # 0x5f314edb = FNV-1 32 bit of "voice_prologue"
BANK_SHA256 = "4ae89e4ea15def53c45efda743cd708a3e159f098a68ad57c7b36639e4b44f4d"
PCK_PATH = ROOT / ".tools/source-packages/WwiseBnk" / PCK_BUNDLE
PACKAGE_DIR = PCK_PATH.parent
BUNDLE_INDEX = PACKAGE_DIR / "bundle-index.json"
BUNDLE_SOURCES = PACKAGE_DIR / "bundle-sources.json"
STORY = ROOT / "unity/Assets/StreamingAssets/Opening/opening-story.json"
VOICE_DIR = ROOT / "unity/Assets/StreamingAssets/Opening/voice"
REFERENCE = ROOT / "assets/reference/character-creation/opening-voice-source-manifest.json"
WORK = ROOT / "output/opening-voice-work"
META_GUID_NAMESPACE = "zjcs-opening:"
META_TEMPLATE = (
    "fileFormatVersion: 2\nguid: %s\n%sDefaultImporter:\n  externalObjects: {}\n"
    "  userData: \n  assetBundleName: \n  assetBundleVariant: \n"
)


def sha256(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1 << 20), b""):
            digest.update(block)
    return digest.hexdigest()


def meta_guid(relative: str) -> str:
    return uuid.uuid5(uuid.NAMESPACE_URL, META_GUID_NAMESPACE + relative).hex


def write_with_meta(path: Path, data: bytes) -> dict:
    """Write a StreamingAssets payload and keep an existing Unity .meta file intact."""
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(data)
    meta = path.with_name(path.name + ".meta")
    if not meta.exists():
        relative = path.relative_to(ROOT).as_posix()
        meta.write_text(META_TEMPLATE % (meta_guid(relative), ""), encoding="utf-8")
    return {"path": path.relative_to(ROOT).as_posix(), "bytes": len(data), "sha256": sha256(data)}


def write_folder_meta(folder: Path) -> None:
    meta = folder.with_name(folder.name + ".meta")
    if meta.exists():
        return
    relative = folder.relative_to(ROOT).as_posix()
    meta.write_text(META_TEMPLATE % (meta_guid(relative), "folderAsset: yes\n"), encoding="utf-8")


# ------------------------------------------------------------- Wwise package
def read_entry(raw: bytes, offset: int) -> dict:
    entry_id, language, size, position, extra = struct.unpack_from("<5I", raw, offset)
    return {"id": entry_id, "language": language, "bytes": size, "offset": position, "extra": extra}


def parse_pck(raw: bytes) -> dict:
    """Parse the AKPK v1 bank/file tables (validated against every cached package)."""
    if raw[:4] != b"AKPK":
        raise ValueError("Wwise package does not start with AKPK")
    header_bytes, version = struct.unpack_from("<II", raw, 4)
    if version != 1:
        raise ValueError("Unsupported Wwise package version: " + str(version))
    bank_offset = 28 + struct.unpack_from("<I", raw, 12)[0]
    bank_bytes = struct.unpack_from("<I", raw, 16)[0]
    file_bytes = struct.unpack_from("<I", raw, 20)[0]
    banks = []
    offset = bank_offset + 4
    for _ in range(struct.unpack_from("<I", raw, bank_offset)[0]):
        banks.append(read_entry(raw, offset))
        offset += 20
    if offset != bank_offset + bank_bytes:
        raise ValueError("Wwise bank table does not match its declared size")
    files = []
    offset += 4
    for _ in range(struct.unpack_from("<I", raw, offset - 4)[0]):
        files.append(read_entry(raw, offset))
        offset += 20
    if offset != bank_offset + bank_bytes + file_bytes:
        raise ValueError("Wwise file table does not match its declared size")
    for entry in banks:
        if raw[entry["offset"]:entry["offset"] + 4] != b"BKHD":
            raise ValueError("Wwise bank offset does not point at BKHD")
        if entry["offset"] + entry["bytes"] > len(raw):
            raise ValueError("Wwise bank overruns the package")
    for entry in files:
        if raw[entry["offset"]:entry["offset"] + 4] != b"RIFF":
            raise ValueError("Wwise media offset does not point at RIFF")
        if entry["offset"] + entry["bytes"] > len(raw):
            raise ValueError("Wwise media overruns the package")
    return {"header_bytes": header_bytes, "banks": banks, "files": files}


def bank_chunks(bank: bytes) -> dict:
    chunks = {}
    offset = 0
    while offset + 8 <= len(bank):
        tag = bank[offset:offset + 4]
        size = struct.unpack_from("<I", bank, offset + 4)[0]
        chunks[tag.decode("ascii", "replace")] = (offset + 8, size)
        offset += 8 + size
    if offset != len(bank):
        raise ValueError("Wwise bank chunks do not cover the whole bank")
    for tag in ("BKHD", "DIDX", "DATA", "HIRC"):
        if tag not in chunks:
            raise ValueError("Wwise bank is missing the " + tag + " chunk")
    return chunks


def media_index(bank: bytes, chunks: dict) -> dict:
    offset, size = chunks["DIDX"]
    if size % 12:
        raise ValueError("Wwise media index size is not a multiple of 12")
    entries = {}
    for position in range(offset, offset + size, 12):
        media_id, media_offset, media_size = struct.unpack_from("<3I", bank, position)
        entries[media_id] = (media_offset, media_size)
    return entries


# ------------------------------------------------------------------- bank
def field(node, name):
    for child in node.get_children() or []:
        attrs = child.get_attrs()
        if attrs.get("name") == name:
            return attrs.get("value")
    return None


def child(node, name):
    for entry in node.get_children() or []:
        if entry.get_attrs().get("name") == name:
            return entry
    raise ValueError("Wwise node is missing " + name)


def load_hirc(bank_path: Path) -> dict:
    from wwiser import wfnv, wversion
    from wwiser.parser import wparser

    parser = wparser.Parser()
    parser.parse_banks([str(bank_path)])
    banks = parser.get_banks()
    if len(banks) != 1:
        raise ValueError("Expected exactly one Wwise bank, parsed " + str(len(banks)))
    hirc = next(child for child in banks[0].get_children() if child.get_name() == "HircChunk")
    items = []
    for holder in hirc.get_children() or []:
        if holder.get_name() == "listLoadedItem":
            items.extend(holder.get_children() or [])
    by_id = {}
    for item in items:
        item_id = field(item, "ulID")
        if item_id is None:
            continue
        if item_id in by_id:
            raise ValueError("Duplicate Wwise HIRC id %d" % item_id)
        by_id[item_id] = item
    return {"items": items, "by_id": by_id, "version": wversion.WWISER_VERSION,
            "fnv": wfnv.Fnv()}


def resolve_event(bank: dict, event: str) -> dict:
    event_id = bank["fnv"].get_hash(event)
    item = bank["by_id"].get(event_id)
    if item is None or item.get_name() != "CAkEvent":
        raise ValueError("Wwise bank has no event for %s (0x%08x)" % (event, event_id))
    actions = child(child(item, "EventInitialValues"), "actions")
    references = actions.get_children() or []
    if len(references) != 1:
        raise ValueError("Wwise event %s has %d actions, expected 1" % (event, len(references)))
    action_id = field(references[0], "ulActionID")
    action = bank["by_id"].get(action_id)
    if action is None or not action.get_name().startswith("CAkAction"):
        raise ValueError("Wwise event %s action %d is missing" % (event, action_id))
    action_type = field(action, "ulActionType")
    if action_type != 0x0403:
        raise ValueError("Wwise event %s action is not Play (0x%x)" % (event, action_type))
    target_id = field(child(action, "ActionInitialValues"), "idExt")
    sound = bank["by_id"].get(target_id)
    if sound is None or sound.get_name() != "CAkSound":
        raise ValueError("Wwise event %s does not target a Sound object" % event)
    source_data = child(child(sound, "SoundInitialValues"), "AkBankSourceData")
    plugin = field(source_data, "ulPluginID")
    if plugin != 0x00040001:
        raise ValueError("Wwise event %s source is not Vorbis (0x%x)" % (event, plugin))
    stream_type = field(source_data, "StreamType")
    if stream_type != 1:
        raise ValueError("Wwise event %s source is not prefetch streaming (%d)"
                         % (event, stream_type))
    media = child(source_data, "AkMediaInformation")
    return {
        "event": event,
        "event_id": event_id,
        "action_id": action_id,
        "action_class": action.get_name(),
        "action_type": action_type,
        "sound_id": target_id,
        "media_id": field(media, "sourceID"),
        "stream_type": stream_type,
        "plugin": plugin,
        "in_memory_bytes": field(media, "uInMemoryMediaSize"),
    }


# --------------------------------------------------------------- ogg audio
def ogg_crc_table() -> list:
    table = []
    for value in range(256):
        result = value << 24
        for _ in range(8):
            result = (((result << 1) ^ 0x04C11DB7) if result & 0x80000000
                      else (result << 1)) & 0xFFFFFFFF
        table.append(result)
    return table


OGG_CRC_TABLE = ogg_crc_table()


def ogg_crc(page: bytes) -> int:
    crc = 0
    for byte in page:
        crc = ((crc << 8) & 0xFFFFFFFF) ^ OGG_CRC_TABLE[((crc >> 24) & 0xFF) ^ byte]
    return crc


def normalize_ogg(raw: bytes, serial: int) -> tuple:
    """Give every Ogg page a fixed stream serial and refresh its CRC.

    libsndfile picks a random Ogg stream serial per encode, so without this the
    same PCM input produces different bytes on every run.  Audio packets are
    untouched.
    """
    out = bytearray(raw)
    position = 0
    pages = 0
    while position < len(out):
        if out[position:position + 4] != b"OggS":
            raise ValueError("Encoded Ogg stream has an invalid page header")
        segments = out[position + 26]
        size = 27 + segments + sum(out[position + 27:position + 27 + segments])
        page = bytearray(out[position:position + size])
        page[14:18] = struct.pack("<I", serial)
        page[22:26] = b"\x00\x00\x00\x00"
        page[22:26] = struct.pack("<I", ogg_crc(page))
        out[position:position + size] = page
        position += size
        pages += 1
    if not pages:
        raise ValueError("Encoded Ogg stream has no pages")
    return bytes(out), pages


def vgmstream_version() -> str:
    if not VGMSTREAM.exists():
        raise FileNotFoundError("Missing vgmstream decoder: " + str(VGMSTREAM))
    result = subprocess.run([str(VGMSTREAM), "-V"], capture_output=True, text=True)
    try:
        return json.loads(result.stdout.strip().splitlines()[0])["version"]
    except (ValueError, KeyError, IndexError) as error:
        raise ValueError("vgmstream did not report its version") from error


def decode_wem(wem: Path, wav: Path) -> dict:
    result = subprocess.run([str(VGMSTREAM), "-i", "-o", str(wav), str(wem)],
                            capture_output=True, text=True)
    if result.returncode != 0 or not wav.exists():
        raise ValueError("vgmstream failed for %s: %s"
                         % (wem.name, (result.stderr or result.stdout).strip()))
    detail = {}
    for line in ((result.stdout or "") + (result.stderr or "")).splitlines():
        key, _, value = line.partition(":")
        key = key.strip().lower()
        value = value.strip()
        if key == "sample rate":
            detail["sample_rate"] = int(value.split()[0])
        elif key == "channels":
            detail["channels"] = int(value.split()[0])
        elif key == "stream total samples":
            detail["samples"] = int(value.split()[0])
    return detail


def encode_ogg(wav: Path, target: Path, serial: int) -> dict:
    try:
        import soundfile
    except ImportError as error:  # pragma: no cover - environment guard
        raise RuntimeError(
            "soundfile is unavailable for Python %s; run this script with the repository "
            "Python 3.12 toolchain that matches .tools/character-art-python"
            % sys.version.split()[0]) from error
    samples, rate = soundfile.read(str(wav), dtype="int16", always_2d=True)
    if not len(samples) or rate <= 0:
        raise ValueError("Decoded WAV is empty: " + wav.name)
    soundfile.write(str(target), samples, rate, format="OGG", subtype="VORBIS")
    normalized, pages = normalize_ogg(target.read_bytes(), serial)
    target.write_bytes(normalized)
    info = soundfile.info(str(target))
    if info.format != "OGG" or info.subtype != "VORBIS":
        raise ValueError("Encoded payload is not OGG Vorbis: " + target.name)
    frames = int(info.frames)
    if frames <= 0:
        raise ValueError("Encoded OGG has no decoded frames: " + target.name)
    decoded, decoded_rate = soundfile.read(str(target), dtype="int16", always_2d=True)
    if len(decoded) != frames or decoded_rate != info.samplerate:
        raise ValueError("Encoded OGG does not decode back to its declared length")
    return {"bytes": len(normalized), "sha256": sha256(normalized), "pages": pages,
            "sample_rate": int(info.samplerate), "channels": int(info.channels),
            "frames": frames, "duration": round(frames / float(info.samplerate), 6)}


# --------------------------------------------------------------- packaging
def apk_provenance() -> dict:
    from fetch_official_assets import APK, SHA256, SIZE, URL

    if not APK.exists():
        raise FileNotFoundError("Missing pinned APK: " + str(APK))
    if APK.stat().st_size != SIZE or sha256_file(APK) != SHA256:
        raise ValueError("Pinned APK checksum mismatch; refusing to extract voice audio")
    return {"url": URL, "size": SIZE, "sha256": SHA256}


def ensure_package() -> dict:
    from fetch_official_assets import extract

    if not PCK_PATH.exists():
        extract("zh_cn_voice_prologue_pck", PACKAGE)
    if not PCK_PATH.exists():
        raise FileNotFoundError("Wwise package is unavailable: " + PCK_BUNDLE)
    raw = PCK_PATH.read_bytes()
    if len(raw) != PCK_SIZE or sha256(raw) != PCK_SHA256:
        raise ValueError("Wwise prologue package does not match the pinned hash")
    index = json.loads(BUNDLE_INDEX.read_text(encoding="utf-8")) if BUNDLE_INDEX.exists() else {}
    sources = json.loads(BUNDLE_SOURCES.read_text(encoding="utf-8")) if BUNDLE_SOURCES.exists() else {}
    record = dict(sources.get(PCK_BUNDLE) or {})
    if not record:
        if PCK_BUNDLE not in index:
            raise ValueError("Wwise package is missing from the pinned bundle index")
        record = {"origin": "apk_zip", "apk_sha256": None, "bundle_hash": index[PCK_BUNDLE],
                  "path": "assets/yoo/WwiseBnk/" + index[PCK_BUNDLE] + ".rawfile"}
    return {"raw": raw, "source": record}


def story_events() -> list:
    payload = json.loads(STORY.read_text(encoding="utf-8"))
    events = []
    for line in payload.get("dialogues") or []:
        if line.get("dialog_type") != "Voice":
            continue
        voice = (line.get("voice") or "").strip()
        if voice and voice not in events:
            events.append(voice)
    if not events:
        raise ValueError("Opening story has no Voice dialogue events")
    return events


def require_audio_toolchain() -> None:
    """Fail fast when the interpreter cannot load the bundled libsndfile wrapper."""
    try:
        import numpy
        numpy.zeros(1, dtype="int16")
        import soundfile  # noqa: F401
    except ImportError:
        raise SystemExit(
            "The repository audio toolchain in .tools/character-art-python is built for "
            "Python 3.12; run this script with that interpreter, for example "
            "D:/miniconda3/envs/comm/python.exe scripts/prepare_opening_voice.py "
            "(current interpreter: %s)" % sys.version.split()[0]) from None


def main() -> None:
    require_audio_toolchain()
    story = STORY.read_bytes()
    events = story_events()
    apk = apk_provenance()
    package = ensure_package()
    raw = package["raw"]

    pck = parse_pck(raw)
    bank_entries = [entry for entry in pck["banks"]
                    if struct.unpack_from("<I", raw, entry["offset"] + 12)[0] == BANK_ID]
    if len(bank_entries) != 1:
        raise ValueError("Prologue package does not contain exactly one %s bank" % BANK_NAME)
    bank_entry = bank_entries[0]
    bank_raw = raw[bank_entry["offset"]:bank_entry["offset"] + bank_entry["bytes"]]
    if sha256(bank_raw) != BANK_SHA256:
        raise ValueError("Prologue sound bank does not match the pinned hash")
    chunks = bank_chunks(bank_raw)
    header_offset, header_size = chunks["BKHD"]
    if header_size < 12:
        raise ValueError("Prologue sound bank header is truncated")
    version, bank_id, language_id = struct.unpack_from("<3I", bank_raw, header_offset)
    if version != BANK_VERSION or bank_id != BANK_ID:
        raise ValueError("Prologue sound bank header differs from the pinned bank")
    media = media_index(bank_raw, chunks)
    data_offset = chunks["DATA"][0]
    files = {entry["id"]: entry for entry in pck["files"]}
    if len(media) != len(files):
        raise ValueError("Prologue package media index and file table disagree")

    WORK.mkdir(parents=True, exist_ok=True)
    bank_path = WORK / (BANK_NAME + ".bnk")
    bank_path.write_bytes(bank_raw)
    bank = load_hirc(bank_path)

    VOICE_DIR.mkdir(parents=True, exist_ok=True)
    write_folder_meta(VOICE_DIR)

    resolved = []
    outputs = []
    for event in events:
        chain = resolve_event(bank, event)
        media_id = chain["media_id"]
        if media_id not in media or media_id not in files:
            raise ValueError("Wwise event %s media 0x%08x is missing from the package"
                             % (event, media_id))
        prefetch_offset, prefetch_size = media[media_id]
        file_entry = files[media_id]
        wem = raw[file_entry["offset"]:file_entry["offset"] + file_entry["bytes"]]
        prefetch = bank_raw[data_offset + prefetch_offset:
                            data_offset + prefetch_offset + prefetch_size]
        if prefetch_size != chain["in_memory_bytes"]:
            raise ValueError("Wwise event %s prefetch size differs from the media index" % event)
        if prefetch_size >= len(wem):
            raise ValueError("Wwise event %s has no streamed tail" % event)
        if wem[:prefetch_size] != prefetch:
            raise ValueError("Wwise event %s prefetch is not the streamed file prefix" % event)
        if wem[:4] != b"RIFF":
            raise ValueError("Wwise event %s media is not a RIFF WEM" % event)

        wem_path = WORK / ("%s_%08x.wem" % (event, media_id))
        wem_path.write_bytes(wem)
        wav_path = WORK / (event + ".wav")
        decoded = decode_wem(wem_path, wav_path)
        encoded = encode_ogg(wav_path, WORK / (event + ".ogg"), chain["event_id"])
        payload = (WORK / (event + ".ogg")).read_bytes()
        target = VOICE_DIR / (event.lower() + ".ogg")
        output = write_with_meta(target, payload)
        written = target.read_bytes()
        if sha256(written) != encoded["sha256"] or len(written) != encoded["bytes"]:
            raise ValueError("Written voice payload changed after encoding: " + event)
        output.update(duration=encoded["duration"], frames=encoded["frames"],
                      sample_rate=encoded["sample_rate"], channels=encoded["channels"])
        outputs.append(output)
        resolved.append({
            "event": event,
            "event_id": "0x%08x" % chain["event_id"],
            "actions": [{"class": chain["action_class"], "id": chain["action_id"],
                         "type": "0x%04x" % chain["action_type"]}],
            "sound_id": chain["sound_id"],
            "media_id": "0x%08x" % media_id,
            "stream_type": "prefetch" if chain["stream_type"] == 1 else str(chain["stream_type"]),
            "plugin": "vorbis" if chain["plugin"] == 0x00040001 else "0x%08x" % chain["plugin"],
            "prefetch": {"bytes": prefetch_size, "matches_wem_prefix": True},
            "wem": {"file": wem_path.name, "bytes": len(wem), "sha256": sha256(wem)},
            "ogg": {"path": output["path"], "sha256": output["sha256"],
                    "bytes": output["bytes"], "duration": encoded["duration"],
                    "sample_rate": encoded["sample_rate"], "channels": encoded["channels"]},
            "decoded": decoded,
        })

    previous = {}
    if REFERENCE.exists():
        try:
            existing = json.loads(REFERENCE.read_text(encoding="utf-8"))
            previous = {item["path"]: item["sha256"] for item in existing.get("outputs") or []}
        except (ValueError, KeyError, TypeError):
            previous = {}
    verification = {
        "package_hash": sha256(raw) == PCK_SHA256,
        "bank_hash": sha256(bank_raw) == BANK_SHA256,
        "bank_id": "0x%08x" % bank_id,
        "events_resolved": len(resolved),
        "prefetch_prefixes_match": all(item["prefetch"]["matches_wem_prefix"] for item in resolved),
        "outputs_rehashed": all(
            sha256((ROOT / item["path"]).read_bytes()) == item["sha256"] for item in outputs),
        "outputs_decode": all(item["duration"] > 0 for item in outputs),
        "matches_previous_manifest": (
            all(previous.get(item["path"]) == item["sha256"] for item in outputs)
            if previous else None),
    }

    try:
        import soundfile
        sf_version = soundfile.__version__
        sf_lib = soundfile.__libsndfile_version__
    except ImportError:  # pragma: no cover - encode_ogg already failed earlier
        sf_version = sf_lib = "unavailable"
    from fetch_official_assets import URL

    source = package["source"]
    manifest = {
        "manifest": VERSION,
        "operation": (
            "Resolve every Voice dialogue event of the opening story through the pinned Wwise "
            "sound bank (event -> play action -> sound -> streamed Vorbis media id), decode the "
            "streamed WEM with vgmstream and re-encode it as OGG Vorbis with a normalized Ogg "
            "stream serial so the payload is reproducible."),
        "inputs": {
            "story": {
                "path": STORY.relative_to(ROOT).as_posix(),
                "bytes": len(story),
                "sha256": sha256(story),
                "voice_events": events,
                "voice_event_count": len(events),
            },
            "apk": apk,
            "package": {
                "package": PACKAGE,
                "bundle": PCK_BUNDLE,
                "bundle_hash": source.get("bundle_hash"),
                "origin": source.get("origin"),
                "apk_entry": source.get("path"),
                "apk_sha256": source.get("apk_sha256", apk["sha256"]),
                "url": URL,
                "bytes": len(raw),
                "sha256": sha256(raw),
            },
            "bank": {
                "name": BANK_NAME,
                "bank_id": "0x%08x" % bank_id,
                "bank_version": version,
                "language_id": language_id,
                "bytes": len(bank_raw),
                "sha256": sha256(bank_raw),
                "media_entries": len(media),
                "streamed_files": len(files),
                "chunks": {tag: {"offset": value[0], "bytes": value[1]}
                           for tag, value in sorted(chunks.items())},
            },
        },
        "events": resolved,
        "tools": {
            "wwiser": bank["version"],
            "wwiser_command": "wwiser.parser.wparser.Parser().parse_banks([<work>/voice_prologue.bnk])",
            "vgmstream": vgmstream_version(),
            "vgmstream_command": "vgmstream-cli.exe -i -o <work>/<event>.wav <work>/<event>_<media>.wem",
            "soundfile": sf_version,
            "libsndfile": sf_lib,
            "soundfile_command": ("soundfile.write(<work>/<event>.ogg, int16 samples, sample_rate, "
                                  "format='OGG', subtype='VORBIS')"),
            "python": sys.version.split()[0],
            "ogg_normalization": ("Ogg page stream serial = event id, page CRC-32 "
                                  "(poly 0x04C11DB7) recomputed"),
        },
        "outputs": sorted(outputs, key=lambda item: item["path"]),
        "verification": verification,
    }
    REFERENCE.parent.mkdir(parents=True, exist_ok=True)
    REFERENCE.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n",
                         encoding="utf-8")

    print(json.dumps({
        "manifest": manifest["manifest"],
        "bank": {"name": BANK_NAME, "id": manifest["inputs"]["bank"]["bank_id"],
                 "sha256": manifest["inputs"]["bank"]["sha256"]},
        "events": [{"event": item["event"], "event_id": item["event_id"],
                    "media_id": item["media_id"], "wem_sha256": item["wem"]["sha256"],
                    "wem_bytes": item["wem"]["bytes"],
                    "ogg": item["ogg"]["path"], "ogg_bytes": item["ogg"]["bytes"],
                    "ogg_sha256": item["ogg"]["sha256"],
                    "duration": item["ogg"]["duration"]} for item in resolved],
        "verification": verification,
        "reference": REFERENCE.relative_to(ROOT).as_posix(),
    }, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    try:
        main()
    finally:
        shutil.rmtree(WORK, ignore_errors=True)
