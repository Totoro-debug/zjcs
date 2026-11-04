"""Compare the requested original quality Sprite across verified official versions."""
import hashlib
import json
from pathlib import Path
import urllib.request
import sys
import io
import zipfile
import re
sys.path.insert(0, str(Path(__file__).resolve().parents[1] / ".tools/python-packages"))
import UnityPy
from official_apk import Apk, ROOT
from official_manifest import read_manifest, Reader
from fetch_official_runtime_assets import package_manifest
from validation_workspace import validation_directory


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def inspect(raw, source):
    env = UnityPy.load(raw)
    sprites = sorted((dict(name=obj.read(check_read=False).m_Name,
        object_sha256=sha(obj.get_raw_data()), path_id=str(obj.path_id))
        for obj in env.objects if obj.type.name == "Sprite"), key=lambda value: value["name"])
    return dict(source=source, sprites=sprites,
        requested="ImgQuality_Rainbow", found=any(row["name"] == "ImgQuality_Rainbow" for row in sprites))


class OfficialApkRanges(io.RawIOBase):
    """Read selected ZIP entries with CRC checks without downloading a second APK."""
    def __init__(self, url):
        self.url, self.pos = url, 0
        with urllib.request.urlopen(urllib.request.Request(url, headers={
                "User-Agent": "Mozilla/5.0", "Range": "bytes=0-0"}), timeout=30) as response:
            if response.status != 206:
                raise ValueError("Official historical APK does not support ranges")
            self.length = int(response.headers["Content-Range"].split("/")[-1])
            self.etag = response.headers["Etag"]

    def readable(self): return True
    def seekable(self): return True
    def tell(self): return self.pos
    def seek(self, offset, whence=0):
        self.pos = offset if whence == 0 else self.pos + offset if whence == 1 else self.length + offset
        return self.pos

    def read(self, count=-1):
        count = min(self.length - self.pos, count if count >= 0 else self.length - self.pos)
        if not count: return b""
        span = f"bytes {self.pos}-{self.pos + count - 1}/{self.length}"
        request = urllib.request.Request(self.url, headers={"User-Agent": "Mozilla/5.0",
            "Range": span.split("/")[0].replace("bytes ", "bytes=")})
        with urllib.request.urlopen(request, timeout=40) as response:
            if response.status != 206 or response.headers["Content-Range"] != span or response.headers["Etag"] != self.etag:
                raise ValueError("Historical APK range identity differs")
            raw = response.read(count + 1)
        if len(raw) != count: raise ValueError("Incomplete historical APK range")
        self.pos += count
        return raw


def legacy_quality_record(raw):
    header = Reader(raw)
    if header.read("I") != 0x00594F4F or header.string() != "2.0.0":
        raise ValueError("Review historical manifest format")
    # V2 length-prefixed BundleTable fields, verified against ManifestTools.
    name = b"defaultpackage_assets_image_ui_itemquality.bundle"
    needle = len(name).to_bytes(2, "little") + name
    if raw.count(needle) != 1: raise ValueError("Ambiguous historical quality bundle record")
    record = Reader(raw); record.pos = raw.index(needle)
    bundle_name = record.string(); unity_crc = record.read("I")
    bundle_hash = record.string(); file_crc = record.string(); size = record.read("q"); encrypted = record.read("?")
    if not re.fullmatch("[a-f0-9]{32}", bundle_hash) or encrypted:
        raise ValueError("Review historical quality bundle identity/encryption")
    return dict(bundle_name=bundle_name, bundle_md5=bundle_hash, unity_crc=unity_crc,
                file_crc=file_crc, size=size)


def inspect_legacy_cdn():
    root = "https://zhangjcsdl.leiting.com/patch/202509301228340/Android/DefaultPackage/"
    def read(name):
        return urllib.request.urlopen(urllib.request.Request(root + name,
            headers={"User-Agent": "Mozilla/5.0"}), timeout=30).read()
    version = read("PackageManifest_DefaultPackage.version").decode("ascii").strip()
    if version != "340_153141": raise ValueError("Review changed historical CDN version")
    entry = "PackageManifest_DefaultPackage_" + version
    expected = read(entry + ".hash").decode("ascii").strip()
    raw = read(entry + ".bytes")
    if hashlib.md5(raw).hexdigest() != expected: raise ValueError("Historical CDN manifest MD5 differs")
    record = legacy_quality_record(raw)
    filename = record["bundle_md5"] + ".bundle"
    payload = read(filename)
    if len(payload) != record["size"] or hashlib.md5(payload).hexdigest() != record["bundle_md5"]:
        raise ValueError("Historical CDN quality bundle differs")
    return inspect(payload, dict(version=version, origin="official_cdn", url=root + filename,
        manifest=dict(url=root + entry + ".bytes", md5=expected, sha256=sha(raw)),
        bundle_sha256=sha(payload), **record))


