"""Capture exact original minimap mask patterns and their finite truth table."""
import argparse
import json
from pathlib import Path
from official_apk import ROOT
from prepare_unity_map13_sources import encode, meta, sha, source_path, write


def build(cache):
    source = json.loads((cache / "source-manifest.json").read_bytes())
    original, = [c for c in source["components"] if c["type"] == "MiniMapMaskHandler"]
    fields = original["fields"]
    patterns = {}
    for key in ("mCornerPatterns", "mEdgePatterns"):
        patterns[key] = [dict(tile=str(p["MyTile"]["m_PathID"]), DefaultCondition=dict(PosDatas=[
            dict(Pos=c["Pos"], Locked=bool(c["Locked"])) for c in p["DefaultCondition"]["PosDatas"]])) for p in fields[key]]
    dirs = [(0,-1),(-1,-1),(-1,0),(-1,1),(0,1),(1,1),(1,0),(1,-1)]
    cases = []
    for mask in range(256):
        locked = {d for bit,d in enumerate(dirs) if mask & (1 << bit)}
        group = patterns["mEdgePatterns"] if any(x==0 or y==0 for x,y in locked) else patterns["mCornerPatterns"]
        matches = []
        for pattern in group:
            for rotation in range(4):
                conditions = []
                for condition in pattern["DefaultCondition"]["PosDatas"]:
                    x,y = condition["Pos"]["X"],condition["Pos"]["Y"]
                    for _ in range(rotation): x,y=-y,x
                    conditions.append(((x,y) in locked) == condition["Locked"])
                if all(conditions): matches.append((pattern["tile"],rotation))
        if mask and not matches: raise ValueError("Original mask patterns do not cover " + str(mask))
        cases.append(dict(mask=mask, tile=matches[0][0] if mask else "", rotation=matches[0][1] if mask else 0))
    out = ROOT / "unity/Assets/Official/World/Map/13/Source/minimap-mask-patterns.json"
    write(out,encode(dict(manifest=source["manifest"],mapClassId=13,source=original,patterns=patterns,cases=cases)))
    meta(out)
    print("Original minimap mask fixture:",len(cases),"cases",sha(out.read_bytes()))
    return out


if __name__ == "__main__":
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--cache",type=Path,required=True)
    build(parser.parse_args().cache)
