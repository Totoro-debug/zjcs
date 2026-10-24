using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zjcs.Offline.Progression;

namespace Zjcs.Presentation.Home
{
    [Serializable]
    public sealed class HomeFruitTreeTutorialLine
    {
        public int role_id;
        public string text;
        public string voice_event_key;
        public string voice_file;
    }

    [Serializable]
    public sealed class HomeFruitTreeTutorialDialogue
    {
        public int id;
        public HomeFruitTreeTutorialLine[] lines;
    }

    [Serializable]
    public sealed class HomeFruitTreeTutorialStory
    {
        public string id;
        public HomeFruitTreeTutorialDialogue[] dialogues;
    }

    [Serializable]
    internal sealed class HomeFruitTreeTutorialVoice
    {
        public string event_key;
        public string file;
    }

    [Serializable]
    internal sealed class HomeFruitTreeTutorialData
    {
        public string manifest;
        public HomeFruitTreeTutorialStory[] stories;
        public HomeFruitTreeTutorialVoice[] voices;
    }

    public static class HomeFruitTreeTutorialCatalog
    {
        private static HomeFruitTreeTutorialData data;

        public static HomeFruitTreeTutorialLine[] Dialogue(string storyId, int dialogueId)
        {
            if (data == null)
            {
                string path = Path.Combine(Application.streamingAssetsPath, "Home/fruit-tree-tutorials.json");
                if (!File.Exists(path)) throw new FileNotFoundException("Official fruit tree tutorials are missing.", path);
                data = JsonUtility.FromJson<HomeFruitTreeTutorialData>(File.ReadAllText(path));
            }
            if (data == null || data.manifest != "854_156383" || data.stories == null)
                throw new FormatException("Official fruit tree tutorial data is incomplete or unsupported.");
            HomeFruitTreeTutorialStory story = data.stories.FirstOrDefault(value => value.id == storyId);
            HomeFruitTreeTutorialDialogue dialogue = story?.dialogues?.FirstOrDefault(value => value.id == dialogueId);
            if (dialogue?.lines == null || dialogue.lines.Length == 0)
                throw new FormatException("The official fruit tree tutorial dialogue is missing: " + storyId + "/" + dialogueId);
            foreach (HomeFruitTreeTutorialLine line in dialogue.lines)
                if (line == null || string.IsNullOrWhiteSpace(line.text)
                    || string.IsNullOrWhiteSpace(line.voice_event_key) || string.IsNullOrWhiteSpace(line.voice_file))
                    throw new FormatException("The official fruit tree tutorial line is incomplete: " + storyId + "/" + dialogueId);
            return dialogue.lines;
        }

        public static string VoiceFile(string eventKey)
        {
            if (data == null) Dialogue("FruitTreeUnlock1", 0);
            HomeFruitTreeTutorialVoice[] matches = data.voices?.Where(value => value != null
                && value.event_key == eventKey).ToArray();
            if (matches == null || matches.Length != 1 || string.IsNullOrWhiteSpace(matches[0].file)
                || Path.IsPathRooted(matches[0].file) || matches[0].file.Contains(".."))
                throw new FormatException("The official fruit tree tutorial voice is missing or invalid: " + eventKey);
            return matches[0].file;
        }
    }

    public sealed partial class HomeHudView
    {
        private const string FruitTreeTutorialPortrait = "npc_portrait_8402-80389f7e01.png";
        private const string FruitTreeTutorialTextPath =
            "/TalkDialog/Content/CharacterAndTalkContent/TalkContent/TalkText";
        private const string FruitTreeTutorialNamePath =
            "/TalkDialog/Content/CharacterAndTalkContent/TalkContent/NpcName/NpcNameText";
        private const string FruitTreeTutorialPortraitPath =
            "/TalkDialog/Content/CharacterAndTalkContent/CharacterGroup/RightCharacter/Mask/Locator/PortraitParent";

        private GameObject fruitTreeTutorialLayer;
        private HomeUiRenderer fruitTreeTutorialRenderer;
        private HomeFruitTreeTutorialLine[] fruitTreeTutorialLines;
        private Action fruitTreeTutorialCompleted;
        private int fruitTreeTutorialLineIndex;
        private GameObject fruitTreeTutorialMaskObject;
        private HomeFruitTreeTutorialMaskGraphic fruitTreeTutorialMask;

