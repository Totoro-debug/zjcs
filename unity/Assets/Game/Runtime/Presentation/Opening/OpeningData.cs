using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using UnityEngine;

namespace Zjcs.Presentation.Opening
{
    /// <summary>Minimal JSON reader for the official opening payloads.</summary>
    public static class OpeningJson
    {
        public static object Parse(byte[] bytes)
        {
            if (bytes == null) throw new ArgumentNullException(nameof(bytes));
            return new Reader(Encoding.UTF8.GetString(bytes)).ReadValue();
        }

        private sealed class Reader
        {
            private readonly string text;
            private int index;

            public Reader(string text)
            {
                this.text = text ?? throw new ArgumentNullException(nameof(text));
            }

            public object ReadValue()
            {
                SkipSpace();
                if (index >= text.Length) throw new FormatException("JSON ended unexpectedly.");
                char token = text[index];
                switch (token)
                {
                    case '{': return ReadObject();
                    case '[': return ReadArray();
                    case '"': return ReadString();
                    case 't': Expect("true"); return true;
                    case 'f': Expect("false"); return false;
                    case 'n': Expect("null"); return null;
                    default: return ReadNumber();
                }
            }

            private Dictionary<string, object> ReadObject()
            {
                var result = new Dictionary<string, object>(StringComparer.Ordinal);
                index++;
                SkipSpace();
                if (index < text.Length && text[index] == '}') { index++; return result; }
                while (true)
                {
                    SkipSpace();
                    string key = ReadString();
                    SkipSpace();
                    if (text[index] != ':') throw new FormatException("JSON object expects ':' at " + index);
                    index++;
                    result[key] = ReadValue();
                    SkipSpace();
                    if (index >= text.Length) throw new FormatException("JSON object is not closed.");
                    if (text[index] == ',') { index++; continue; }
                    if (text[index] == '}') { index++; return result; }
                    throw new FormatException("JSON object has an unexpected token at " + index);
                }
            }

            private List<object> ReadArray()
            {
                var result = new List<object>();
                index++;
                SkipSpace();
                if (index < text.Length && text[index] == ']') { index++; return result; }
                while (true)
                {
                    result.Add(ReadValue());
                    SkipSpace();
                    if (index >= text.Length) throw new FormatException("JSON array is not closed.");
                    if (text[index] == ',') { index++; continue; }
                    if (text[index] == ']') { index++; return result; }
                    throw new FormatException("JSON array has an unexpected token at " + index);
                }
            }

            private string ReadString()
            {
                if (text[index] != '"') throw new FormatException("JSON expects a string at " + index);
                index++;
                var builder = new StringBuilder();
                while (true)
                {
                    if (index >= text.Length) throw new FormatException("JSON string is not closed.");
                    char value = text[index++];
                    if (value == '"') return builder.ToString();
                    if (value != '\\') { builder.Append(value); continue; }
                    char escape = text[index++];
                    switch (escape)
                    {
                        case '"': builder.Append('"'); break;
                        case '\\': builder.Append('\\'); break;
                        case '/': builder.Append('/'); break;
                        case 'b': builder.Append('\b'); break;
                        case 'f': builder.Append('\f'); break;
                        case 'n': builder.Append('\n'); break;
                        case 'r': builder.Append('\r'); break;
                        case 't': builder.Append('\t'); break;
                        case 'u':
                            builder.Append((char)int.Parse(text.Substring(index, 4), NumberStyles.HexNumber));
                            index += 4;
                            break;
                        default: throw new FormatException("JSON escape is invalid at " + index);
                    }
                }
            }

            private object ReadNumber()
            {
                int start = index;
                while (index < text.Length && "-+.eE0123456789".IndexOf(text[index]) >= 0) index++;
                string value = text.Substring(start, index - start);
                if (value.Length == 0 || !double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double number))
                    throw new FormatException("JSON number is invalid at " + start);
                return number;
            }

