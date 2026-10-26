using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Combat.Official;
using Zjcs.Presentation.Combat;

namespace Zjcs.Presentation.Opening
{
    /// <summary>Bindings from GamePlayManagerConfig, native fight UI and bitmap damage prefabs.</summary>
    public sealed class OpeningBattlePresentation : MonoBehaviour
    {
        private sealed class Unit
        {
            public FightBattleUnitState State;
            public OfficialBattleUnitWorldHudView View;
        }
        private readonly Dictionary<int, Unit> units = new Dictionary<int, Unit>();
        private readonly Dictionary<int, Dictionary<string, object>> heads = new Dictionary<int, Dictionary<string, object>>();
        private readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
        private readonly Dictionary<string, List<object>> textBindings = new Dictionary<string, List<object>>();
        private readonly Dictionary<string, List<object>> imageBindings = new Dictionary<string, List<object>>();
        private readonly List<OpeningDamageFloat> damage = new List<OpeningDamageFloat>();
        private List<object> factions;
        private Dictionary<string, object> colors;
        private OfficialBattleHudCatalog catalog;
        private Camera battleCamera;
        public string TimelinePath { get; private set; }
        public float TimelineMinDist { get; private set; }
        public float TimelineDuration { get; private set; }

        public void Initialize(Camera stageCamera)
        {
            battleCamera = stageCamera;
            catalog = OfficialBattleHudCatalog.LoadDefault();
            var ui = Read("unit-ui.json");
            factions = OpeningJson.Array(ui["factions"], "factions");
            foreach (object entry in OpeningJson.Array(ui["heads"], "heads"))
            {
                var head = OpeningJson.Object(entry, "head");
                heads.Add(OpeningJson.Int(head, "classId"), head);
            }
            var timeline = OpeningJson.Object(ui["timeline"], "timeline");
            TimelinePath = OpeningJson.String(timeline, "IconSlot");
            TimelineMinDist = (float)OpeningJson.Number(timeline, "minDist");
            TimelineDuration = (float)OpeningJson.Number(timeline, "moveDuration");
            var damageUi = Read("damage-ui.json");
            colors = OpeningJson.Object(damageUi["colors"], "damage colors");
            foreach (object entry in OpeningJson.Array(damageUi["prefabs"], "damage prefabs"))
            {
                var prefab = OpeningJson.Object(entry, "damage prefab");
                textBindings.Add(OpeningJson.String(prefab, "key"), OpeningJson.Array(prefab["texts"], "texts"));
                imageBindings.Add(OpeningJson.String(prefab, "key"), OpeningJson.Array(prefab["images"], "images"));
            }
        }

        public void AddUnit(OpeningRecordEntity entity, Transform root)
        {
            if (!catalog.TryGetUnitBar(entity.ClassId, out BattleUnitBarBinding binding))
                throw new InvalidOperationException("Opening unit HP prefab was not migrated: " + entity.ClassId);
            var faction = Faction(entity.Faction);
            var state = new FightBattleUnitState { Id=entity.EntityId, Side=entity.Faction, Hp=entity.Hp,
                MaxHp=entity.MaxHp, LastMaxHp=entity.MaxHp, MonsterId=entity.MonsterId };
            var view = OfficialBattleUnitWorldHudView.Create(catalog, binding,
                new BattleUnitBarFaction { hpColor=Floats(faction["hpColor"]), hpAddColor=Floats(faction["hpAddColor"]),
                    hpSubColor=Floats(faction["hpSubColor"]) }, state, root, battleCamera, 32000);
            view.Root.name = "HpBarGame";
            units.Add(entity.EntityId, new Unit { State=state, View=view });
            if (entity.NpcClassId <= 0) return;
            var head = heads[entity.NpcClassId];
            var layout = OpeningJson.Object(head["nameLayout"], "NPC name layout");
            var go = new GameObject("Name", typeof(RectTransform));
            go.transform.SetParent(root, false);
            go.transform.localPosition = Vector(layout["position"]);
            go.transform.localScale = Vector(layout["scale"]);
            var rect = (RectTransform)go.transform;
            var size = Floats(layout["size"]);
            rect.sizeDelta = new Vector2(size[0],size[1]);
            var text = go.AddComponent<TextMeshPro>();
            text.font = catalog.Font;
            text.text = OpeningJson.String(head, "name");
            text.fontSize = (float)OpeningJson.Number(layout, "font_size");
            text.alignment = (TextAlignmentOptions)OpeningJson.Int(layout, "alignment");
            text.color = ColorValue(layout["color"]);
            text.enableWordWrapping = OpeningJson.Bool(layout, "word_wrap");
            text.overflowMode = (TextOverflowModes)OpeningJson.Int(layout, "overflow");
            text.sortingOrder = 32001;
        }