        public void ShowFruitTreeTutorial(string storyId, int dialogueId, Action completed)
        {
            ClearFruitTreeTutorial();
            fruitTreeTutorialLines = HomeFruitTreeTutorialCatalog.Dialogue(storyId, dialogueId);
            fruitTreeTutorialLineIndex = 0;
            fruitTreeTutorialCompleted = completed;
            fruitTreeTutorialRenderer = CreatePetLayer("talk_talk", out fruitTreeTutorialLayer,
                ContinueFruitTreeTutorial);

            GameObject portraitParent = fruitTreeTutorialRenderer.Find(FruitTreeTutorialPortraitPath);
            Sprite portrait = catalog.FindSprite(FruitTreeTutorialPortrait);
            if (portraitParent == null || portrait == null)
                throw new InvalidOperationException("The official fruit tree tutorial portrait is unavailable.");
            var portraitObject = new GameObject("OfficialGuidePortrait", typeof(RectTransform), typeof(Image));
            portraitObject.transform.SetParent(portraitParent.transform, false);
            RectTransform portraitRect = (RectTransform)portraitObject.transform;
            portraitRect.anchorMin = Vector2.zero;
            portraitRect.anchorMax = Vector2.one;
            portraitRect.offsetMin = Vector2.zero;
            portraitRect.offsetMax = Vector2.zero;
            Image portraitImage = portraitObject.GetComponent<Image>();
            portraitImage.sprite = portrait;
            portraitImage.preserveAspect = true;
            portraitImage.raycastTarget = false;

            Button next = fruitTreeTutorialRenderer.Find<Button>("/TalkDialog/NextBtn");
            if (next == null) throw new InvalidOperationException("The official fruit tree tutorial next button is missing.");
            next.onClick.AddListener(ContinueFruitTreeTutorial);
            SetFruitTreeTutorialLine();
        }

        public void ShowFruitTreeFurnitureMask(int furnitureType)
        {
            if (host.Scene == null || !host.Scene.TryGetFurnitureClickBounds(furnitureType, out Rect bounds))
                throw new InvalidOperationException("The official fruit tree furniture click area is unavailable.");
            Vector2 scale = furnitureType == OfflineFruitTreeService.PetFurnitureType
                ? new Vector2(0.8f, 0.8f) : new Vector2(1f, 0.8f);
            Vector2 offset = furnitureType == OfflineFruitTreeService.PetFurnitureType
                ? new Vector2(0f, -1.5f) : Vector2.zero;
            bounds.position += offset;
            bounds.size = Vector2.Scale(bounds.size, scale);
            ShowFruitTreeWorldMask(bounds);
        }

        public void ShowFruitTreeCollectionMask()
        {
            if (host.Scene == null
                || !host.Scene.TryGetFurnitureClickBounds(OfflineFruitTreeService.ProductionFurnitureType,
                    out Rect bounds))
                throw new InvalidOperationException("The official fruit tree click area is unavailable.");
            ShowFruitTreeWorldMask(bounds);
        }

        public void ShowFruitTreeRepairButtonMask()
        {
            Button button = activeDialogRenderer?.Find<Button>(
                "/FurnitureRepairDialog/BgDialog3/BgImg/Mask/RepaireBtn");
            if (button == null) button = activeDialogRenderer?.Find<Button>("RepaireBtn");
            RectTransform buttonRect = button == null ? null : button.transform as RectTransform;
            if (buttonRect == null)
                throw new InvalidOperationException("The official furniture repair button is unavailable.");

            var corners = new Vector3[4];
            buttonRect.GetWorldCorners(corners);
            Camera eventCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
            Vector2 max = new Vector2(float.MinValue, float.MinValue);
            foreach (Vector3 corner in corners)
            {
                Vector2 screen = RectTransformUtility.WorldToScreenPoint(eventCamera, corner);
                min = Vector2.Min(min, screen);
                max = Vector2.Max(max, screen);
            }
            ShowFruitTreeScreenMask(Rect.MinMaxRect(min.x, min.y, max.x, max.y));
        }

        public void HideFruitTreeTutorialMask()
        {
            if (fruitTreeTutorialMask != null) fruitTreeTutorialMask.Hide();
        }