            private void Expect(string literal)
            {
                if (string.CompareOrdinal(text, index, literal, 0, literal.Length) != 0)
                    throw new FormatException("JSON literal is invalid at " + index);
                index += literal.Length;
            }

            private void SkipSpace()
            {
                while (index < text.Length && char.IsWhiteSpace(text[index])) index++;
            }
        }

        // ------------------------------------------------------------- helpers
        public static Dictionary<string, object> Object(object value, string what)
        {
            return value as Dictionary<string, object>
                ?? throw new FormatException("Opening payload expected an object: " + what);
        }

        public static List<object> Array(object value, string what)
        {
            return value as List<object>
                ?? throw new FormatException("Opening payload expected an array: " + what);
        }

        public static string String(Dictionary<string, object> node, string key, string fallback = "")
        {
            return node != null && node.TryGetValue(key, out object value) && value is string text ? text : fallback;
        }

        public static double Number(Dictionary<string, object> node, string key, double fallback = 0)
        {
            return node != null && node.TryGetValue(key, out object value) ? ToNumber(value, fallback) : fallback;
        }

        public static int Int(Dictionary<string, object> node, string key, int fallback = 0)
        {
            return (int)Number(node, key, fallback);
        }

        public static bool Bool(Dictionary<string, object> node, string key, bool fallback = false)
        {
            return node != null && node.TryGetValue(key, out object value) && value is bool flag ? flag : fallback;
        }

        public static double ToNumber(object value, double fallback = 0)
        {
            return value is double number ? number : fallback;
        }

