"""Select actual shader executables by platform and native GPU program type.

Built-in shader tables share numeric blob indices across graphics APIs. Passing
the union of all API indices to a selected table can decode parameter records
as programs; selection must precede decompression and program decoding.
"""
from copy import copy

from official_shader_programs import gles_programs


STAGES = ("progVertex", "progFragment", "progGeometry", "progHull", "progDomain", "progRayTracing")


def platform_shader(shader, platform, gpu_types):
    matches = [i for i, value in enumerate(shader.platforms) if value == platform]
    if len(matches) != 1:
        raise ValueError("Original shader graphics API table is not unique: " + str(platform))
    index = matches[0]
    selected = copy(shader)
    selected.platforms = [platform]
    for field in ("offsets", "compressedLengths", "decompressedLengths"):
        setattr(selected, field, [getattr(shader, field)[index]])
    form = copy(shader.m_ParsedForm)
    selected.m_ParsedForm = form
    form.m_SubShaders = []
    for original_sub in shader.m_ParsedForm.m_SubShaders:
        sub = copy(original_sub); sub.m_Passes = []; form.m_SubShaders.append(sub)
        for original_pass in original_sub.m_Passes:
            pass_ = copy(original_pass); sub.m_Passes.append(pass_)
            for name in STAGES:
                original = getattr(original_pass, name, None)
                if original is None:
                    continue
                stage = copy(original)
                stage.m_SubPrograms = [p for p in original.m_SubPrograms if p.m_GpuProgramType in gpu_types]
                stage.m_PlayerSubPrograms = [[p for p in group if p.m_GpuProgramType in gpu_types]
                    for group in (original.m_PlayerSubPrograms or [])]
                setattr(pass_, name, stage)
    return selected


def gles3_programs(shader, with_keywords=False):
    selected = platform_shader(shader, 9, {4})
    if not any(stage.m_SubPrograms or any(stage.m_PlayerSubPrograms or [])
               for sub in selected.m_ParsedForm.m_SubShaders for pass_ in sub.m_Passes
               for name in STAGES for stage in [getattr(pass_, name, None)] if stage is not None):
        raise ValueError("Original shader has no referenced GLES3 executable")
    result = gles_programs(selected, with_keywords=with_keywords)
    if any(p["type"] != "kShaderGpuProgramGLES3" or not p["source"].startswith("#ifdef VERTEX\n")
           or "#ifdef FRAGMENT\n" not in p["source"] for p in result):
        raise ValueError("Referenced GLES3 entry is not a combined executable source program")
    return result