        private void ShowFruitTreeWorldMask(Rect worldBounds)
        {
            Camera camera = host.Scene.Camera;
            Vector3[] corners =
            {
                new Vector3(worldBounds.xMin, worldBounds.yMin, 0f),
                new Vector3(worldBounds.xMin, worldBounds.yMax, 0f),
                new Vector3(worldBounds.xMax, worldBounds.yMax, 0f),
                new Vector3(worldBounds.xMax, worldBounds.yMin, 0f)
            };
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
            Vector2 max = new Vector2(float.MinValue, float.MinValue);
            foreach (Vector3 corner in corners)
            {
                Vector3 screen = camera.WorldToScreenPoint(corner);
                min = Vector2.Min(min, screen);
                max = Vector2.Max(max, screen);
            }
            ShowFruitTreeScreenMask(Rect.MinMaxRect(min.x, min.y, max.x, max.y));
        }

        private void ShowFruitTreeScreenMask(Rect screenBounds)
        {
            if (canvas == null) throw new InvalidOperationException("The home UI canvas is unavailable.");
            if (fruitTreeTutorialMask == null)
            {
                fruitTreeTutorialMaskObject = new GameObject("FruitTreeTutorialMask", typeof(RectTransform),
                    typeof(HomeFruitTreeTutorialMaskGraphic));
                fruitTreeTutorialMaskObject.transform.SetParent(canvas.transform, false);
                RectTransform rect = (RectTransform)fruitTreeTutorialMaskObject.transform;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                fruitTreeTutorialMask = fruitTreeTutorialMaskObject.GetComponent<HomeFruitTreeTutorialMaskGraphic>();
            }
            fruitTreeTutorialMaskObject.transform.SetAsLastSibling();
            Camera eventCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            fruitTreeTutorialMask.Show(screenBounds, eventCamera);
        }

        private void ContinueFruitTreeTutorial()
        {
            if (fruitTreeTutorialRenderer == null || fruitTreeTutorialLines == null) return;
            fruitTreeTutorialLineIndex++;
            if (fruitTreeTutorialLineIndex < fruitTreeTutorialLines.Length)
            {
                SetFruitTreeTutorialLine();
                return;
            }

            Action completed = fruitTreeTutorialCompleted;
            ClearFruitTreeTutorial();
            completed?.Invoke();
        }

        private void SetFruitTreeTutorialLine()
        {
            HomeFruitTreeTutorialLine line = fruitTreeTutorialLines[fruitTreeTutorialLineIndex];
            if (line.role_id != 840200)
                throw new InvalidOperationException("The official fruit tree tutorial has an unsupported speaker.");
            fruitTreeTutorialRenderer.SetText(FruitTreeTutorialTextPath, line.text);
            TMP_Text name = fruitTreeTutorialRenderer.Find<TMP_Text>(FruitTreeTutorialNamePath);
            if (name != null) name.text = string.Empty;
            host.PlayFruitTreeTutorialVoice(line.voice_event_key);
        }

        private void ClearFruitTreeTutorial()
        {
            host.StopFruitTreeTutorialVoice();
            ClosePetLayer(ref fruitTreeTutorialLayer);
            HideFruitTreeTutorialMask();
            fruitTreeTutorialRenderer = null;
            fruitTreeTutorialLines = null;
            fruitTreeTutorialCompleted = null;
            fruitTreeTutorialLineIndex = 0;
        }
    }

    public sealed class HomeFruitTreeTutorialMaskGraphic : MaskableGraphic, ICanvasRaycastFilter
    {
        private const float CornerRadius = 23f;
        private const int CornerSegments = 10;
        private static readonly Color MaskColor = new Color(0f, 0f, 0f, 199f / 255f);
        private Vector2 targetMin;
        private Vector2 targetMax;
        private bool targetVisible;

        public void Show(Rect screenBounds, Camera eventCamera)
        {
            if (screenBounds.width <= 0f || screenBounds.height <= 0f)
                throw new ArgumentOutOfRangeException(nameof(screenBounds));
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
            Vector2 max = new Vector2(float.MinValue, float.MinValue);
            Vector2[] corners =
            {
                new Vector2(screenBounds.xMin, screenBounds.yMin),
                new Vector2(screenBounds.xMin, screenBounds.yMax),
                new Vector2(screenBounds.xMax, screenBounds.yMax),
                new Vector2(screenBounds.xMax, screenBounds.yMin)
            };
            foreach (Vector2 corner in corners)
            {
                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, corner,
                    eventCamera, out Vector2 local))
                    throw new InvalidOperationException("The fruit tree tutorial target cannot be projected to its canvas.");
                min = Vector2.Min(min, local);
                max = Vector2.Max(max, local);
            }

