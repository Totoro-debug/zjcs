"""Recover GotGrayObj attachment and all dialogue reward sprites from 884_156692."""
import hashlib
import json
from pathlib import Path
import re
import shutil

import yaml
from UnityPy.classes.PPtr import PPtr
from UnityPy.enums import ClassIDType

from official_apk import Apk, ROOT
from official_manifest import read_asset_manifest
from prepare_current_partner_native import BUNDLE, PartnerNativeRecovery, digest, documents, identity
from prepare_partner_card_native import Dumper, guid, script_id, sha
from prepare_partner_input_dialog import metadata
from prepare_partner_panel_native import PartnerPanelBinding
from validation_workspace import validation_directory

RECEIPT_NAME = "partner-dialogue-reward-assets-source-manifest.json"
MANIFEST_VERSION = "884_156692"
GOT_GRAY_GUID = "53b02144bdb12964e8a5909afdbc26c4"


class PinnedPartnerNativeRecovery(PartnerNativeRecovery):
    def __init__(self, version: str = MANIFEST_VERSION, bundle: str = BUNDLE):
        self.apk = Apk(version)
        self.cache = ROOT / ".tools/source-packages/current" / self.apk.version / "native-conversion"
        self.export_name = "native-export-all"
        self.bundle = bundle
        self.native = self.cache / self.export_name / "ExportedProject/Assets"
        self.field_cache = self.cache / "component-fields-all"
        manifest_path = self.field_cache / "source-manifest.json"
        manifest = json.loads(manifest_path.read_bytes())
        if manifest["manifest"] != self.apk.version or not manifest["completed"]:
            raise ValueError("Pinned field recovery cache is not complete: " + self.apk.version)
        self.cached = {row["cab"].lower() + ":" + row["path_id"]: row for row in manifest["objects"]}
        import prepare_current_partner_native
        component_cache = dict(
            path=manifest_path.relative_to(ROOT).as_posix(),
            sha256=digest(manifest_path.read_bytes()),
        )
        self.native_verified = None
        self.schema_managed = self.cache / "changed-inputs/Managed"
        self.objects = self.apk.objects(bundle)
        self.typed = {}
        self.schemas = None
        self.outputs = []
        self.proof = dict(
            manifest=self.apk.version,
            bundle=bundle,
            stage="Recovered original hierarchy/fields only; runtime script and resource binding pending",
            generator_sha256=digest(Path(prepare_current_partner_native.__file__).read_bytes()),
            component_cache=component_cache,
            prefabs=[],
        )


