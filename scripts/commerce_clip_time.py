"""Legacy clip duration, from pinned libunity.so's 0xf9e900 time-range code.

The native icall 0x5755e0 calls 0xf9c434; that returns the range's end.
0xa2bc84 proves the 0x190 vector included by that range is AnimationEvent[].
"""
import math

ENGINE_SHA256 = '5142b00b7ef6fa53828de13afd2cfd07a6a7763100d145cbb50a22e0114a0340'


def legacy_duration(clip):
    assert clip.m_Legacy
    assert not clip.m_CompressedRotationCurves, 'Compressed legacy curve range needs separate decoding'
    times = []
    for name in ['m_RotationCurves','m_EulerCurves','m_PositionCurves','m_ScaleCurves','m_FloatCurves']:
        for curve in getattr(clip,name):
            if curve.curve.m_Curve:times.append(curve.curve.m_Curve[-1].time)
    for curve in clip.m_PPtrCurves:
        if curve.curve:times.append(curve.curve[-1].time+1.0/clip.m_SampleRate)
    times.extend(event.time for event in clip.m_Events if math.isfinite(event.time))
    return max(times) if times else 1.0
