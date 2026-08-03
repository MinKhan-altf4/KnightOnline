using KnightOnline.Client.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace KnightOnline.Client.Editor
{
    public static class InGamePlayerHudBuilder
    {
        private const string MenuPath =
            "KnightOnline/UI/Build Player Status HUD";

        [MenuItem(MenuPath)]
        public static void Build()
        {
            InGameHUD hud = Object.FindAnyObjectByType<InGameHUD>();
            if (hud == null)
            {
                EditorUtility.DisplayDialog(
                    "Player HUD",
                    "Open the InGame scene first. No InGameHUD was found.",
                    "OK");
                return;
            }

            Transform canvas = hud.transform;
            Transform existing = canvas.Find("PlayerStatusPanel");
            if (existing != null)
                Undo.DestroyObjectImmediate(existing.gameObject);

            Sprite uiSprite = LoadUiSprite();
            GameObject panel = CreateImage(
                "PlayerStatusPanel",
                canvas,
                new Color(0.035f, 0.055f, 0.09f, 0.9f),
                uiSprite);
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            SetTopLeft(panelRect, new Vector2(18f, -18f), new Vector2(350f, 158f));

            TextMeshProUGUI nameText = CreateText(
                "CharacterNameText",
                panel.transform,
                "Character",
                22f,
                TextAlignmentOptions.Left,
                new Color(1f, 0.88f, 0.45f));
            SetTopStretch(
                nameText.rectTransform,
                12f,
                80f,
                -7f,
                28f);

            TextMeshProUGUI levelText = CreateText(
                "LevelText",
                panel.transform,
                "Lv. 1",
                20f,
                TextAlignmentOptions.Right,
                Color.white);
            SetTopRight(
                levelText.rectTransform,
                new Vector2(-12f, -7f),
                new Vector2(78f, 28f));

            Bar health = CreateBar(
                panel.transform,
                "Health",
                -43f,
                new Color(0.86f, 0.12f, 0.12f));
            Bar mana = CreateBar(
                panel.transform,
                "Mana",
                -78f,
                new Color(0.12f, 0.38f, 0.92f));
            Bar experience = CreateBar(
                panel.transform,
                "Experience",
                -113f,
                new Color(0.95f, 0.68f, 0.08f));

            var serializedHud = new SerializedObject(hud);
            Assign(serializedHud, "_characterNameText", nameText);
            Assign(serializedHud, "_levelText", levelText);
            Assign(serializedHud, "_healthText", health.Label);
            Assign(serializedHud, "_healthFill", health.Fill);
            Assign(serializedHud, "_manaText", mana.Label);
            Assign(serializedHud, "_manaFill", mana.Fill);
            Assign(serializedHud, "_experienceText", experience.Label);
            Assign(serializedHud, "_experienceFill", experience.Fill);
            serializedHud.ApplyModifiedProperties();

            Undo.RegisterCreatedObjectUndo(panel, "Build Player Status HUD");
            EditorUtility.SetDirty(hud);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Selection.activeGameObject = panel;
            EditorUtility.DisplayDialog(
                "Player HUD",
                "PlayerStatusPanel was created and connected to InGameHUD. " +
                "Save the scene with Ctrl+S.",
                "OK");
        }

        private static Bar CreateBar(
            Transform parent,
            string name,
            float top,
            Color fillColor)
        {
            Sprite uiSprite = LoadUiSprite();
            GameObject background = CreateImage(
                name + "Background",
                parent,
                new Color(0.05f, 0.05f, 0.06f, 0.95f),
                uiSprite);
            SetTopStretch(
                background.GetComponent<RectTransform>(),
                12f,
                12f,
                top,
                27f);

            GameObject fillObject = CreateImage(
                name + "Fill",
                background.transform,
                fillColor,
                uiSprite);
            Image fill = fillObject.GetComponent<Image>();
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 1f;
            fill.raycastTarget = false;
            Stretch(fill.rectTransform, 2f);

            TextMeshProUGUI label = CreateText(
                name + "Text",
                background.transform,
                name,
                17f,
                TextAlignmentOptions.Center,
                Color.white);
            Stretch(label.rectTransform, 0f);
            return new Bar(fill, label);
        }

        private static GameObject CreateImage(
            string name,
            Transform parent,
            Color color,
            Sprite sprite)
        {
            var gameObject = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            gameObject.layer = parent.gameObject.layer;
            gameObject.transform.SetParent(parent, false);
            Image image = gameObject.GetComponent<Image>();
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.color = color;
            image.raycastTarget = false;
            return gameObject;
        }

        private static Sprite LoadUiSprite()
        {
            Sprite sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>(
                "UI/Skin/UISprite.psd");
            if (sprite != null)
                return sprite;

            Object[] buttonAssets = AssetDatabase.LoadAllAssetsAtPath(
                "Assets/_Project/Art/UI/Buttons/buttons.png");
            foreach (Object asset in buttonAssets)
            {
                if (asset is Sprite projectSprite)
                    return projectSprite;
            }

            throw new MissingReferenceException(
                "No UI sprite is available for the Player HUD.");
        }

        private static TextMeshProUGUI CreateText(
            string name,
            Transform parent,
            string value,
            float fontSize,
            TextAlignmentOptions alignment,
            Color color)
        {
            var gameObject = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));
            gameObject.layer = parent.gameObject.layer;
            gameObject.transform.SetParent(parent, false);
            TextMeshProUGUI text = gameObject.GetComponent<TextMeshProUGUI>();
            text.text = value;
            text.font = TMP_Settings.defaultFontAsset;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = color;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            return text;
        }

        private static void Assign(
            SerializedObject target,
            string propertyName,
            Object value)
        {
            SerializedProperty property = target.FindProperty(propertyName);
            if (property == null)
                throw new MissingReferenceException(propertyName);
            property.objectReferenceValue = value;
        }

        private static void SetTopLeft(
            RectTransform rect,
            Vector2 position,
            Vector2 size)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void SetTopRight(
            RectTransform rect,
            Vector2 position,
            Vector2 size)
        {
            rect.anchorMin = Vector2.one;
            rect.anchorMax = Vector2.one;
            rect.pivot = Vector2.one;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void SetTopStretch(
            RectTransform rect,
            float left,
            float right,
            float top,
            float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(
                (left - right) * 0.5f,
                top);
            rect.sizeDelta = new Vector2(-(left + right), height);
        }

        private static void Stretch(RectTransform rect, float inset)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
        }

        private readonly struct Bar
        {
            public readonly Image Fill;
            public readonly TextMeshProUGUI Label;

            public Bar(Image fill, TextMeshProUGUI label)
            {
                Fill = fill;
                Label = label;
            }
        }
    }
}
