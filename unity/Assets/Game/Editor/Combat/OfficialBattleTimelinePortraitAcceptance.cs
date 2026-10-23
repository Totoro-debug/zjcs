using System;
using System.Collections;
using System.Linq.Expressions;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Presentation.Combat;

namespace Zjcs.Editor.Combat
{
    public static class OfficialBattleTimelinePortraitAcceptance
    {
        [MenuItem("Zjcs/Combat/Validate Timeline Portrait Binding")]
        public static void Run()
        {
            OfficialBattleHudCatalog hud = OfficialBattleHudCatalog.LoadDefault();
            OfficialBattleMonsterPortraitCatalog portraits = OfficialBattleMonsterPortraitCatalog.TryLoadDefault();
            if (portraits == null) throw new InvalidOperationException("Monster portrait catalog is missing.");

            Assembly combat = Assembly.Load("Zjcs.Combat.Official");
            Type checkpointType = combat.GetType("Zjcs.Combat.Official.FightBattleCheckpoint", true);
            Type unitType = combat.GetType("Zjcs.Combat.Official.FightBattleUnitState", true);
            Type entryType = combat.GetType("Zjcs.Combat.Official.FightTurnEntry", true);
            var playerTexture = new Texture2D(4, 4);
            Sprite playerFallback = Sprite.Create(playerTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));
            var canvas = new GameObject("BattleTimelinePortraitAcceptance", typeof(RectTransform), typeof(Canvas));
            try
            {
                var view = new OfficialBattleHudView(hud);
                view.Mount(canvas.transform);
                object checkpoint = Activator.CreateInstance(checkpointType);
                AddUnit(checkpoint, checkpointType, unitType, 1, 0, 1);
                AddUnit(checkpoint, checkpointType, unitType, 2, 3206, 2);

                object timeline = checkpointType.GetField("Timeline").GetValue(checkpoint);
                IList entries = (IList)timeline.GetType().GetField("Entries").GetValue(timeline);
                entries.Add(CreateEntry(entryType, 1, 0));
                entries.Add(CreateEntry(entryType, 2, 1));

                Type resolverType = typeof(Func<,>).MakeGenericType(unitType, typeof(Sprite));
                Func<object, Sprite> resolve = unit =>
                {
                    int monsterId = Convert.ToInt32(unitType.GetField("MonsterId").GetValue(unit));
                    return monsterId > 0 ? portraits.Find(monsterId) : playerFallback;
                };
                ParameterExpression argument = Expression.Parameter(unitType, "unit");
                Delegate portraitResolver = Expression.Lambda(resolverType,
                    Expression.Invoke(Expression.Constant(resolve), Expression.Convert(argument, typeof(object))), argument).Compile();
                typeof(OfficialBattleHudView).GetMethod("RefreshTimeline", BindingFlags.Instance | BindingFlags.Public)
                    .Invoke(view, new object[] { checkpoint, portraitResolver, null, false });

                Transform[] nodes = canvas.GetComponentsInChildren<Transform>(true);
                Transform player = Find(nodes, "GameRoundCharacterItem_1");
                Transform monster = Find(nodes, "GameRoundCharacterItem_2");
                RawImage playerImage = RequireRawImage(player, "IconGo/PlayerHeadETC2Go/HeadImg", "player head");
                if (playerImage.texture != playerTexture)
                    throw new InvalidOperationException("Player RawImage did not bind the fallback portrait texture.");
                Image monsterImage = RequireImage(monster, "IconGo/MonsterIconImg", "monster head");
                if (monsterImage.sprite == null || monsterImage.sprite.name != "G_3206")
                    throw new InvalidOperationException("Monster IconId 3206 did not bind G_3206.");

                Debug.Log("Battle timeline portrait binding passed: player node and G_3206 monster Sprite.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(canvas);
                UnityEngine.Object.DestroyImmediate(playerFallback);
                UnityEngine.Object.DestroyImmediate(playerTexture);
            }
        }

        private static void AddUnit(object checkpoint, Type checkpointType, Type unitType, int id, int monsterId, int side)
        {
            object unit = Activator.CreateInstance(unitType);
            Set(unit, unitType, "Id", id);
            Set(unit, unitType, "MonsterId", monsterId);
            Set(unit, unitType, "Side", side);
            Set(unit, unitType, "Hp", 1L);
            Set(unit, unitType, "MaxHp", 1L);
            object turn = unitType.GetField("Turn").GetValue(unit);
            Set(turn, turn.GetType(), "Id", id);
            Set(turn, turn.GetType(), "Hp", 1L);
            Set(turn, turn.GetType(), "Speed", 100);
            ((IList)checkpointType.GetField("Units").GetValue(checkpoint)).Add(unit);
        }

        private static object CreateEntry(Type entryType, int id, long next)
        {
            object entry = Activator.CreateInstance(entryType);
            Set(entry, entryType, "Id", id);
            Set(entry, entryType, "Next", next);
            return entry;
        }

        private static void Set(object target, Type type, string name, object value)
        {
            type.GetField(name).SetValue(target, value);
        }

        private static Transform Find(Transform[] nodes, string name)
        {
            foreach (Transform node in nodes)
                if (node.name == name) return node;
            return null;
        }

        private static RawImage RequireRawImage(Transform root, string path, string description)
        {
            Transform node = root == null ? null : root.Find(path);
            RawImage image = node == null ? null : node.GetComponent<RawImage>();
            if (image == null)
                throw new InvalidOperationException("Battle timeline " + description + " RawImage is missing: " + path);
            return image;
        }

        private static Image RequireImage(Transform root, string path, string description)
        {
            Transform node = root == null ? null : root.Find(path);
            Image image = node == null ? null : node.GetComponent<Image>();
            if (image == null)
                throw new InvalidOperationException("Battle timeline " + description + " node is missing: " + path);
            return image;
        }
    }
}
