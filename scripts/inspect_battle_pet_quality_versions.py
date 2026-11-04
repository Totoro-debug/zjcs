"""Compare the requested original quality Sprite across verified official versions."""
import hashlib
import json
from pathlib import Path
import urllib.request
import sys
sys.path.insert(0, str(Path(__file__).resolve().parents[1] / ".tools/python-packages"))
import UnityPy
from official_apk import Apk, ROOT
from official_manifest import read_manifest
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
        roots = [("202610211330871", "871_156598"), ("202610291150884", "884_156692")]
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
    target = ROOT / "assets/reference/battle-hud/pet-quality-version-source-manifest.json"
    target.write_bytes((json.dumps(dict(generator_sha256=sha(Path(__file__).read_bytes()), versions=results,
        operation="Inspect actual original Sprite objects in each independently verified version. No replacement art, version repinning or runtime asset changes."),
        ensure_ascii=False, indent=2) + "\n").encode("utf-8"))
    for row in results:
        print(row["source"]["version"], "original quality sprites", len(row["sprites"]), "Rainbow", row["found"], flush=True)


if __name__ == "__main__":
    build()
