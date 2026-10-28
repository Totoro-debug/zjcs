"""Port the pinned original skill toggle components without changing their state order."""
import hashlib
import re
import uuid
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
CODE = ROOT / ".tools/source-packages/official-code"
OUTPUT = ROOT / "unity/Assets/Game/Runtime/Official/SkillsUI"


def source_records():
    """Record every delivered port against its pinned decompiled input."""
    records = []
    for target in sorted(OUTPUT.glob("*.cs")):
        candidates = [CODE / folder / target.name for folder in (
            "client-source", "framework-firstpass-source", "framework-source/EasingCore",
            "framework-source/FancyScrollView", "framework-source/UGUIJszx")]
        matches = [path for path in candidates if path.is_file()]
        if "namespace Zjcs.Official.SkillsUI.FancyScrollView" in target.read_text(encoding="utf-8"):
            matches = [path for path in matches if "FancyScrollView" in path.parts]
        assert len(matches) == 1, (target.name, matches)
        source = matches[0]
        records.append({"source": source.relative_to(ROOT).as_posix(),
                        "source_sha256": hashlib.sha256(source.read_bytes()).hexdigest(),
                        "output": target.relative_to(ROOT).as_posix(),
                        "output_sha256": hashlib.sha256(target.read_bytes()).hexdigest()})
    return records


def restore():
    records = []
    for name, folder in (("UIToggle", "framework-firstpass-source"),
                         ("UIToggleGroup", "framework-firstpass-source"),
                         ("UIToggleObject", "framework-firstpass-source"),
                         ("UIToggleStatusBase", "framework-firstpass-source"), ("ToggleText", "client-source"),
                         ("HorizontalOrVerticalLayoutGroupJSZX", "framework-source/UGUIJszx"),
                         ("HorizontalLayoutGroupJSZX", "framework-source/UGUIJszx"),
                         ("VerticalLayoutGroupJSZX", "framework-source/UGUIJszx"),
                         ("LayoutElementClamper", "client-source"),
                         ("LoopVerticalScrollWithMaxHeight", "client-source")):
        source = CODE / folder / (name + ".cs")
        raw = source.read_bytes()
        text = raw.decode("utf-8-sig").replace("\r\n", "\n")
        text = re.sub(r"^\s*//IL_.*\n", "", text, flags=re.M)
        text = text.replace("namespace UGUIJszx;\n", "")
        text = text.replace("((LayoutGroup)this).", "")
        text = text.replace("\t\tCalculateLayoutInputHorizontal();", "\t\tbase.CalculateLayoutInputHorizontal();")
        if name == "LayoutElementClamper":
            text = text.replace("using Framework.Logging;\n", "").replace("using Framework.Logging.InterpolatedStringHandler;\n", "")
            start = text.index("\t\t\tGLoggerErrorInterpolatedStringHandler")
            end = text.index("\n\t\t\tfor (int i", start)
            text = text[:start] + '\t\t\tDebug.LogError("Multiple LayoutElementClamper components found on GameObject.");' + text[end:]
            text = text.replace("Object.DestroyImmediate", "UnityEngine.Object.DestroyImmediate")
        if name == "UIToggle":
            for namespace in ("Framework.DependencyInjection", "GFrameCore.Extensions.ZLogger", "Microsoft.Extensions.Logging", "ZLogger"):
                text = text.replace("using " + namespace + ";\n", "")
            text = text.replace("\t[Inject]\n\tprivate ILogger<UIToggle> Logger { get; set; }\n", "")
            start = text.index("\t\t\tILogger logger =")
            end = text.index("\n\t\t}", start)
            text = text[:start] + '\t\t\tDebug.LogError("Toggle的group上没有UIToggleGroup组件");' + text[end:]
        text = text.replace("((Object)(object)", "((UnityEngine.Object)(object)").replace("(Object)null", "(UnityEngine.Object)null")
        if name == "ToggleText":
            text = re.sub(r"\bMultiLanguageText\b", "Zjcs.Official.WorldUI.WorldMultiLanguageText", text)
            text = text.replace("StateSwitcher<bool>", "Zjcs.Official.WorldUI.WorldStateSwitcher<bool>")
        header = re.match(r"(?:using[^\n]*\n|[ \t]*\n)*", text).end()
        text = text[:header] + "namespace Zjcs.Official.SkillsUI\n{\n" + text[header:].rstrip() + "\n}\n"
        text = "// Original " + folder + "/" + name + ".cs; logging and local UI service bindings adapted.\n" + text
        target = OUTPUT / (name + ".cs")
        target.write_text(text, encoding="utf-8")
        target.with_suffix(".cs.meta").write_text("fileFormatVersion: 2\nguid: " + uuid.uuid5(uuid.NAMESPACE_URL, "zjcs/" + target.relative_to(ROOT).as_posix()).hex + "\n", encoding="utf-8")
        records.append({"source": source.relative_to(ROOT).as_posix(), "source_sha256": hashlib.sha256(raw).hexdigest(),
                        "output": target.relative_to(ROOT).as_posix(), "output_sha256": hashlib.sha256(target.read_bytes()).hexdigest()})
    return records


if __name__ == "__main__":
    import json
    print(json.dumps(restore(), ensure_ascii=False, indent=2))