            Rect outer = rectTransform.rect;
            min = Vector2.Max(min, outer.min);
            max = Vector2.Min(max, outer.max);
            if (max.x <= min.x || max.y <= min.y)
                throw new InvalidOperationException("The fruit tree tutorial target is outside the visible home canvas.");
            targetMin = min;
            targetMax = max;
            targetVisible = true;
            color = MaskColor;
            raycastTarget = true;
            if (!gameObject.activeSelf) gameObject.SetActive(true);
            SetVerticesDirty();
        }

        public void Hide()
        {
            targetVisible = false;
            raycastTarget = false;
            if (gameObject.activeSelf) gameObject.SetActive(false);
        }

        public void ShowFull()
        {
            targetMin = Vector2.zero;
            targetMax = Vector2.zero;
            targetVisible = true;
            raycastTarget = true;
            color = MaskColor;
            if (!gameObject.activeSelf) gameObject.SetActive(true);
            SetVerticesDirty();
        }

        public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
        {
            if (!targetVisible || !RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform,
                screenPoint, eventCamera, out Vector2 local)) return true;
            return !IsInsideTarget(local);
        }

        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear();
            if (!targetVisible) return;

            Rect outer = rectTransform.rect;
            Vector2 outerMin = outer.min;
            Vector2 outerMax = outer.max;
            Vector2 bottomLeft = targetMin;
            Vector2 topRight = targetMax;
            Vector2 topLeft = new Vector2(bottomLeft.x, topRight.y);
            Vector2 bottomRight = new Vector2(topRight.x, bottomLeft.y);
            AddTriangle(helper, outerMin, new Vector2(outerMin.x, outerMax.y), bottomLeft);
            AddTriangle(helper, new Vector2(outerMin.x, outerMax.y), bottomLeft, topLeft);
            AddTriangle(helper, new Vector2(outerMin.x, outerMax.y), topLeft, new Vector2(outerMax.x, outerMax.y));
            AddTriangle(helper, new Vector2(outerMax.x, outerMax.y), topLeft, topRight);
            AddTriangle(helper, new Vector2(outerMax.x, outerMax.y), topRight, outerMax);
            AddTriangle(helper, topRight, outerMax, bottomRight);
            AddTriangle(helper, bottomLeft, bottomRight, outerMax);
            AddTriangle(helper, outerMin, bottomLeft, outerMax);

            float radius = Mathf.Min(CornerRadius, Mathf.Min(targetMax.x - targetMin.x,
                targetMax.y - targetMin.y) * 0.5f);
            Vector2[] cornerPositions = { bottomLeft, topLeft, topRight, bottomRight };
            Vector2[] centers =
            {
                bottomLeft + new Vector2(radius, radius),
                topLeft + new Vector2(radius, -radius),
                topRight + new Vector2(-radius, -radius),
                bottomRight + new Vector2(-radius, radius)
            };
            Vector2[] arcStarts =
            {
                bottomLeft + new Vector2(0f, radius),
                topLeft + new Vector2(radius, 0f),
                topRight + new Vector2(0f, -radius),
                bottomRight + new Vector2(-radius, 0f)
            };
            float[] startAngles = { Mathf.PI, Mathf.PI * 0.5f, 0f, Mathf.PI * 1.5f };
            for (int corner = 0; corner < cornerPositions.Length; corner++)
            {
                Vector2 previous = arcStarts[corner];
                for (int segment = 1; segment <= CornerSegments; segment++)
                {
                    float angle = startAngles[corner] + Mathf.PI * 0.5f * segment / CornerSegments;
                    Vector2 next = centers[corner] + radius * new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    AddTriangle(helper, cornerPositions[corner], previous, next);
                    previous = next;
                }
            }
        }

        private bool IsInsideTarget(Vector2 point) => point.x >= targetMin.x && point.x <= targetMax.x
            && point.y >= targetMin.y && point.y <= targetMax.y;

        private void AddTriangle(VertexHelper helper, Vector2 a, Vector2 b, Vector2 c)
        {
            int first = helper.currentVertCount;
            AddVertex(helper, a);
            AddVertex(helper, b);
            AddVertex(helper, c);
            helper.AddTriangle(first, first + 1, first + 2);
        }

        private void AddVertex(VertexHelper helper, Vector2 position)
        {
            UIVertex vertex = UIVertex.simpleVert;
            vertex.color = color;
            vertex.position = position;
            helper.AddVert(vertex);
        }
    }
}
