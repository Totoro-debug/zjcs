using System;
using System.IO;
using System.Linq;
using UnityEngine;
using Zjcs.Offline.World.MapGridVisibility;
using Zjcs.Official.World.Map13GridVisibility;
using Zjcs.Official.World.NativeTerrain.Common;
using Zjcs.Official.World.NativeMap13Labels;

namespace Zjcs.Editor.Map13GridVisibility
{
    public static class OfficialMap13LocalVisibilityAcceptance
    {
        [Serializable]private sealed class Save {public string visibility;public int x,y;}
        public static void Run()
        {
            OfficialMap13GridVisibilityAcceptance.Run();
            var config=JsonUtility.FromJson<OfflineMapGridVisibility.Configuration>(Resources.Load<TextAsset>("Map13/grid-visibility").text);
            config.characterClassId=3001;
            var bodies=JsonUtility.FromJson<OfflineMapGridVisibility.BodyConfiguration>(Resources.Load<TextAsset>("Map13/grid-entities").text);
            var labels=JsonUtility.FromJson<Map13LabelData>(Resources.Load<TextAsset>("Map13/Native/region-label-data").text);
            Require(bodies.entities.Length==2690&&bodies.templates.Length==148,"Original body closure differs.");
            int RegionAt(Pos2d p)=>p.X>=0&&p.Y>=0&&p.X<labels.width&&p.Y<labels.height?labels.regionGrid[p.Y*labels.width+p.X]:-1;
            var initial=bodies.entities.Single(e=>e.placementId==101).transform.Pos;
            int initialCount,movedCount;byte[] snapshot;
            using(var session=new OfflineMapGridVisibility(config,bodies,initial,null,Array.Empty<int>(),RegionAt,_=>true))
            {
                initialCount=Count(session);Require(initialCount>=61&&initialCount<10000,"New local state should contain the original sight/body closure.");
                var previous=session.Snapshot();var next=new Pos2d(initial.X+1,initial.Y);session.MovePlayer(next);movedCount=Count(session);
                Require(movedCount>=initialCount&&session.IsVisible(initial)&&session.IsVisible(next),"Original local sight did not persist movement.");
                foreach(var region in config.regions)session.UnlockRegion(region.region);
                foreach(var region in config.regions)
                    foreach(var p in session.RegionPositions(region.region))
                        if(p.X>=0&&p.Y>=0&&p.X<240&&p.Y<240)Require(session.IsVisible(p),"Original region cell remained hidden.");
                snapshot=session.Snapshot();
                var directory=Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR")??throw new InvalidDataException("Explicit isolated save directory required.");
                var savePath=Path.Combine(directory,"map13-visibility-diagnostic-save.json");
                File.WriteAllText(savePath,JsonUtility.ToJson(new Save{visibility=Convert.ToBase64String(snapshot),x=next.X,y=next.Y},true));
                var saved=JsonUtility.FromJson<Save>(File.ReadAllText(savePath));
                using(var restored=new OfflineMapGridVisibility(config,bodies,new Pos2d(saved.x,saved.y),Convert.FromBase64String(saved.visibility),Array.Empty<int>(),RegionAt,_=>true))
                {
                    for(int y=0;y<240;y++)for(int x=0;x<240;x++)Require(session.IsVisible(new Pos2d(x,y))==restored.IsVisible(new Pos2d(x,y)),"Local file restore differs.");
                    Require(snapshot.SequenceEqual(restored.Snapshot()),"Local restore changed original compression bytes.");
                }
                var raw=GridVisibilityCompressUtils.DoMyUnzip(previous);
                foreach(var body in bodies.templates)
                {
                    var info=body.body;info.Init();var center=new Pos2d(73,97);
                    var actual=info.GetBodyAddOneRange(center);
                    foreach(var cell in info.BodyRangeInfos)
                    {
                        Require(actual.Contains(center+cell.Pos),"Original body cell omitted.");
                        foreach(var dir in Pos2d.EightDirs)Require(actual.Contains(center+cell.Pos+dir),"Original body eight-neighbor expansion differs.");
                    }
                    // Shared source returns a mutable cached set; local entities
                    // must own their snapshots instead of sharing this cache.
                    var frozen=actual.ToArray();info.GetBodyAddOneRange(new Pos2d(19,18));
                    Require(frozen.All(p=>p.X>30&&p.Y>30),"Body fixture coordinates are invalid.");
                }
            }
            bool wrongSize=false;try{using var invalid=new OfflineMapGridVisibility(config,bodies,initial,BitConverter.GetBytes(2).Concat(BitConverter.GetBytes(3)).ToArray(),Array.Empty<int>(),RegionAt,_=>false);}catch(InvalidDataException){wrongSize=true;}
            Require(wrongSize,"Mismatched local dimensions should fail before original component starts.");
            Debug.Log("Map13 local visibility passed: 148 body templates, 2690 placements, "+initialCount+" initial / "+movedCount+" moved cells, 106 original region activations, full isolated file restore and exact compressed bytes.");
        }
        private static int Count(OfflineMapGridVisibility session){int count=0;for(int y=0;y<240;y++)for(int x=0;x<240;x++)if(session.IsVisible(new Pos2d(x,y)))count++;return count;}
        private static void Require(bool value,string message){if(!value)throw new InvalidDataException(message);}
    }
}
