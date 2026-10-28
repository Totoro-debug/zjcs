using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

// Only the child/name/parent surface used by Find is modeled here. No Unity rendering or input is exercised.
internal sealed class Transform
{
    public string name;
    public Transform parent;
    public readonly List<Transform> children = new List<Transform>();
    public Transform GetChild(int index) => children[index];
}

internal static class Paths
{
    /* ACTUAL_FIND_METHOD */

    private static int Main(string[] args)
    {
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(args[0]));
            int named = 0, references = 0, matchingHosts = 0, matchingGroups = 0, matchingTargets = 0, absentMatchingTargets = 0;
            foreach (JsonElement prefab in document.RootElement.GetProperty("prefabs").EnumerateArray())
            {
                var nodes = new Dictionary<string, Transform>();
                JsonElement[] records = prefab.GetProperty("transforms").EnumerateArray()
                    .OrderBy(row => row.GetProperty("path").GetString().Count(c => c == '/') + (row.GetProperty("path").GetString() == "" ? 0 : 1)).ToArray();
                foreach (JsonElement record in records)
                {
                    string path = record.GetProperty("path").GetString();
                    string[] names = record.GetProperty("objects").EnumerateArray().Select(x => x.GetString()).ToArray();
                    var node = new Transform { name = names[names.Length - 1] };
                    nodes.Add(path, node);
                    if (path == "") node.name += "(Clone)";
                    else
                    {
                        int separator = path.LastIndexOf('/');
                        string parent = separator < 0 ? "" : path.Substring(0, separator);
                        int index = int.Parse(path.Substring(separator + 1));
                        node.parent = nodes[parent];
                        if (node.parent.children.Count != index) throw new Exception("Source hierarchy has a child gap.");
                        node.parent.children.Add(node);
                    }
                    if (Find(nodes[""], path, names) != node) throw new Exception("Named source path resolved to another node.");
                    named++;
                }
                foreach (JsonElement button in prefab.GetProperty("buttons").EnumerateArray())
                {
                    JsonElement target = button.GetProperty("target");
                    if (target.ValueKind == JsonValueKind.Null) continue;
                    string path = target.GetString();
                    if (Find(nodes[""], path, null) != nodes[path]) throw new Exception("Button Graphic reference resolved to another node.");
                    references++;
                }
                if (prefab.TryGetProperty("popup", out JsonElement popup) && popup.ValueKind == JsonValueKind.Object
                    && popup.TryGetProperty("matching", out JsonElement matching))
                {
                    string hostPath = matching.GetProperty("path").GetString();
                    string[] hostNames = matching.GetProperty("objects").EnumerateArray().Select(x => x.GetString()).ToArray();
                    Transform host = Find(nodes[""], hostPath, hostNames);
                    if (host != nodes[hostPath]) throw new Exception("Matching animation resolved to another host.");
                    matchingHosts++;
                    foreach (JsonElement group in matching.GetProperty("canvasGroups").EnumerateArray())
                    {
                        string relative = group.GetProperty("path").GetString();
                        string[] names = group.GetProperty("objects").EnumerateArray().Select(x => x.GetString()).ToArray();
                        if (Find(host, relative, names) != nodes[hostPath + "/" + relative])
                            throw new Exception("Matching CanvasGroup reference resolved outside its original host.");
                        matchingGroups++;
                    }
                    foreach (JsonElement target in matching.GetProperty("targets").EnumerateArray())
                    {
                        Transform node = host;
                        foreach (string part in target.GetProperty("path").GetString().Split('/'))
                            node = node.children.Single(child => child.name == part);
                        if (node == null) throw new Exception("Matching curve target is absent from the source hierarchy.");
                        matchingTargets++;
                    }
                    foreach (JsonElement target in matching.GetProperty("absentSourceTargets").EnumerateArray())
                    {
                        Transform node = host;
                        foreach (string part in target.GetProperty("path").GetString().Split('/'))
                            node = node?.children.SingleOrDefault(child => child.name == part);
                        if (node != null) throw new Exception("An officially absent shared-clip target was replaced with a fabricated node.");
                        absentMatchingTargets++;
                    }
                }
                bool rejected = false;
                try { Find(nodes[""], "", new[] { "wrong original root" }); }
                catch (InvalidDataException) { rejected = true; }
                if (!rejected) throw new Exception("Mismatched original names must still be rejected.");
                rejected = false;
                try { Find(nodes[""], "", Array.Empty<string>()); }
                catch (InvalidDataException) { rejected = true; }
                if (!rejected) throw new Exception("Empty name assertions must still be rejected.");
                JsonElement childRecord = records.First(row => row.GetProperty("path").GetString() != "");
                string childPath = childRecord.GetProperty("path").GetString();
                string[] childNames = childRecord.GetProperty("objects").EnumerateArray().Select(x => x.GetString()).ToArray();
                string originalRootName = nodes[""].name;
                nodes[""].name = "changed parent";
                rejected = false;
                try { Find(nodes[""], childPath, childNames); }
                catch (InvalidDataException) { rejected = true; }
                nodes[""].name = originalRootName;
                if (!rejected) throw new Exception("Mismatched original parent names must still be rejected.");
            }
            if (references == 0) throw new Exception("No real Button Graphic references were replayed.");
            if (matchingHosts != 1 || matchingGroups != 2 || matchingTargets != 17 || absentMatchingTargets != 3)
                throw new Exception("The original matching animation host, groups or curve targets were not replayed.");
            Console.WriteLine($"Dungeon path replay passed: {named} original nodes, {references} Button Graphic references, {matchingHosts} matching host, {matchingGroups} CanvasGroups, {matchingTargets} curve targets and {absentMatchingTargets} absent source target groups; wrong names and parents rejected.");
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error.GetType().Name + ": " + error.Message);
            return 1;
        }
    }
}