class PartnerDialogueRewardBinding(PartnerPanelBinding):
    def __init__(self, work: Path):
        import prepare_partner_card_native
        original_cls = prepare_partner_card_native.PartnerNativeRecovery
        prepare_partner_card_native.PartnerNativeRecovery = PinnedPartnerNativeRecovery
        try:
            super().__init__(work)
        finally:
            prepare_partner_card_native.PartnerNativeRecovery = original_cls

    def register_scripts(self):
        super().register_scripts()
        runtime = ROOT / "unity/Assets/Game/Runtime/Official"
        cell_path = runtime / "ProfessionUI/ItemIconAttachmentCell.cs"
        cell_meta = metadata(cell_path)
        pointer = dict(fileID=11500000, guid=guid(cell_meta), type=3)
        self.scripts["ItemIconAttachmentCell"] = pointer
        self.script_sources["ItemIconAttachmentCell"] = dict(
            path=cell_path.relative_to(ROOT).as_posix(),
            pointer=pointer,
            sha256=sha(cell_path.read_bytes()),
            meta_sha256=sha(cell_meta.read_bytes()),
        )
        self.class_ids[script_id("", "ItemIconAttachmentCell")] = pointer
        self.script_guids.add(pointer["guid"])

    def restore_prefab(self, prefab):
        for key, pointer in prefab["external"].items():
            self.objects[pointer["guid"]] = self.original(key)
        for key, file_id in prefab["objectMap"].items():
            owner = self.original(key)
            self.learn(self.recovery.fields(owner), next(iter(prefab["documents"][file_id].values())), owner)
        for script in prefab["scripts"]:
            fields = prefab["documents"][script["nativeFileID"]]["MonoBehaviour"]
            full_name = ".".join(filter(None, [script["namespace"], script["type"]]))
            fields["m_Script"] = self.scripts[full_name]
        prefab["documents"] = self.replace(prefab["documents"])

    def find_sprite_asset(self, obj, record):
        key, file_id = self.recovery.fields(obj)["m_RenderDataKey"]
        render_guid = "".join(format(key["data[" + str(i) + "]"], "08x")[::-1] for i in range(4))
        if render_guid != record["guid"]:
            raise ValueError("Dialogue reward render key differs: " + record["address"])
        name = self.name_key(obj.read(check_read=False).m_Name)
        candidates = []
        for candidate_key in [name] + [name + str(i) for i in range(100)]:
            for candidate in self.by_name.get(candidate_key, []):
                if candidate.suffix == ".asset":
                    docs = documents(candidate.read_text("utf8"))
                    if len(docs) == 1 and next(iter(docs.values())).get("Sprite", {}).get("m_RenderDataKey") == {render_guid: file_id}:
                        candidates.append(candidate)
        if len(candidates) != 1:
            raise ValueError("Expected unique Sprite asset for " + record["address"] + ", found " + repr(candidates))
        return candidates[0]

    def build_and_install(self):
        apk = self.recovery.apk
        records = read_asset_manifest(apk.source.manifest_bytes("DefaultPackage", apk.archive))

        # 1. Recover official GotGrayObj.prefab (ItemIconAttachmentType.GotGrayTip = 23).
        attachment_record, = [row for row in records if row["guid"] == GOT_GRAY_GUID]
        attachment_obj, = [
            ref.deref()
            for path, ref in apk.container(attachment_record["bundle"])
            if path.casefold() == attachment_record["path"].casefold() and ref.deref().type.name == "GameObject"
        ]
        attachment_name = attachment_obj.read().m_Name
        self.recovery.recover(
            attachment_name,
            self.work / "dialogue-reward-native",
            bundle=attachment_record["bundle"],
            native_relative=attachment_record["path"].removeprefix("Assets/"),
        )
        self.recovery.finalize()
        att_native, _, att_data, att_record = self.recovery.outputs[0]
        self.restore_prefab(att_data)
        self.inputs[att_record["nativeSource"]] = sha(att_native.read_bytes())
        self.inputs[att_record["nativeSource"] + ".meta"] = sha(Path(str(att_native) + ".meta").read_bytes())
        cache_proof = self.recovery.proof["component_cache"]
        self.inputs[cache_proof["path"]] = cache_proof["sha256"]
        got_gray_guid = hashlib.md5(b"zjcs:partner-dialogue:GotGrayObj.prefab").hexdigest()
        got_gray_pointer = dict(fileID=int(att_data["root"]), guid=got_gray_guid, type=3)
        self.queue.append((Path("Attachments/GotGrayObj.prefab"), got_gray_guid, att_data["documents"]))

        # 2. Bind all 86 reward sprites referenced by partner-dialogue-rules.json.
        dialogue_rules_path = ROOT / "unity/Assets/Official/Partners/partner-dialogue-rules.json"
        partner_rules_path = ROOT / "unity/Assets/Official/Partners/partner-rules.json"
        dialogue_rules = json.loads(dialogue_rules_path.read_bytes())
        partner_rules = json.loads(partner_rules_path.read_bytes())
        if partner_rules["manifest"] != apk.version:
            raise ValueError("Partner rules version differs from 884_156692")
        self.inputs[dialogue_rules_path.relative_to(ROOT).as_posix()] = sha(dialogue_rules_path.read_bytes())
        self.inputs[partner_rules_path.relative_to(ROOT).as_posix()] = sha(partner_rules_path.read_bytes())

        reward_ids = {int(key) for node in dialogue_rules["nodes"] if node.get("RewardItem") for key in node["RewardItem"]}
        items = {row["ClassId"]: row for row in partner_rules["tables"]["item"] if row["ClassId"] in reward_ids}
        if set(items) != reward_ids:
            raise ValueError("Missing item definitions for dialogue rewards: " + repr(sorted(reward_ids - set(items))))

        sprites = []
        seen_addresses = set()
        for item_id in sorted(items):
            item = items[item_id]
            address = "Assets/Image/" + item["RawIcon"]
            if address in seen_addresses:
                continue
            seen_addresses.add(address)
            record, = [row for row in records if row["address"] == address or row["path"] == address + ".png"]
            obj, = [
                ref.deref()
                for path, ref in apk.container(record["bundle"])
                if path.casefold() == record["path"].casefold() and ref.deref().type.name == "Sprite"
            ]
            candidate = self.find_sprite_asset(obj, record)
            source_guid = guid(Path(str(candidate) + ".meta"))
            self.objects[source_guid] = obj
            sprites.append(
                dict(
                    item_id=item_id,
                    address=address,
                    pointer=self.load(source_guid),
                    manifest_asset=record,
                    original=identity(obj),
                    original_sha256=sha(obj.get_raw_data()),
                )
            )

        # Resolve newly queued dependencies (e.g., textures referenced by queued Sprite assets).
        idx = 0
        while idx < len(self.queue):
            target, target_guid, data = self.queue[idx]
            idx += 1
            if isinstance(data, dict):
                resolved = self.replace(data)
                data.clear()
                data.update(resolved)

        # 3. Load canonical shared-texture aliases and reuse installed Partner assets cleanly.
        shared_path = ROOT / "assets/reference/shared-textures-source-manifest.json"
        shared_manifest = json.loads(shared_path.read_bytes()) if shared_path.is_file() else {"textures": []}
        shared_guid_aliases = {}
        shared_by_sha = {}
        for row in shared_manifest.get("textures", []):
            src_row = row.get("source", {})
            can_row = row.get("canonical", {})
            if can_row.get("guid") and (ROOT / can_row["path"]).is_file():
                shared_guid_aliases[src_row.get("guid")] = can_row["guid"]
                shared_guid_aliases[can_row["guid"]] = can_row["guid"]
                if can_row.get("sha256"):
                    shared_by_sha.setdefault(can_row["sha256"], can_row["guid"])

        proof_path = ROOT / "assets/reference" / RECEIPT_NAME
        previous_receipt = json.loads(proof_path.read_bytes()) if proof_path.is_file() else {}
        previous = previous_receipt.get("outputs", {})
        isolated_guids = previous_receipt.get("isolated_guids", {})
        if (not isinstance(isolated_guids, dict)
                or any(not re.fullmatch(r"[0-9a-f]{32}", value)
                       for pair in isolated_guids.items() for value in pair)
                or len(set(isolated_guids.values())) != len(isolated_guids)
                or set(isolated_guids) & set(isolated_guids.values())):
            raise ValueError("Invalid isolated partner reward GUID mapping")
        existing = {}
        for meta_path in (ROOT / "unity/Assets/Official/Partners").rglob("*.meta"):
            asset = Path(str(meta_path)[:-5])
            if asset.is_file():
                key = guid(meta_path)
                if key in existing:
                    raise ValueError("Duplicate partner GUID: " + key)
                existing[key] = asset
        for can_sha, can_guid in shared_by_sha.items():
            pass
        canonical_paths = {
            row["canonical"]["guid"]: ROOT / row["canonical"]["path"]
            for row in shared_manifest.get("textures", [])
            if row.get("canonical", {}).get("guid") and (ROOT / row["canonical"]["path"]).is_file()
        }

        destination = ROOT / "unity/Assets/Official/Partners/UI/DialoguePlayback/Rewards"
        installed, reused = {}, []

        def emit(data_dict):
            raw_text = "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n"
            for file_id, content in data_dict.items():
                kind, = content
                raw_text += "--- !u!" + str(int(ClassIDType[kind])) + " &" + str(file_id) + "\n"
                raw_text += yaml.dump(content, Dumper=Dumper, sort_keys=False, allow_unicode=True, width=1000000)
            return raw_text.encode("utf8")

        def write_file_checked(path: Path, raw: bytes):
            relative = path.relative_to(ROOT).as_posix()
            if path.is_file() and path.read_bytes().replace(b"\r\n", b"\n") != raw.replace(b"\r\n", b"\n"):
                if sha(path.read_bytes()) != previous.get(relative):
                    raise ValueError("Refusing unrecorded overwrite: " + relative)
            path.parent.mkdir(parents=True, exist_ok=True)
            if not path.is_file() or path.read_bytes() != raw:
                path.write_bytes(raw)
            installed[relative] = sha(path.read_bytes())

        # A previous collision repair keeps the two different native objects
        # distinct. Preserve those GUIDs whenever this source is regenerated.
        serialized_queue = []
        aliases = dict(shared_guid_aliases)
        aliases.update(isolated_guids)
        for target, target_guid, data in self.queue:
            raw = emit(data) if isinstance(data, dict) else data
            norm_sha = sha(raw.replace(b"\r\n", b"\n"))
            raw_sha = sha(raw)
            if target_guid in isolated_guids:
                target_guid = isolated_guids[target_guid]
            elif target_guid not in existing:
                canonical_guid = (
                    shared_guid_aliases.get(target_guid)
                    or shared_by_sha.get(raw_sha)
                    or shared_by_sha.get(norm_sha)
                )
                if canonical_guid and canonical_guid != target_guid:
                    aliases[target_guid] = canonical_guid
                    target_guid = canonical_guid
            serialized_queue.append((target, target_guid, raw))

        # Unity GUIDs are project-wide, including assets outside Partners.
        # Do not recreate a duplicate when another feature already owns one.
        selected = {target_guid for _, target_guid, _ in serialized_queue} - canonical_paths.keys()
        assets_root = ROOT / "unity/Assets"
        partners_root = ROOT / "unity/Assets/Official/Partners"
        for meta_path in assets_root.rglob("*.meta"):
            asset = Path(str(meta_path)[:-5])
            if not asset.is_file() or asset.is_relative_to(partners_root):
                continue
            key = guid(meta_path)
            if key in selected:
                raise ValueError("Partner reward GUID already belongs to another asset: " + str(asset))

        def remap_guids(raw: bytes) -> bytes:
            if not aliases or not raw.startswith(b"%YAML"):
                return raw
            return re.sub(
                rb"(?<=guid: )[0-9a-f]{32}",
                lambda m: aliases.get(m[0].decode(), m[0].decode()).encode(),
                raw,
            )

        for target, target_guid, raw in serialized_queue:
            raw = remap_guids(raw)
            if target_guid in existing:
                path = existing[target_guid]
                reused.append(path.relative_to(ROOT).as_posix())
                installed[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
                meta_path = Path(str(path) + ".meta")
                installed[meta_path.relative_to(ROOT).as_posix()] = sha(meta_path.read_bytes())
                continue
            if target_guid in canonical_paths:
                reused.append(canonical_paths[target_guid].relative_to(ROOT).as_posix())
                continue
            path = destination / target
            write_file_checked(path, raw)
            importer = "PrefabImporter" if path.suffix.lower() == ".prefab" else "NativeFormatImporter"
            meta_raw = (
                "fileFormatVersion: 2\nguid: "
                + target_guid
                + "\n"
                + importer
                + ":\n  externalObjects: {}\n"
            ).encode("utf8")
            write_file_checked(Path(str(path) + ".meta"), meta_raw)

        for folder in [destination] + sorted(p for p in destination.rglob("*") if p.is_dir()):
            meta_file = metadata(folder, "DefaultImporter", True)
            installed[meta_file.relative_to(ROOT).as_posix()] = sha(meta_file.read_bytes())

        image_script = ROOT / "unity/Assets/Game/Runtime/Offline/ProfessionUI/OfflineProfessionImageCatalog.cs"
        self.inputs[image_script.relative_to(ROOT).as_posix()] = sha(image_script.read_bytes())
        catalog_path = ROOT / "unity/Assets/Game/Resources/Partners/OfficialPartnerDialogueRewardImages.asset"
        entries = [dict(Path=attachment_record["path"], AssetGuid=GOT_GRAY_GUID, Asset=got_gray_pointer)]
        entries.extend(
            dict(Path=row["address"], AssetGuid=row["manifest_asset"]["guid"], Asset=row["pointer"])
            for row in sprites
        )
        catalog_body = {
            11400000: dict(
                MonoBehaviour=dict(
                    m_ObjectHideFlags=0,
                    m_CorrespondingSourceObject=dict(fileID=0),
                    m_PrefabInstance=dict(fileID=0),
                    m_PrefabAsset=dict(fileID=0),
                    m_GameObject=dict(fileID=0),
                    m_Enabled=1,
                    m_EditorHideFlags=0,
                    m_Script=dict(fileID=11500000, guid=guid(Path(str(image_script) + ".meta")), type=3),
                    m_Name="OfficialPartnerDialogueRewardImages",
                    m_EditorClassIdentifier="",
                    Entries=entries,
                )
            )
        }
        write_file_checked(catalog_path, remap_guids(emit(catalog_body)))
        catalog_meta = (
            "fileFormatVersion: 2\nguid: "
            + hashlib.md5(b"zjcs:partner-dialogue-reward-images").hexdigest()
            + "\nNativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 11400000\n"
        ).encode("utf8")
        write_file_checked(Path(str(catalog_path) + ".meta"), catalog_meta)

        self.inputs["scripts/prepare_partner_dialogue_reward_assets.py"] = sha(Path(__file__).read_bytes())
        receipt = dict(
            manifest=apk.version,
            generator_sha256=sha(Path(__file__).read_bytes()),
            inputs=self.inputs,
            script_bindings=self.script_sources,
            got_gray_attachment=dict(record=attachment_record, pointer=got_gray_pointer),
            reward_sprites=len(sprites),
            isolated_guids=isolated_guids,
            reused=reused,
            outputs=installed,
            scope="Original 884_156692 GotGrayObj (GotGrayTip) attachment prefab and all 86 partner dialogue RewardItem sprites.",
        )
        proof_path.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + "\n", "utf8", newline="\n")
        print(
            f"Installed OfficialPartnerDialogueRewardImages: 1 attachment + {len(sprites)} reward sprites; "
            f"{len(installed)} outputs ({len(reused)} reused)",
            flush=True,
        )


def main():
    temp_created = False
    import os
    if not os.environ.get("ZJCS_VALIDATION_DIR"):
        import uuid
        work = ROOT / "output/validation" / ("partner-dialogue-rewards-" + uuid.uuid4().hex)
        work.mkdir(parents=True, exist_ok=True)
        (work / ".zjcs-validation.json").write_text(
            json.dumps({"format": 1, "repository": str(ROOT), "ownerPid": os.getpid()}), "utf8"
        )
        os.environ["ZJCS_VALIDATION_DIR"] = str(work)
        temp_created = True
    work = validation_directory()
    try:
        PartnerDialogueRewardBinding(work).build_and_install()
    finally:
        if temp_created and work.exists():
            shutil.rmtree(work, ignore_errors=True)


if __name__ == "__main__":
    main()