        public static List<int> IntList(Dictionary<string, object> node, string key)
        {
            var result = new List<int>();
            if (node == null || !node.TryGetValue(key, out object value) || !(value is List<object> list)) return result;
            foreach (object entry in list) result.Add((int)ToNumber(entry));
            return result;
        }
    }

    /// <summary>One node of an official opening story graph.</summary>
    public sealed class OpeningStoryNode
    {
        public int Id;
        public string Type = string.Empty;
        public string Name = string.Empty;
        public List<int> Enter = new List<int>();
        public List<int> Exit = new List<int>();
        public float Seconds;
        public bool NeedMask;
        public int DialogId;
        public List<int> DialogItems = new List<int>();
        public bool AutoClose;
        public bool Immediately;
        public bool KeepBlackScreen;
        public bool Instant;
        public int LifeType;
        public float AutoWait;
        public float AutoDura;
        public bool AutoPlay;
        public string WindowPath = string.Empty;
        public string Behaviour = string.Empty;
        public List<string> Playables = new List<string>();
        public bool WaitPlayable;
        public float PlayableDura;
        public bool HideMainUi;
        public bool InStoryMode;
    }

    public sealed class OpeningStoryGraph
    {
        public string Name = string.Empty;
        public int Order;
        public bool ShowSkipButton;
        public readonly Dictionary<int, OpeningStoryNode> Nodes = new Dictionary<int, OpeningStoryNode>();

        public OpeningStoryNode Start
        {
            get
            {
                foreach (OpeningStoryNode node in Nodes.Values)
                    if (node.Type == "StartStoryNode") return node;
                throw new InvalidOperationException("Official story graph has no start node: " + Name);
            }
        }
    }

    public sealed class OpeningDialogLine
    {
        public string Story = string.Empty;
        public int Id;
        public int Order;
        public string Text = string.Empty;
        public string DialogType = string.Empty;
        public string Voice = string.Empty;
        public int RoleId;
        public string RoleFace = string.Empty;
    }

    public sealed class OpeningRole
    {
        public int Id;
        public string Name = string.Empty;
        public string PortraitPath = string.Empty;
        public string PortraitPrefabKey = string.Empty;
        public bool NeedFlipOnRight;
        public Vector2 PortraitFaceOffset;
        public readonly Dictionary<string, string> PortraitFaceSprites =
            new Dictionary<string, string>(StringComparer.Ordinal);
        public readonly Dictionary<string, Vector2> PortraitFaceOffsets =
            new Dictionary<string, Vector2>(StringComparer.Ordinal);
    }

    public sealed class OpeningGridCell
    {
        public int X;
        public int Y;
        public int Ground;
        public int Height;
    }

    public sealed class OpeningStageData
    {
        public int StageId;
        public int GameId;
        public string Scene = string.Empty;
        public float OriginX;
        public float OriginY;
        public int SizeX;
        public int SizeY;
        public readonly List<OpeningGridCell> Cells = new List<OpeningGridCell>();
        public readonly Dictionary<int, string> Models = new Dictionary<int, string>();
    }

    public sealed class OpeningStoryData
    {
        public string Manifest = string.Empty;
        public readonly Dictionary<string, OpeningStoryGraph> Graphs =
            new Dictionary<string, OpeningStoryGraph>(StringComparer.Ordinal);
        public readonly List<OpeningDialogLine> Dialogues = new List<OpeningDialogLine>();
        public readonly Dictionary<int, OpeningRole> Roles = new Dictionary<int, OpeningRole>();

        public IEnumerable<OpeningDialogLine> FindDialogues(string story, int dialogId)
        {
            foreach (OpeningDialogLine line in Dialogues)
                if (line.Story == story && line.Id == dialogId) yield return line;
        }

        public OpeningStoryGraph RequireGraph(string name)
        {
            return Graphs.TryGetValue(name, out OpeningStoryGraph graph)
                ? graph : throw new InvalidOperationException("Official opening story is missing: " + name);
        }
    }

    /// <summary>Official pre-recorded fight record.</summary>
    public sealed class OpeningRecord
    {
        public long GameId;
        public int StageId;
        public float FightSpeed;
        public byte[] Compressed;
        public readonly List<OpeningRecordEntity> Entities = new List<OpeningRecordEntity>();
        public readonly List<OpeningRecordRound> Rounds = new List<OpeningRecordRound>();
        public readonly List<string> StartMessageTypes = new List<string>();
        public string StatusChangeValue = string.Empty;

        public int StepCount
        {
            get
            {
                int count = 0;
                foreach (OpeningRecordRound round in Rounds) count += round.Steps.Count;
                return count;
            }
        }

        public static OpeningRecord Parse(byte[] compressed)
        {
            if (compressed == null || compressed.Length == 0)
                throw new InvalidOperationException("Official opening record is empty.");
            byte[] decoded;
            if (compressed.Length >= 2 && compressed[0] == 0x1F && compressed[1] == 0x8B)
            {
                using (var input = new MemoryStream(compressed))
                using (var gzip = new GZipStream(input, CompressionMode.Decompress))
                using (var output = new MemoryStream())
                {
                    gzip.CopyTo(output);
                    decoded = output.ToArray();
                }
            }
            else
            {
                decoded = compressed;
            }

            Dictionary<string, object> root = OpeningJson.Object(OpeningJson.Parse(decoded), "record");
            var record = new OpeningRecord
            {
                GameId = (long)OpeningJson.Number(root, "GameId"),
                StageId = OpeningJson.Int(root, "StageClassId"),
                Compressed = compressed,
            };
            var entities = new List<object>();
            foreach (object item in OpeningJson.Array(root["GameStartMsgs"], "GameStartMsgs"))
            {
                Dictionary<string, object> message = OpeningJson.Object(item, "start message");
                string type = OpeningJson.String(message, "Item1");
                record.StartMessageTypes.Add(type);
                Dictionary<string, object> body = OpeningJson.Object(message["Item2"], "start message body");
                if (type == "GAME_ENTITY_LIST")
                    entities = OpeningJson.Array(body["EntityList"], "EntityList");
                else if (type == "GAME_PLAY_STATUS_CHANGE")
                    record.StatusChangeValue = OpeningJson.Number(body, "Val").ToString(CultureInfo.InvariantCulture);
            }

            bool foundFightSpeed = false;
            foreach (object rawEntity in entities)
            {
                Dictionary<string, object> entity = OpeningJson.Object(rawEntity, "entity");
                foreach (object rawComponent in OpeningJson.Array(entity["ComponentList"], "ComponentList"))
                {
                    Dictionary<string, object> component = OpeningJson.Object(rawComponent, "component");
                    if (!OpeningJson.String(component, "$type").EndsWith(".FightComponent, GameServer", StringComparison.Ordinal))
                        continue;
                    record.FightSpeed = (float)OpeningJson.Number(component, "Speed");
                    foundFightSpeed = record.FightSpeed > 0f;
                    break;
                }
                if (foundFightSpeed) break;
            }
            if (!foundFightSpeed) throw new FormatException("Opening fight record has no valid FightComponent speed.");

            foreach (object item in entities)
                record.Entities.Add(OpeningRecordEntity.Parse(OpeningJson.Object(item, "entity")));
            foreach (object item in OpeningJson.Array(root["RoundMsgList"], "RoundMsgList"))
                record.Rounds.Add(OpeningRecordRound.Parse(OpeningJson.Object(item, "round")));
            if (record.Entities.Count == 0 || record.Rounds.Count == 0)
                throw new FormatException("Official opening record has no entities or rounds.");
            return record;
        }
    }

    public sealed class OpeningRecordEntity
    {
        public int ClassId;
        public int EntityId;
        public string CharacterType = string.Empty;
        public int MonsterId;
        public int GridX;
        public int GridY;
        public string Direction = string.Empty;
        public long Hp;
        public long MaxHp;
        public int Faction;
        public float Level;
        public string Profession = string.Empty;
        public int NpcClassId;
        public readonly HashSet<string> Components = new HashSet<string>(StringComparer.Ordinal);

        public static OpeningRecordEntity Parse(Dictionary<string, object> node)
        {
            var entity = new OpeningRecordEntity
            {
                ClassId = OpeningJson.Int(node, "ClassId"),
            };
            Dictionary<string, object> id = OpeningJson.Object(node["Id"], "entity id");
            entity.EntityId = OpeningJson.Int(id, "EntityId");
            foreach (object item in OpeningJson.Array(node["ComponentList"], "ComponentList"))
            {
                Dictionary<string, object> component = OpeningJson.Object(item, "component");
                string type = OpeningJson.String(component, "$type");
                int comma = type.IndexOf(',');
                string name = comma > 0 ? type.Substring(0, comma) : type;
                int dot = name.LastIndexOf('.');
                if (dot >= 0) name = name.Substring(dot + 1);
                entity.Components.Add(name);
                switch (name)
                {
                    case "FightRoleCharacterComponent":
                        entity.CharacterType = OpeningJson.String(component, "CharacterType");
                        break;
                    case "FightRoleMonsterComponent":
                        entity.MonsterId = OpeningJson.Int(component, "MonsterId");
                        break;
                    case "GridTransformComponent":
                        Dictionary<string, object> position = OpeningJson.Object(component["MyPos"], "grid position");
                        entity.GridX = OpeningJson.Int(position, "X");
                        entity.GridY = OpeningJson.Int(position, "Y");
                        entity.Direction = OpeningJson.String(component, "Direction");
                        break;
                    case "FightRoleFightComponent":
                        entity.Hp = (long)OpeningJson.Number(component, "Hp");
                        entity.MaxHp = (long)OpeningJson.Number(component, "LastMaxHp");
                        entity.Faction = OpeningJson.Int(component, "Faction");
                        break;
                    case "LevelComponent":
                        entity.Level = (float)OpeningJson.Number(component, "Level");
                        break;
                    case "CharacterComponent":
                        entity.Profession = OpeningJson.String(component, "Profession");
                        if (component.TryGetValue("TargetData", out object target)
                            && target is Dictionary<string, object> targetData)
                            entity.NpcClassId = OpeningJson.Int(targetData, "NpcClassId");
                        break;
                }
            }
            return entity;
        }
    }

    public sealed class OpeningRecordRound
    {
        public readonly List<OpeningRecordStep> Steps = new List<OpeningRecordStep>();

        public static OpeningRecordRound Parse(Dictionary<string, object> node)
        {
            var round = new OpeningRecordRound();
            foreach (object item in OpeningJson.Array(node["StepList"], "StepList"))
            {
                Dictionary<string, object> step = OpeningJson.Object(item, "step");
                round.Steps.Add(new OpeningRecordStep
                {
                    MessageType = OpeningJson.String(step, "MsgType"),
                    Time = (float)OpeningJson.Number(step, "Time"),
                    Data = OpeningJson.Object(step["Data"], "step data"),
                });
            }
            return round;
        }
    }

    public sealed class OpeningRecordStep
    {
        public string MessageType = string.Empty;
        public float Time;
        public Dictionary<string, object> Data;

        public string DataType
        {
            get
            {
                string type = OpeningJson.String(Data, "$type");
                int comma = type.IndexOf(',');
                string name = comma > 0 ? type.Substring(0, comma) : type;
                int dot = name.LastIndexOf('.');
                return dot >= 0 ? name.Substring(dot + 1) : name;
            }
        }
    }

    /// <summary>Loads and validates the shipped opening payload.</summary>
    public sealed class OpeningAssets
    {
        public const string Manifest = "854_156383";
        public const string Folder = "Opening";

        public OpeningStoryData Story { get; private set; }
        public OpeningStageData Stage { get; private set; }
        public OpeningRecord Record { get; private set; }
        public OpeningUiData Ui { get; private set; }
        public Dictionary<string, string> VoiceFiles { get; private set; }

        public static string Directory => Path.Combine(Application.streamingAssetsPath, Folder);

        public static OpeningAssets LoadDefault()
        {
            var assets = new OpeningAssets
            {
                Story = ParseStory(Read("opening-story.json")),
                Stage = ParseStage(Read("opening-stage.json")),
                Record = OpeningRecord.Parse(Read("opening-record.bytes")),
                Ui = OpeningUiData.Parse(Read("ui/opening-ui.json"), Path.Combine(Directory, "ui")),
                VoiceFiles = ParseVoices(Read("opening-voices.json")),
            };
            if (assets.Record.StageId != assets.Stage.StageId)
                throw new FormatException("Opening record stage differs from the official stage data.");
            return assets;
        }

        private static byte[] Read(string relative)
        {
            string path = Path.Combine(Directory, relative);
            if (!File.Exists(path)) throw new FileNotFoundException("Opening payload is missing: " + relative, path);
            return File.ReadAllBytes(path);
        }

        private static OpeningStoryData ParseStory(byte[] bytes)
        {
            Dictionary<string, object> root = OpeningJson.Object(OpeningJson.Parse(bytes), "story");
            var data = new OpeningStoryData { Manifest = OpeningJson.String(root, "manifest") };
            if (data.Manifest != Manifest) throw new FormatException("Opening story manifest is not pinned.");
            foreach (KeyValuePair<string, object> entry in OpeningJson.Object(root["graphs"], "graphs"))
            {
                Dictionary<string, object> raw = OpeningJson.Object(entry.Value, "graph");
                var graph = new OpeningStoryGraph
                {
                    Name = entry.Key,
                    Order = OpeningJson.Int(raw, "order"),
                    ShowSkipButton = OpeningJson.Bool(raw, "show_skip_button"),
                };
                foreach (object item in OpeningJson.Array(raw["nodes"], "nodes"))
                {
                    Dictionary<string, object> node = OpeningJson.Object(item, "node");
                    var converted = new OpeningStoryNode
                    {
                        Id = OpeningJson.Int(node, "id"),
                        Type = OpeningJson.String(node, "type"),
                        Name = OpeningJson.String(node, "name"),
                        Enter = OpeningJson.IntList(node, "enter"),
                        Exit = OpeningJson.IntList(node, "exit"),
                        Seconds = (float)OpeningJson.Number(node, "seconds"),
                        NeedMask = OpeningJson.Bool(node, "need_mask"),
                        DialogId = OpeningJson.Int(node, "dialog_id"),
                        DialogItems = OpeningJson.IntList(node, "dialog_items"),
                        AutoClose = OpeningJson.Bool(node, "auto_close"),
                        Immediately = OpeningJson.Bool(node, "immediately"),
                        KeepBlackScreen = OpeningJson.Bool(node, "keep_black"),
                        Instant = OpeningJson.Bool(node, "instant"),
                        LifeType = OpeningJson.Int(node, "life_type"),
                        AutoWait = (float)OpeningJson.Number(node, "auto_wait"),
                        AutoDura = (float)OpeningJson.Number(node, "auto_dura"),
                        AutoPlay = OpeningJson.Bool(node, "auto_play"),
                        WindowPath = OpeningJson.String(node, "window_path"),
                        Behaviour = OpeningJson.String(node, "behaviour"),
                        WaitPlayable = OpeningJson.Bool(node, "wait_playable"),
                        PlayableDura = (float)OpeningJson.Number(node, "playable_dura"),
                        HideMainUi = OpeningJson.Bool(node, "hide_main_ui"),
                        InStoryMode = OpeningJson.Bool(node, "in_story_mode"),
                    };
                    if (node.TryGetValue("playables", out object playables) && playables is List<object> list)
                        foreach (object value in list) converted.Playables.Add(value as string ?? string.Empty);
                    if (graph.Nodes.ContainsKey(converted.Id))
                        throw new FormatException("Opening story node id is duplicated: " + converted.Id);
                    graph.Nodes.Add(converted.Id, converted);
                }
                if (!data.Graphs.ContainsKey(entry.Key)) data.Graphs.Add(entry.Key, graph);
            }
            if (!data.Graphs.ContainsKey("BeforeFight") || !data.Graphs.ContainsKey("AfterFight")
                || !data.Graphs.ContainsKey("FirstStory"))
                throw new FormatException("Opening story payload is incomplete.");

            foreach (object item in OpeningJson.Array(root["dialogues"], "dialogues"))
            {
                Dictionary<string, object> line = OpeningJson.Object(item, "dialog line");
                data.Dialogues.Add(new OpeningDialogLine
                {
                    Story = OpeningJson.String(line, "story"),
                    Id = OpeningJson.Int(line, "id"),
                    Order = OpeningJson.Int(line, "order"),
                    Text = OpeningJson.String(line, "text"),
                    DialogType = OpeningJson.String(line, "dialog_type"),
                    Voice = OpeningJson.String(line, "voice"),
                    RoleId = OpeningJson.Int(line, "role_id"),
                    RoleFace = OpeningJson.String(line, "role_face"),
                });
            }
            foreach (KeyValuePair<string, object> entry in OpeningJson.Object(root["roles"], "roles"))
            {
                Dictionary<string, object> role = OpeningJson.Object(entry.Value, "role");
                int id = int.Parse(entry.Key, CultureInfo.InvariantCulture);
                data.Roles[id] = new OpeningRole
                {
                    Id = id,
                    Name = OpeningJson.String(role, "name"),
                    PortraitPath = OpeningJson.String(role, "portrait"),
                    PortraitPrefabKey = OpeningJson.String(role, "portrait_prefab"),
                    NeedFlipOnRight = OpeningJson.Bool(role, "need_flip_on_right"),
                    PortraitFaceOffset = ReadRoleVector(role, "portrait_face_offset"),
                };
                if (role.TryGetValue("portrait_sprites", out object rawSprites))
                    foreach (KeyValuePair<string, object> sprite in OpeningJson.Object(rawSprites, "portrait sprites"))
                        data.Roles[id].PortraitFaceSprites[sprite.Key] = sprite.Value as string ?? string.Empty;
                if (role.TryGetValue("portrait_face_offsets", out object rawOffsets))
                    foreach (KeyValuePair<string, object> offset in OpeningJson.Object(rawOffsets, "portrait face offsets"))
                    {
                        List<object> values = OpeningJson.Array(offset.Value, "portrait face offset");
                        if (values.Count < 2) throw new FormatException("Opening portrait face offset is incomplete.");
                        data.Roles[id].PortraitFaceOffsets[offset.Key] = new Vector2(
                            (float)OpeningJson.ToNumber(values[0]), (float)OpeningJson.ToNumber(values[1]));
                    }
            }
            return data;
        }

        private static Dictionary<string, string> ParseVoices(byte[] bytes)
        {
            Dictionary<string, object> root = OpeningJson.Object(OpeningJson.Parse(bytes), "opening voices");
            if (OpeningJson.String(root, "manifest") != Manifest)
                throw new FormatException("Opening voice payload manifest is not pinned.");
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (object item in OpeningJson.Array(root["voices"], "opening voices"))
            {
                Dictionary<string, object> voice = OpeningJson.Object(item, "opening voice");
                string eventName = OpeningJson.String(voice, "event");
                string file = OpeningJson.String(voice, "file");
                if (string.IsNullOrEmpty(eventName) || string.IsNullOrEmpty(file)
                    || !file.StartsWith("voices/", StringComparison.Ordinal)
                    || file.Contains("..") || file.IndexOf('\\') >= 0 || Path.IsPathRooted(file)
                    || result.ContainsKey(eventName))
                    throw new FormatException("Opening voice mapping is invalid: " + eventName);
                result.Add(eventName, file);
            }
            return result;
        }

        private static Vector2 ReadRoleVector(Dictionary<string, object> role, string key)
        {
            if (!role.TryGetValue(key, out object raw) || !(raw is List<object> values) || values.Count < 2)
                return Vector2.zero;
            return new Vector2((float)OpeningJson.ToNumber(values[0]), (float)OpeningJson.ToNumber(values[1]));
        }

        private static OpeningStageData ParseStage(byte[] bytes)
        {
            Dictionary<string, object> root = OpeningJson.Object(OpeningJson.Parse(bytes), "stage");
            var stage = new OpeningStageData
            {
                StageId = OpeningJson.Int(root, "stage_id"),
                GameId = OpeningJson.Int(root, "game_id"),
                Scene = OpeningJson.String(root, "scene"),
            };
            Dictionary<string, object> grid = OpeningJson.Object(root["grid"], "grid");
            List<object> origin = OpeningJson.Array(grid["origin"], "origin");
            stage.OriginX = (float)OpeningJson.ToNumber(origin[0]);
            stage.OriginY = (float)OpeningJson.ToNumber(origin[1]);
            List<object> size = OpeningJson.Array(grid["size"], "size");
            stage.SizeX = (int)OpeningJson.ToNumber(size[0]);
            stage.SizeY = (int)OpeningJson.ToNumber(size[1]);
            foreach (object item in OpeningJson.Array(grid["cells"], "cells"))
            {
                List<object> cell = OpeningJson.Array(item, "cell");
                stage.Cells.Add(new OpeningGridCell
                {
                    X = (int)OpeningJson.ToNumber(cell[0]),
                    Y = (int)OpeningJson.ToNumber(cell[1]),
                    Ground = (int)OpeningJson.ToNumber(cell[2]),
                    Height = (int)OpeningJson.ToNumber(cell[3]),
                });
            }
            foreach (KeyValuePair<string, object> entry in OpeningJson.Object(root["models"], "models"))
                stage.Models[int.Parse(entry.Key, CultureInfo.InvariantCulture)] = entry.Value as string ?? string.Empty;
            return stage;
        }
    }
}
