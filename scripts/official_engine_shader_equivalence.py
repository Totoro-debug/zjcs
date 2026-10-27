"""Prove a native engine shader counterpart before binding it by built-in name."""
from copy import copy
import re

from official_platform_shader_programs import gles3_programs
from prepare_unity_map13_sources import sha


def without_framebuffer_fetch(source):
    extension = ("#ifdef GL_EXT_shader_framebuffer_fetch\n"
        "#extension GL_EXT_shader_framebuffer_fetch : enable\n#endif\n")
    source = source.replace(extension, "")
    declaration = ("#ifdef GL_EXT_shader_framebuffer_fetch\n"
        "layout(location = 0) inout mediump vec4 SV_Target0;\n#else\n"
        "layout(location = 0) out mediump vec4 SV_Target0;\n#endif")
    if declaration in source:
        # The canonical counterpart must overwrite the entire output and never
        # read its previous value. Matching the remaining source is mandatory.
        uses = re.findall(r"\bSV_Target0\b([^;]*);", source)
        written = set()
        for use in filter(None, uses):
            assignment = re.fullmatch(r"(?:\.([xyzw]+))?\s*=\s*([^;]+)", use)
            if not assignment or "SV_Target0" in assignment[2]:
                raise ValueError("Engine shader reads framebuffer-fetch output")
            written.update(assignment[1] or "xyzw")
        fragment = source.split("#ifdef FRAGMENT", 1)[1].split("void main()", 1)[1]
        if written != set("xyzw") or re.search(r"\b(?:if|for|while|switch)\s*\(|\bdiscard\b", fragment):
            raise ValueError("Engine shader does not unconditionally overwrite framebuffer output")
        source = source.replace(declaration, "layout(location = 0) out mediump vec4 SV_Target0;")
    if "GL_EXT_shader_framebuffer_fetch" in source:
        raise ValueError("Unhandled engine framebuffer-fetch conditional")
    return source


def executable_cases(shader):
    cases = {}
    for si, original_sub in enumerate(shader.m_ParsedForm.m_SubShaders):
        for pi, original_pass in enumerate(original_sub.m_Passes):
            sub = copy(original_sub); sub.m_Passes = [original_pass]
            form = copy(shader.m_ParsedForm); form.m_SubShaders = [sub]
            selected = copy(shader); selected.m_ParsedForm = form
            for program in gles3_programs(selected, with_keywords=True):
                key = si, pi, tuple(sorted(program["keywords"]))
                if key in cases:
                    raise ValueError("Duplicate engine shader executable case")
                cases[key] = program
    return cases


def project_original(original, counterpart, path="m_ParsedForm"):
    """Check all original non-program fields; reject unexpected added state."""
    if isinstance(original, dict):
        result = {}
        for key, value in original.items():
            if key.startswith("prog") or key == "m_EditorDataHash":
                continue
            if key == "m_Platforms":
                if 9 not in value or not set(value).issubset(counterpart[key]) or 4 not in counterpart[key]:
                    raise ValueError("Engine shader must retain original APIs and include Windows D3D")
                result[key] = value
                continue
            if key not in counterpart:
                raise ValueError("Windows engine shader omitted original field: " + path + "." + key)
            result[key] = project_original(value, counterpart[key], path + "." + key)
        extras = set(counterpart) - set(original)
        for key in extras:
            if key == "m_EditorDataHash":
                continue
            if key == "m_PackageRequirements" and counterpart[key] == dict(
                    m_Requirements=[], m_StatusMessage="", m_CombinedStatus=1):
                continue
            raise ValueError("Additional Windows engine shader state requires proof: " + path + "." + key)
        return result
    if isinstance(original, (list, tuple)):
        if len(original) != len(counterpart):
            raise ValueError("Engine shader field count differs: " + path)
        return [project_original(a, b, path + "[" + str(i) + "]")
                for i, (a, b) in enumerate(zip(original, counterpart))]
    if original != counterpart:
        raise ValueError("Engine shader state differs: " + path)
    return original


def prove(original, counterpart):
    state = project_original(original.read_typetree()["m_ParsedForm"],
                             counterpart.read_typetree()["m_ParsedForm"])
    original_cases = executable_cases(original.read())
    windows_cases = executable_cases(counterpart.read())
    if set(original_cases) != set(windows_cases):
        raise ValueError("Engine shader executable profile differs")
    programs = []
    for (si, pi, keywords), source in original_cases.items():
        native = windows_cases[si, pi, keywords]
        canonical = without_framebuffer_fetch(source["source"])
        if canonical != without_framebuffer_fetch(native["source"]):
            raise ValueError("Windows engine shader executable differs: " + str((si, pi, keywords)))
        programs.append(dict(subshader=si, pass_index=pi, keywords=list(keywords),
            original_index=source["index"], windows_index=native["index"],
            original_source_sha256=sha(source["source"].encode()),
            windows_source_sha256=sha(native["source"].encode()),
            canonical_source_sha256=sha(canonical.encode())))
    return state, programs
