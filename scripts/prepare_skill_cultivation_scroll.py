"""Adapt the pinned official horizontal skill cultivation scroll component."""
import hashlib
import re
import uuid
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def restore():
    source = ROOT / ".tools/source-packages/official-code/loop-scroll-full-source/UnityEngine.UI/LoopHorizontalScrollRect.cs"
    raw = source.read_bytes()
    source_hash = hashlib.sha256(raw).hexdigest()
    assert source_hash == "fcb6aa1db13c8fc37c35972df4e0c1592e18b0a0b5ad52cb6293df9042ff6b22"
    text = raw.decode("utf-8-sig")
    text = re.sub(r"^\s*//IL_.*\n", "", text, flags=re.M)
    text = text.replace("namespace UnityEngine.UI;", "namespace UnityEngine.UI\n{") + "\n}\n"
    text = text.replace("Object.op_Implicit((Object)(object)HorizontalLayoutGroup)", "(bool)HorizontalLayoutGroup")
    text = text.replace("Object.op_Implicit((Object)(object)m_Content)", "(bool)m_Content")
    text = text.replace("((UIBehaviour)this).Awake();", "base.Awake();")
    assert "op_" not in text and "Unknown" not in text
    output = ROOT / "unity/Assets/Game/Runtime/Official/WorldUI/LoopScroll/LoopHorizontalScrollRect.cs"
    output.write_text(text, encoding="utf-8")
    meta = output.with_suffix(".cs.meta")
    if not meta.exists():
        meta.write_text("fileFormatVersion: 2\nguid: " + uuid.uuid5(uuid.NAMESPACE_URL, "zjcs-official/" + output.relative_to(ROOT).as_posix()).hex + "\n", encoding="utf-8")
    return {"source": source.relative_to(ROOT).as_posix(), "sha256": source_hash,
            "output": output.relative_to(ROOT).as_posix(), "output_sha256": hashlib.sha256(output.read_bytes()).hexdigest(),
            "operation": "C# namespace and decompiler operator/base-call syntax repair; retain original scrolling and virtualization."}


if __name__ == "__main__":
    print(restore())