        public void ApplyDamage(int id, long signedValue)
        {
            if (!units.TryGetValue(id, out Unit unit)) return;
            // FightDamageComponentImplement.CalcDamageValue: damage is negative, cure positive.
            unit.State.Hp = Math.Max(0, Math.Min(unit.State.MaxHp, unit.State.Hp + signedValue));
            unit.View.Refresh(unit.State);
        }

        public void RemoveUnit(int id)
        {
            if (!units.TryGetValue(id, out Unit unit)) return;
            unit.View.Dispose();
            units.Remove(id);
        }

        public void BindPortrait(GameObject item, OpeningRecordEntity entity)
        {
            Transform root = item.transform;
            int classId = entity.NpcClassId > 0 ? entity.NpcClassId : entity.MonsterId;
            Sprite icon = SpriteFile("UnitUI/"+OpeningJson.String(heads[classId], "file"));
            var faction = Faction(entity.Faction);
            SetImage(root, "FactionImg", SpriteFile("UnitUI/"+OpeningJson.String(faction, "roundBackground")));
            Transform iconRoot = root.Find("IconGo");
            bool npc = entity.NpcClassId > 0;
            root.Find("WeGo").gameObject.SetActive(false);
            iconRoot.Find("PlayerHeadETC2Go").gameObject.SetActive(false);
            iconRoot.Find("PlayerMaskImg").gameObject.SetActive(false);
            iconRoot.Find("MonsterIconImg").gameObject.SetActive(!npc);
            iconRoot.Find("NpcIconGO").gameObject.SetActive(npc);
            if (npc) SetImage(iconRoot, "NpcIconGO/NpcIconImg", icon);
            else { SetImage(iconRoot, "MonsterIconImg", icon); SetImage(iconRoot,"MonsterIconImg/MonsterHurtImg",icon); }
        }