def inspect_older_apk():
    # This URL is the full-update target of the official 152594 config branch.
    config_url = "https://zhangjcsupdate.leiting.com/zhangjcs/version/release/110001_Main.dis"
    config = urllib.request.urlopen(config_url, timeout=30).read()
    url = json.loads(config)["152594"]["AndroidFullUpdateAppUrl"]
    if url != "https://zhangjcsdlf.leiting.com/full/zjcs_M523655.apk":
        raise ValueError("Review changed official historical full-update target")
    remote = OfficialApkRanges(url)
    with zipfile.ZipFile(remote) as archive:
        entry = "assets/yoo/DefaultPackage/PackageManifest_DefaultPackage_332_153082.bytes"
        raw = archive.read(entry)
        expected = archive.read(entry.removesuffix(".bytes") + ".hash").decode("ascii").strip()
        if hashlib.md5(raw).hexdigest() != expected:
            raise ValueError("Historical APK manifest MD5 differs")
        record = legacy_quality_record(raw)
        bundle_hash, size = record["bundle_md5"], record["size"]
        candidates = [value for value in archive.namelist()
            if value.startswith("assets/yoo/DefaultPackage/") and bundle_hash in value and value.endswith(".bundle")]
        if len(candidates) != 1: raise ValueError("Historical quality payload is not uniquely packaged")
        payload = archive.read(candidates[0])
        if len(payload) != size or hashlib.md5(payload).hexdigest() != bundle_hash:
            raise ValueError("Historical quality payload differs from manifest")
        return inspect(payload, dict(version="332_153082", origin="official_apk_zip_ranges", url=url,
            configuration=dict(url=config_url, sha256=sha(config)), apk_size=remote.length, apk_etag=remote.etag,
            full_apk_sha256_verified=False, entry=candidates[0], zip_crc32=archive.getinfo(candidates[0]).CRC,
            manifest=dict(entry=entry, md5=expected, sha256=sha(raw)), bundle_sha256=sha(payload), **record,
            v2_record_layout="https://raw.githubusercontent.com/tuyoogame/YooAsset/2.3.12/Assets/YooAsset/Runtime/ResourcePackage/ManifestTools.cs"))


def build():
    work = validation_directory()
    if not (work / ".zjcs-validation.json").is_file():
        raise ValueError("Use an owned validation workspace")
    apk = Apk.current()
    results = []
    try:
        package = "DefaultPackage"
        name = "defaultpackage_assets_image_ui_itemquality.bundle"
        filename = "PackageManifest_DefaultPackage_" + apk.source.apk_version + ".v3.bytes"
        manifest_raw = apk.archive.read("assets/yoo/DefaultPackage/" + filename)
        manifest = read_manifest(manifest_raw)
        bundle, = [row for row in manifest["bundles"] if row["name"] == name]
        path = "assets/yoo/DefaultPackage/" + bundle["file"]
        raw = apk.archive.read(path)
        if len(raw) != bundle["size"] or hashlib.md5(raw).hexdigest() != bundle["hash"]:
            raise ValueError("Packaged original quality bundle differs")
        results.append(inspect(raw, dict(version=manifest["version"], origin="apk_zip", path=path,
            apk_sha256=apk.source.apk_sha256, manifest_sha256=sha(manifest_raw), bundle_sha256=sha(raw),
            bundle_md5=bundle["hash"])))
        roots = [("202610211330871", "871_156598"), ("202610262246877", "877_156664"),
                 ("202610291150884", "884_156692")]
        for resource, expected in roots:
            root = "https://zhangjcsdl.leiting.com/patch/" + resource + "/Android"
            destination = work / ("quality-version-" + expected)
            parsed, proof = package_manifest(package, [root], destination)
            if parsed["version"] != expected:
                raise ValueError("Official quality inspection version changed")
            bundle, = [row for row in parsed["bundles"] if row["name"] == name]
            url = root + "/DefaultPackage/" + bundle["file"]
            raw = urllib.request.urlopen(url, timeout=30).read(bundle["size"] + 1)
            if len(raw) != bundle["size"] or hashlib.md5(raw).hexdigest() != bundle["hash"]:
                raise ValueError("Historical quality bundle failed its own manifest hash")
            results.append(inspect(raw, dict(version=expected, resource_version=resource,
                origin="official_cdn", url=url, manifest=proof, bundle_md5=bundle["hash"], bundle_sha256=sha(raw))))
    finally:
        apk.archive.close()
    results.append(inspect_older_apk())
    results.append(inspect_legacy_cdn())
    target = ROOT / "assets/reference/battle-hud/pet-quality-version-source-manifest.json"
    target.write_bytes((json.dumps(dict(generator_sha256=sha(Path(__file__).read_bytes()), versions=results,
        operation="Inspect actual original Sprite objects in each independently verified version. No replacement art, version repinning or runtime asset changes."),
        ensure_ascii=False, indent=2) + "\n").encode("utf-8"))
    for row in results:
        print(row["source"]["version"], "original quality sprites", len(row["sprites"]), "Rainbow", row["found"], flush=True)


if __name__ == "__main__":
    build()
