"""Compile the unchanged pinned AStar implementation to independent fixtures."""
import hashlib
import json
import random
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / '.tools/source-packages/official-code/common-source'
OUT = ROOT / '.tools/gameplay-fixtures/combat'


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    names = ['AStarUtils', 'FindPathArgContext', 'IAStarMovableHandler', 'IAStarMoveCustomContext']
    original = [(SOURCE / 'AStar' / (name + '.cs')).read_text('utf8') for name in names]
    # Pos2d serialization and Unity conversions are irrelevant to path search.
    code = r'''
using System;using System.Linq;using System.Collections.Generic;using System.Text.Json;
namespace Common {
public readonly struct Pos2d : IEquatable<Pos2d> {
 public readonly int X,Y;public Pos2d(int x,int y){X=x;Y=y;}
 public static readonly Pos2d[] FourDirs={new(0,-1),new(-1,0),new(0,1),new(1,0)};
 public static Pos2d operator +(Pos2d a,Pos2d b)=>new(a.X+b.X,a.Y+b.Y);
 public static bool operator ==(Pos2d a,Pos2d b)=>a.Equals(b);
 public static bool operator !=(Pos2d a,Pos2d b)=>!a.Equals(b);
 public bool Equals(Pos2d b)=>X==b.X&&Y==b.Y;
 public override bool Equals(object b)=>b is Pos2d p&&Equals(p);
 public override int GetHashCode()=>HashCode.Combine(X,Y);
 public static int Distance(Pos2d a,Pos2d b)=>Math.Abs(a.X-b.X)+Math.Abs(a.Y-b.Y);
}}
'''
    for text in original:
        header, body = text.split('namespace AStar;', 1)
        code += 'namespace AStar {' + header + body + '}\n'
    code += r'''
namespace AStar {
public class Context:IAStarMoveCustomContext{}
public class Handler:IAStarMovableHandler {
 public int[] bounds;public HashSet<Common.Pos2d> blocked=new();public HashSet<string> edges=new();
 public bool IsFit(Common.Pos2d p,IAStarMoveCustomContext c)=>p.X>=bounds[0]&&p.Y>=bounds[1]&&p.X<=bounds[2]&&p.Y<=bounds[3]&&!blocked.Contains(p);
 public bool CanMoveToNext(Common.Pos2d a,Common.Pos2d b)=>!edges.Contains($"{a.X}:{a.Y}>{b.X}:{b.Y}");
}
public static class Reference {
 static Common.Pos2d Pos(JsonElement x)=>new(x[0].GetInt32(),x[1].GetInt32());
 public static string[] Evaluate(string input){var results=new List<string>();
 foreach(var c in JsonDocument.Parse(input).RootElement.EnumerateArray()){
  var h=new Handler{bounds=c.GetProperty("bounds").EnumerateArray().Select(x=>x.GetInt32()).ToArray()};
  foreach(var p in c.GetProperty("blocked").EnumerateArray())h.blocked.Add(Pos(p));
  foreach(var e in c.GetProperty("edges").EnumerateArray())h.edges.Add(e.GetString());
  var builder=new FindPathArgContext.Builder(Pos(c.GetProperty("start")),Pos(c.GetProperty("end")))
   .WithCustomContext(new Context()).WithBodyRanges(c.GetProperty("body").EnumerateArray().Select(Pos).ToList())
   .WithMaxStep(c.GetProperty("max_step").GetInt32()).WithPreciseTarget(c.GetProperty("precise").GetBoolean())
   .WithPreferDist(c.GetProperty("prefer_dist").GetInt32()).WithFirstStepPosList(c.GetProperty("first_steps").EnumerateArray().Select(Pos).ToList());
  if(c.TryGetProperty("end_cells",out var endings)){var cells=endings.EnumerateArray().Select(Pos).ToHashSet();builder.WithCheckEndDel(p=>cells.Contains(p));}
  var path=AStarUtils.FindPath(h,builder.Build());
  results.Add(JsonSerializer.Serialize(path?.Select(p=>new[]{p.X,p.Y}).ToArray()));
 }return results.ToArray();
}}}
'''
    rng = random.Random(854156383)
    cases = []
    for i in range(160):
        cases.append(dict(name=f'original-astar-{i}', bounds=[-3,-3,6,6],
            start=[0,0], end=[rng.randrange(-3,7),rng.randrange(-3,7)],
            body=[[0,0]] if i % 3 else [[0,0],[1,0],[0,1]],
            blocked=[[x,y] for x in range(-3,7) for y in range(-3,7) if rng.random()<0.23],
            edges=['0:0>0:-1','1:0>1:1'] if i % 4 else [],
            first_steps=[[1,1]] if i % 11 == 0 else [],
            max_step=rng.choice([-1,1,2,3,6,20]), precise=bool(i % 2), prefer_dist=i % 3,
            **(dict(end_cells=[[3,2],[3,3]]) if i % 7 == 0 else {})))
    harness = OUT / 'PinnedNavigation.cs'
    harness.write_text(code, 'utf8')
    inputs = OUT / 'navigation-input.json'
    inputs.write_text(json.dumps(cases), 'utf8')
    runner = OUT / 'run-navigation-reference.ps1'
    def quote(path): return str(path).replace("'", "''")
    runner.write_text("$ErrorActionPreference='Stop'\nAdd-Type -Path '"+quote(harness)+"'\n[AStar.Reference]::Evaluate([IO.File]::ReadAllText('"+quote(inputs)+"'))", 'utf8')
    result = subprocess.run(['pwsh','-NoProfile','-File',str(runner)], capture_output=True,text=True,encoding='utf8')
    if result.returncode: raise RuntimeError(result.stdout + result.stderr)
    lines = result.stdout.splitlines()
    assert len(lines) == len(cases)
    for case, line in zip(cases, lines): case['expected'] = json.loads(line)
    (OUT / 'navigation-reference.json').write_text(json.dumps(dict(manifest='854_156383',cases=cases,
        original_code_sha256={name:hashlib.sha256((SOURCE/'AStar'/(name+'.cs')).read_bytes()).hexdigest() for name in names}),ensure_ascii=False,separators=(',',':'))+'\n','utf8')
    print('Original AStar reference:', len(cases), 'cases')


if __name__ == '__main__': main()