        public void ShowDamage(RectTransform layer, Vector3 anchor, int targetId, long value,
            string element, string resultType, bool critical, bool blocked, bool immunity)
        {
            bool cure = resultType == "Cure";
            bool abyss = element == "Abyss" && !cure;
            string name = cure ? "BattleCure" : abyss ? critical ? "AbyssCritDamage" : blocked ? "AbyssBlockDamage" : "AbyssDamage"
                : critical ? "BattleCritDamage" : blocked ? "BattleBlockDamage" : "BattleDamage";
            string key = "damage_" + name + "FloatEffect_Combine";
            BattleHudPrefab prefab = catalog.FindPrefab(key);
            var renderer = new OfficialBattleHudRenderer(catalog);
            var instance = renderer.Create(key, layer);
            foreach(object entry in imageBindings[key])
            {
                var binding=OpeningJson.Object(entry,"damage image");
                Transform node=FindPath(instance.transform,OpeningJson.String(binding,"path"));
                Image image=OpeningJson.Bool(binding,"sequence")
                    ? (Image)node.gameObject.AddComponent<OpeningDamageSequenceImage>() : node.gameObject.AddComponent<Image>();
                image.sprite=SpriteFile("DamageUI/"+OpeningJson.String(binding,"file"));
                image.material=Resources.Load<Material>("OpeningDamage/DamageNumber");
                image.color=ColorValue(binding["color"]);
                image.type=(Image.Type)OpeningJson.Int(binding,"type");
                image.preserveAspect=OpeningJson.Bool(binding,"preserveAspect");
                image.raycastTarget=OpeningJson.Bool(binding,"raycastTarget");
                image.maskable=OpeningJson.Bool(binding,"maskable");
                if(image is OpeningDamageSequenceImage sequence)
                    sequence.Initialize(OpeningJson.Int(binding,"rows"),OpeningJson.Int(binding,"columns"),
                        (float)OpeningJson.Number(binding,"rate"));
            }
            foreach (object entry in textBindings[key])
            {
                var binding = OpeningJson.Object(entry, "legacy Text");
                Transform node = FindPath(instance.transform, OpeningJson.String(binding, "path"));
                var text = node.gameObject.AddComponent<Text>();
                text.font = Resources.Load<Font>("OpeningDamage/" + OpeningJson.String(binding, "font"));
                if (text.font == null) throw new InvalidOperationException("Official bitmap damage font is absent.");
                text.fontSize = OpeningJson.Int(binding, "fontSize");
                text.fontStyle = (FontStyle)OpeningJson.Int(binding, "style");
                text.alignment = (TextAnchor)OpeningJson.Int(binding, "alignment");
                text.alignByGeometry = OpeningJson.Bool(binding, "alignByGeometry");
                text.supportRichText = OpeningJson.Bool(binding, "richText");
                text.horizontalOverflow = (HorizontalWrapMode)OpeningJson.Int(binding, "horizontalOverflow");
                text.verticalOverflow = (VerticalWrapMode)OpeningJson.Int(binding, "verticalOverflow");
                text.lineSpacing = (float)OpeningJson.Number(binding, "lineSpacing");
                text.resizeTextForBestFit = OpeningJson.Bool(binding, "bestFit");
                text.resizeTextMinSize = OpeningJson.Int(binding, "minSize");
                text.resizeTextMaxSize = OpeningJson.Int(binding, "maxSize");
                text.raycastTarget = false;
                text.color = ColorValue(binding["color"]);
                text.text = OpeningFightPlayback.FormatBattleNumber(Math.Abs(value));
            }
            Dictionary<string, object> tint = DamageColor(element, cure, critical, blocked, immunity);
            Color textColor = abyss ? Color.white : NativeColor(tint, "TextColor").linear;
            foreach (Text text in instance.GetComponentsInChildren<Text>(true))
            {
                Color c=textColor; c.a=text.color.a; text.color=c;
            }
            foreach (TMP_Text text in instance.GetComponentsInChildren<TMP_Text>(true)) text.color=textColor;
            foreach (Image image in instance.GetComponentsInChildren<Image>(true))
            {
                if (image.name == "BgImg") { image.color=NativeColor(tint,"BgImageColor"); image.gameObject.SetActive(true); }
                if (image.name == "Add") { Color c=NativeColor(tint,"AddImageColor"); c.a=image.color.a; image.color=c; image.gameObject.SetActive(true); }
                if (image.name == "DamageTypeImage") image.color=NativeColor(tint,"IconImageColor");
            }
            foreach (string hidden in new[] { prefab.damageUi.trueDamagePath, prefab.damageUi.breakResiliencePath })
            {
                Transform node=FindPath(instance.transform,hidden);
                if (node != null) node.gameObject.SetActive(false);
            }
            foreach(Transform node in instance.GetComponentsInChildren<Transform>(true))
                if(node.name=="ElementConfrontText")
                {
                    // ElementConfrontationUI.Reset hides its content. The parent is
                    // enabled again by the native float clip after two frames.
                    foreach(Graphic graphic in node.GetComponentsInChildren<Graphic>(true))
                        graphic.enabled=false;
                    node.Find("Bg")?.gameObject.SetActive(false);
                    node.Find("Text")?.gameObject.SetActive(false);
                }
            var animations = renderer.InitializeAnimations(key, instance, useManualTick:true);
            bool played=false;
            foreach (BattleHudNode node in prefab.nodes)
            {
                if (node.animations == null) continue;
                foreach (BattleHudAnimation animation in node.animations)
                    if (animation.name.StartsWith("GameCanvas_FlyText_",StringComparison.Ordinal))
                        played |= animations.Play(animation.owner,animation.name);
            }
            if (!played) throw new InvalidOperationException("Official damage float animation is missing: "+key);
            instance.SetActive(true);
            var floating=instance.AddComponent<OpeningDamageFloat>();
            floating.Initialize(animations, prefab.damageUi.showTime, layer, battleCamera, anchor, targetId);
            damage.Add(floating);
            Vector3 offset=Vector3.zero;
            Vector3 spacing=NativeVector(colors,"DamageUISpacing");
            for (int i=damage.Count-1;i>=0;i--)
                if (damage[i] != null && damage[i].TargetId == targetId)
                { damage[i].StackOffset=offset; offset+=spacing; }
        }

