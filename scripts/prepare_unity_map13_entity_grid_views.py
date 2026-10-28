"""Migrate complete original EC grid/transform presentation and coordinate queries."""
from pathlib import Path
import json
import re

from official_apk import ROOT
from prepare_unity_map13_sources import encode, sha, write
from prepare_unity_story_attachment_runtime import CODE, record, method
from prepare_unity_story_playable_runtime import logging_adapter
from prepare_unity_map13_prop_parts import meta

NS = 'Zjcs.Official.World.Map13EntityGridViews'
DEST = ROOT / 'unity/Assets/Game/Runtime/Official/World/Map13EntityGridViews'
RECEIPT = ROOT / 'assets/reference/official-map/map13-entity-grid-views-source-manifest.json'


def build():
    selected = json.loads((CODE.parent / 'code-manifest.json').read_bytes())
    assemblies = [CODE / 'managed/decoded' / n for n in ('Assembly-CSharp.dll', 'Console.dll')]
    for p in assemblies:
        assert sha(p.read_bytes()) == selected['assemblies'][p.name]['decoded_sha256'], p
    inputs, outputs = [], []

    def source(name, common=False):
        p = CODE / ('common-source/Common' if common else 'client-source') / (name + '.cs')
        inputs.append(p)
        return p.read_text(encoding='utf-8-sig')

    def emit(name, body, imports=''):
        body = re.sub(r'(?m)^using (?:Common|Framework(?:\.[\w.]+)?);\n', '', body).replace('namespace Common;', '')
        header, _, code = body.partition('public ')
        attributes = ''.join(re.findall(r'(?m)^\[[^\n]+\]\n', header))
        header = re.sub(r'(?m)^\[[^\n]+\]\n', '', header)
        text = ('// Migrated original grid presentation/query source; see Map13 grid-view receipt.\n'
                + imports + header + '\nnamespace ' + NS + '\n{\n' + attributes + 'public ' + code + '\n}\n')
        p = DEST / (name + '.cs'); write(p, ('\n'.join(l.rstrip() for l in text.splitlines()) + '\n').encode())
        meta(p, 'MonoImporter'); outputs.extend([p, Path(str(p) + '.meta')])

    imports = ('using Object = UnityEngine.Object;\nusing Zjcs.Official.EntityViews;\n'
               'using Zjcs.Official.World.NativeTerrain.Common;\n')
    for name in ['ECGridTransformView', 'ECTransformView', 'ZSetUtils']:
        text, _ = logging_adapter(source(name))
        text = re.sub(r'\bMyEntity\b', 'ECViewEntityBinding', text)
        text = re.sub(r'\bGridTransformComponent\b', 'Map13GridTransformData', text)
        text = re.sub(r'\bTransformComponent\b', 'Map13TransformData', text)
        text = re.sub(r'\bMoveAgent\b', 'Map13MovementAgent', text)
        if name != 'ZSetUtils':
            text = text.replace('public class ', '[Serializable]\npublic class ', 1)
            if name == 'ECTransformView':
                text = 'using System;\n' + text if 'using System;' not in text else text
        # Repair ILSpy's precedence loss without changing the original boolean test.
        text = text.replace('flipX == localScale.x > 0f', 'flipX == (localScale.x > 0f)')
        emit(name, text, imports)

    for name in ['IGridTransformPosUpdatedEventReceiver', 'GridTransformPosUpdatedEventDelegate']:
        text = re.sub(r'\bGridTransformComponent\b', 'Map13GridTransformData', source(name, True))
        emit(name, text, 'using Zjcs.Official.World.Map13EntityEvents;\nusing Zjcs.Official.World.NativeTerrain.Common;\n')

    direction, _ = logging_adapter(method(source('EnumUtils', True), 'public static Quaternion DirToRotation('))
    emit('Map13DirectionQuery', 'using UnityEngine;\npublic static class Map13DirectionQuery\n{\n' + direction + '\n}\n',
         'using Zjcs.Official.World.NativeTerrain.Common;\n')
    text = source('GridTransformComponent', True)
    queries = '\n\n'.join(method(text, sig) for sig in ['public Vector3 GridToPosition(', 'public Pos2d PositionToGrid(',
                           'public static Vector3 GridToPosition(', 'public static Pos2d PositionToGrid('])
    queries = re.sub(r'\bGridGroundComponent\b', 'Map13GridGroundData', queries)
    emit('GridPresentationBoundaries', '''using System.Collections.Generic;
using UnityEngine;
using Zjcs.Official.World.NativeTerrain.Common;
// Required original movement/ground/body-data consumption boundaries. They
// implement no navigation, collision, body occupancy or movement-speed rules.
public abstract class Map13MovementAgent : MonoBehaviour
{ public abstract bool IsMoving { get; } public abstract void Stop(); }
public abstract class Map13GridGroundData
{ public abstract Vector3 GridToPos(Pos2d grid); public abstract Pos2d PosToGrid(Vector3 position); }
public abstract class Map13GridTransformInfo
{ public Vector2 RealCenter; public abstract List<Pos2d> GetBodyRange(); }
public sealed class Map13TransformData
{ public Vector3 Position, Scale; public Quaternion Rotation; }
public sealed class Map13GridTransformData
{
    public GridTransformPosUpdatedEventDelegate PosUpdatedEvent = new GridTransformPosUpdatedEventDelegate();
    public Pos2d Pos;
    public Direction Direction;
    public bool FlipX;
    public Map13GridTransformInfo Info;
    public Map13GridGroundData GridGround;
''' + queries + '\n}\n')
    asm = DEST / (NS + '.asmdef')
    write(asm, encode(dict(name=NS, rootNamespace=NS, references=[
        'Zjcs.Official.EntityViews', 'Zjcs.Official.World.Map13EntityEvents',
        'Zjcs.Official.World.NativeTerrain'], autoReferenced=True)))
    meta(asm, 'AssemblyDefinitionImporter'); meta(DEST, folder=True)
    outputs.extend([asm, Path(str(asm) + '.meta'), Path(str(DEST) + '.meta')])
    for name in ['map14-entity-view-runtime-source-manifest.json', 'map13-entity-events-source-manifest.json',
                 'map13-native-terrain-code-source-manifest.json']:
        p = RECEIPT.with_name(name); inputs.append(p)
        for entry in json.loads(p.read_bytes())['outputs']:
            q = ROOT / entry['path']; assert sha(q.read_bytes()) == entry['sha256'], q
            inputs.append(q)
    write(RECEIPT, encode(dict(manifest='871_156598', mapClassId=13,
        method='Copy complete original ECGridTransformView, ECTransformView and ZSetUtils; copy exact original direction rotation and four shared grid-coordinate query methods. Preserve lifecycle/event order, immediate movement cancellation, source depth/center/flip/body-range branches; explicit read/movement boundaries replace entity infrastructure only.',
        coverage='Original presentation and coordinate queries only. Formal original entity/config/ground/movement host, input/save and Windows Player remain pending.',
        assemblies=[record(p) for p in assemblies], inputs=[record(p) for p in sorted(set(inputs))],
        outputs=[record(p) for p in outputs], tools=[record(Path(__file__).resolve())])))
    print('Migrated original grid/transform views:', len(outputs), 'outputs')


if __name__ == '__main__':
    build()
