"""Recover the exact original client IL required by the retained character prefab."""
import concurrent.futures
import hashlib
import json
import subprocess
import urllib.request
from pathlib import Path

from Crypto.Cipher import AES
from Crypto.Util.Padding import unpad
from official_apk import Apk, ROOT, UnityPy
from official_manifest import read_manifest
from prepare_mainline_character_components import record
from prepare_unity_map13_sources import encode, write

PROFILE_COMMIT = "d8571957cb5d6e20c3760a5969274070c3ae90c4"
VERSION = "871_156598"
RECEIPT = ROOT / "assets/reference/official-map/mainline-character-client-sources-source-manifest.json"


def checked_download(path, url, size, md5, sha256=None):
    raw = path.read_bytes() if path.exists() else urllib.request.urlopen(url, timeout=30).read()
    if (len(raw) != size or hashlib.md5(raw).hexdigest() != md5
            or (sha256 is not None and hashlib.sha256(raw).hexdigest() != sha256)):
        raise ValueError("Historical official input differs: " + url)
    if not path.exists():
        write(path, raw)
    return raw


def main():
    configuration = json.loads(subprocess.check_output(
        ["git", "show", PROFILE_COMMIT + ":official-source.json"], cwd=ROOT))
    profile = configuration["profiles"][VERSION]
    selected = next(row for row in profile["manifests"] if row["package"] == "HybridCLR")
    cache = ROOT / profile["cloud_cache"]
    manifest_path = cache / Path(selected["url"]).name
    raw_manifest = checked_download(manifest_path, selected["url"], selected["size"], selected["md5"], selected["sha256"])
    manifest = read_manifest(raw_manifest)
    if manifest["version"] != VERSION or manifest["package"] != "HybridCLR":
        raise ValueError("Historical original client manifest identity differs")
    pin_path = ROOT / "assets/reference/official-map/map14-entity-lifecycle-source-manifest.json"
    expected = {Path(row["path"]).name: row["sha256"] for row in json.loads(pin_path.read_bytes())["assemblies"]}
    output = ROOT / ".tools/source-packages/recovered" / VERSION / "official-code"
    managed = output / "managed/decoded"
    key = bytes(value - 7 for value in [40, 49, 83, 108, 112, 123, 112, 117, 110, 44, 84, 63, 63, 42, 133, 40])
    recovered = []
    for name in ("Assembly-CSharp.dll", "Console.dll"):
        bundle_name = "hybridclr_assets_hybridclrdata_dlls_" + name.lower().replace(".", "_") + ".bundle"
        bundle = next(row for row in manifest["bundles"] if row["name"] == bundle_name)
        url = selected["url"].rsplit("/", 1)[0] + "/" + bundle["file"]
        raw = checked_download(cache / bundle["file"], url, bundle["size"], bundle["hash"])
        environment = UnityPy.Environment()
        environment.path = str(cache)

        def resolve(cab_name, *args, **kwargs):
            cab = environment.get_cab(cab_name.rsplit("/", 1)[-1].lower())
            if cab is not None:
                return cab
            raise FileNotFoundError("Unresolved historical client CAB: " + cab_name)

        environment.find_file = resolve
        environment.load_file(Apk.decode(raw), name=bundle_name)
        assets = [obj for obj in environment.objects if obj.type.name == "TextAsset" and obj.read().m_Name == name]
        if len(assets) != 1:
            raise ValueError("Historical assembly TextAsset is not unique: " + name)
        text = assets[0].read().m_Script.encode("utf8", "surrogateescape")
        decoded = text if text.startswith(b"MZ") else unpad(AES.new(key, AES.MODE_CBC, key[::-1]).decrypt(text), 16)
        if not decoded.startswith(b"MZ") or hashlib.sha256(decoded).hexdigest() != expected[name]:
            raise ValueError("Decoded historical IL differs from the installed original source pin: " + name)
        target = managed / name
        if target.exists() and target.read_bytes() != decoded:
            raise ValueError("Preserve a different historical assembly: " + str(target))
        write(target, decoded)
        recovered.append(dict(name=name, url=url, bundle_md5=bundle["hash"], bundle_sha256=hashlib.sha256(raw).hexdigest(),
                              path_id=str(assets[0].path_id), text_asset_sha256=hashlib.sha256(text).hexdigest(), output=record(target)))
    identity_path = ROOT / "assets/reference/official-map/mainline-character-components-source-manifest.json"
    identities = json.loads(identity_path.read_bytes())["script_identities"]
    names = sorted({row["type"] for row in identities if row["hierarchy"] == "3001"}) + ["MyMapCharacterBase`2", "EMapEntityTagType"]
    targets = [(name, "Assembly-CSharp.dll", "client-source") for name in names]
    targets.extend(("Common." + name, "Console.dll", "common-source/Common")
                   for name in ("CharacterComponent", "GridMoverComponent", "GridMoverComponentInfo",
                                "ActionAgentComponent", "ActionAgentComponentImplement", "ActionComponent",
                                "ActionRuntimeData", "ActionType", "ActionAgentStartEventDelegate",
                                "ActionAgentEndEventDelegate", "EcEntityEndEventDelegate",
                                "EventDelegateBasicBase`2", "EventDelegateBase`4"))
    dotnet = ROOT / ".tools/dotnet/dotnet.exe"
    ilspy = ROOT / ".tools/ilspycmd/tools/net10.0/any/ilspycmd.dll"

    cached = {}
    if RECEIPT.exists():
        previous = json.loads(RECEIPT.read_bytes())
        prior_tools = {row["path"]: row["sha256"] for row in previous["tools"]}
        prior_assemblies = {row["name"]: row["output"]["sha256"] for row in previous["assemblies"]}
        if (previous["manifest"] == VERSION and prior_tools.get(record(ilspy)["path"]) == record(ilspy)["sha256"]
                and prior_assemblies == {row["name"]: row["output"]["sha256"] for row in recovered}):
            cached = {row["path"]: row["sha256"] for row in previous["outputs"]}

    def decompile(target_type):
        name, assembly, folder = target_type
        target = output / folder / (name.rsplit(".", 1)[-1].split("`")[0] + ".cs")
        if target.exists() and cached.get(record(target)["path"]) == record(target)["sha256"]:
            print("Verified cached original character type:", name, flush=True)
            return record(target)
        result = subprocess.run([str(dotnet), str(ilspy), "-t", name, str(managed / assembly)],
                                capture_output=True, check=True, timeout=120)
        source = result.stdout.decode("utf-8-sig")
        if (name.rsplit(".", 1)[-1].split("`")[0] not in source
                or not any(token in source for token in ("class ", "enum ", "interface ", "struct "))):
            raise ValueError("Historical source decompilation returned no type: " + name)
        # ILSpy's update notice is stderr; never retain notices as C# source.
        if "You are not using the latest version" in source:
            raise ValueError("Decompiler notice unexpectedly entered source output")
        write(target, source.encode())
        print("Recovered original character type:", name, flush=True)
        return record(target)

    sources = []
    with concurrent.futures.ThreadPoolExecutor(max_workers=2) as executor:
        futures = [executor.submit(decompile, target) for target in targets]
        for future in concurrent.futures.as_completed(futures):
            sources.append(future.result())
    sources.sort(key=lambda row: row["path"])
    write(RECEIPT, encode(dict(
        format=1, manifest=VERSION, profile_git_commit=PROFILE_COMMIT, profile_path="official-source.json",
        manifest_input=dict(**selected, cached=record(manifest_path)), assemblies=recovered,
        inputs=[record(pin_path), record(identity_path)], tools=[record(Path(__file__).resolve()), record(dotnet), record(ilspy)],
        outputs=sources,
        operation="Read the historical source profile from its committed Git identity without changing the active profile. Verify the original manifest size/MD5/SHA256 and each DLL bundle size/MD5; reuse the official assembly AES-CBC decode. Require decoded Assembly-CSharp and Console hashes to equal the existing original lifecycle pins. Decompile all original 3001 root script types, their generic character base, tag flags and consumed character, movement and action shared types into a separate historical source directory. Reuse source cache only when its recorded hash, originating IL and decompiler hash match; do not substitute current code for removed historical serialized fields.")))
    print("Recovered historical original character source types:", len(sources), flush=True)


if __name__ == "__main__":
    main()