        private Dictionary<string, object> DamageColor(string element, bool cure, bool crit, bool block, bool immunity)
        {
            if (cure) return OpeningJson.Object(colors["ElementCureColor"],"cure");
            int type=element=="Wind"?100:element=="Water"?101:element=="Fire"?102:element=="Light"?103:element=="Dark"?104:element=="Abyss"?200:0;
            foreach (object item in OpeningJson.Array(colors["ElementDamageColors"],"elements"))
            {
                var row=OpeningJson.Object(item,"element");
                if (OpeningJson.Int(row,"ElementType") == type)
                    return OpeningJson.Object(row[immunity?"Immunity":crit?"Crit":block?"Block":"Normal"],"damage tint");
            }
            return OpeningJson.Object(colors["DefaultColor"],"default tint");
        }
        private void Update()
        {
            foreach (Unit unit in units.Values) unit.View.Tick(Time.deltaTime);
            damage.RemoveAll(item=>item==null);
        }
        private void OnDestroy()
        {
            foreach (Unit unit in units.Values) unit.View.Dispose();
            foreach (Sprite sprite in sprites.Values) { Destroy(sprite.texture); Destroy(sprite); }
        }
        private Sprite SpriteFile(string file)
        {
            if (sprites.TryGetValue(file,out Sprite result)) return result;
            var texture=new Texture2D(2,2,TextureFormat.RGBA32,false);
            if (!texture.LoadImage(File.ReadAllBytes(Path.Combine(OpeningAssets.Directory,file))))
                throw new InvalidDataException("Opening portrait failed: "+file);
            result=Sprite.Create(texture,new Rect(0,0,texture.width,texture.height),new Vector2(.5f,.5f),100);
            sprites.Add(file,result); return result;
        }
        private static void SetImage(Transform root,string path,Sprite sprite)
        {
            Transform node=root.Find(path);
            if (node==null) throw new InvalidDataException("Official portrait node is absent: "+path);
            // GridSpriteImage receives its sprite dynamically in RefreshBg; its empty
            // serialized binding does not create an Image in the general HUD renderer.
            var image=node.GetComponent<Image>() ?? node.gameObject.AddComponent<Image>();
            image.sprite=sprite; image.enabled=true; image.raycastTarget=false;
        }
        private Dictionary<string,object> Faction(int id)
        {
            // game_902 FactionInfoList/FactionForColorIndex: friendly 0, monsters 127.
            if (id != 0 && id != 127) throw new InvalidDataException("Unexpected opening faction: "+id);
            return OpeningJson.Object(factions[id == 0 ? 0 : 1], "faction");
        }
        private static Dictionary<string,object> Read(string file)
        {
            var data=OpeningJson.Object(OpeningJson.Parse(File.ReadAllBytes(Path.Combine(OpeningAssets.Directory,file))),file);
            if (OpeningJson.String(data,"manifest") != OpeningAssets.Manifest) throw new InvalidDataException("Unpinned opening UI: "+file);
            return data;
        }
        private static Transform FindPath(Transform root,string path)
        { if (string.IsNullOrEmpty(path)) return null; int slash=path.IndexOf('/',1); return slash<0?root:root.Find(path.Substring(slash+1)); }
        private static float[] Floats(object value)
        { var source=OpeningJson.Array(value,"numbers"); var values=new float[source.Count]; for(int i=0;i<values.Length;i++)values[i]=(float)OpeningJson.ToNumber(source[i]); return values; }
        private static Vector3 Vector(object value) { float[] v=Floats(value); return new Vector3(v[0],v[1],v.Length>2?v[2]:0); }
        private static Color ColorValue(object value) { float[] c=Floats(value); return new Color(c[0],c[1],c[2],c[3]); }
        private static Color NativeColor(Dictionary<string,object> parent,string key)
        { var v=OpeningJson.Object(parent[key],key); return new Color((float)OpeningJson.Number(v,"r"),(float)OpeningJson.Number(v,"g"),(float)OpeningJson.Number(v,"b"),(float)OpeningJson.Number(v,"a")); }
        private static Vector3 NativeVector(Dictionary<string,object> parent,string key)
        { var v=OpeningJson.Object(parent[key],key); return new Vector3((float)OpeningJson.Number(v,"x"),(float)OpeningJson.Number(v,"y"),(float)OpeningJson.Number(v,"z")); }
    }
}
