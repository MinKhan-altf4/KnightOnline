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
            DestroyChildIfPresent(canvas, "Text_CharacterName");
            DestroyChildIfPresent(canvas, "Text_ConnectionStatus");
            DestroyChildIfPresent(canvas, "Text_PositionDebug");
            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler != null)
            {
                Undo.RecordObject(scaler, "Configure responsive HUD scale");
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.screenMatchMode =
                    CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;
                EditorUtility.SetDirty(scaler);
            }
            ConfigureMinimapAndTarget(canvas);
            Transform existing = canvas.Find("PlayerStatusPanel");
            if (existing != null)
                Undo.DestroyObjectImmediate(existing.gameObject);

            Sprite uiSprite = LoadUiSprite();
            GameObject panel = CreateImage(
                "PlayerStatusPanel",
                canvas,
                new Color(1f, 0.68f, 0.12f, 1f),
                uiSprite);
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            SetTopLeft(panelRect, new Vector2(20f, -20f), new Vector2(420f, 150f));

            GameObject surface = CreateImage(
                "Surface",
                panel.transform,
                new Color(0.08f, 0.10f, 0.12f, 0.96f),
                uiSprite);
            Stretch(surface.GetComponent<RectTransform>(), 2f);

            TextMeshProUGUI levelText = CreateText(
                "LevelText",
                surface.transform,
                "Lv.1",
                22f,
                TextAlignmentOptions.Left,
                new Color(1f, 0.9f, 0.55f));
            SetBottomLeft(
                levelText.rectTransform,
                new Vector2(12f, 8f),
                new Vector2(125f, 30f));

            Bar health = CreateBar(
                surface.transform,
                "Health",
                -10f,
                new Color(0.96f, 0.08f, 0.15f));
            Bar mana = CreateBar(
                surface.transform,
                "Mana",
                -55f,
                new Color(0.03f, 0.45f, 1f));
            TextMeshProUGUI experienceText = CreateText(
                "ExperienceText",
                surface.transform,
                "EXP 0.0%",
                22f,
                TextAlignmentOptions.Right,
                Color.white);
            SetBottomRight(
                experienceText.rectTransform,
                new Vector2(-12f, 8f),
                new Vector2(210f, 30f));

            var serializedHud = new SerializedObject(hud);
            Assign(serializedHud, "_levelText", levelText);
            Assign(serializedHud, "_healthText", health.Label);
            Assign(serializedHud, "_healthFill", health.Fill);
            Assign(serializedHud, "_manaText", mana.Label);
            Assign(serializedHud, "_manaFill", mana.Fill);
            Assign(serializedHud, "_experienceText", experienceText);
            Assign(serializedHud, "_experienceFill", null);
            serializedHud.ApplyModifiedProperties();

            BuildBottomMenu(
                canvas,
                panel,
                canvas.Find("MinimapRoot")?.gameObject,
                canvas.Find("TargetPanel")?.gameObject,
                uiSprite);

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
                10f,
                10f,
                top,
                38f);

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
                20f,
                TextAlignmentOptions.Center,
                Color.white);
            Stretch(label.rectTransform, 0f);
            return new Bar(fill, label);
        }

        private static void BuildBottomMenu(
            Transform canvas,
            GameObject playerHud,
            GameObject minimap,
            GameObject targetPanel,
            Sprite uiSprite)
        {
            DestroyChildIfPresent(canvas, "BottomMenuToggle");
            DestroyChildIfPresent(canvas, "BottomMenuPanel");

            Button toggle = CreateButton(
                "BottomMenuToggle",
                canvas,
                "^",
                uiSprite,
                new Color(1f, 0.68f, 0.12f, 1f));
            RectTransform toggleRect = toggle.GetComponent<RectTransform>();
            SetBottomCenter(
                toggleRect,
                new Vector2(0f, 14f),
                new Vector2(110f, 46f));

            GameObject menuPanel = CreateImage(
                "BottomMenuPanel",
                canvas,
                new Color(0.05f, 0.07f, 0.09f, 0.97f),
                uiSprite);
            RectTransform menuRect = menuPanel.GetComponent<RectTransform>();
            SetBottomCenter(
                menuRect,
                new Vector2(0f, 12f),
                new Vector2(940f, 108f));

            Button logout = CreateMenuButton(
                menuPanel.transform,
                "LogoutButton",
                "Đăng xuất",
                -360f,
                uiSprite,
                true);
            CreateMenuButton(
                menuPanel.transform,
                "MountButton",
                "Thú cưỡi",
                -180f,
                uiSprite,
                false);
            CreateMenuButton(
                menuPanel.transform,
                "EquipmentButton",
                "Trang bị",
                0f,
                uiSprite,
                false);
            CreateMenuButton(
                menuPanel.transform,
                "FriendsButton",
                "Bạn bè",
                180f,
                uiSprite,
                false);
            CreateMenuButton(
                menuPanel.transform,
                "MoreButton",
                "Mở rộng",
                360f,
                uiSprite,
                false);
            menuPanel.SetActive(false);

            InGameMenuView view =
                canvas.GetComponent<InGameMenuView>() ??
                Undo.AddComponent<InGameMenuView>(canvas.gameObject);
            var serializedView = new SerializedObject(view);
            Assign(serializedView, "_toggleButton", toggle);
            Assign(
                serializedView,
                "_toggleLabel",
                toggle.GetComponentInChildren<TextMeshProUGUI>());
            Assign(serializedView, "_menuPanel", menuRect);
            Assign(serializedView, "_logoutButton", logout);
            SerializedProperty hudToHide =
                serializedView.FindProperty("_hudToHide");
            GameObject[] hudObjects =
                { playerHud, minimap, targetPanel };
            hudToHide.arraySize = hudObjects.Length;
            for (int index = 0; index < hudObjects.Length; index++)
            {
                hudToHide.GetArrayElementAtIndex(index)
                    .objectReferenceValue = hudObjects[index];
            }
            serializedView.ApplyModifiedProperties();
            EditorUtility.SetDirty(view);
        }

        private static Button CreateMenuButton(
            Transform parent,
            string name,
            string label,
            float x,
            Sprite sprite,
            bool interactable)
        {
            Button button = CreateButton(
                name,
                parent,
                label,
                sprite,
                interactable
                    ? new Color(0.72f, 0.12f, 0.12f, 1f)
                    : new Color(0.24f, 0.26f, 0.28f, 1f));
            RectTransform rect = button.GetComponent<RectTransform>();
            SetMiddleCenter(
                rect,
                new Vector2(x, 0f),
                new Vector2(160f, 72f));
            button.interactable = interactable;
            return button;
        }

        private static Button CreateButton(
            string name,
            Transform parent,
            string label,
            Sprite sprite,
            Color color)
        {
            GameObject gameObject = CreateImage(
                name,
                parent,
                color,
                sprite);
            Image image = gameObject.GetComponent<Image>();
            image.raycastTarget = true;
            Button button = gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            TextMeshProUGUI text = CreateText(
                "Label",
                gameObject.transform,
                label,
                24f,
                TextAlignmentOptions.Center,
                Color.white);
            Stretch(text.rectTransform, 4f);
            return button;
        }

        private static void DestroyChildIfPresent(
            Transform parent,
            string childName)
        {
            Transform child = parent.Find(childName);
            if (child != null)
                Undo.DestroyObjectImmediate(child.gameObject);
        }

        private static void ConfigureMinimapAndTarget(Transform canvas)
        {
            Transform minimap = canvas.Find("MinimapRoot");
            if (minimap is RectTransform minimapRect)
            {
                Undo.RecordObject(minimapRect, "Resize minimap");
                minimapRect.anchoredPosition = new Vector2(-20f, -20f);
                minimapRect.sizeDelta = new Vector2(300f, 300f);
                Image minimapFrame = minimap.GetComponent<Image>();
                if (minimapFrame != null)
                {
                    Undo.RecordObject(minimapFrame, "Restyle minimap");
                    minimapFrame.color = new Color(0.95f, 0.65f, 0.12f, 1f);
                    EditorUtility.SetDirty(minimapFrame);
                }
                Image mapArea = minimap.Find("MapArea")?.GetComponent<Image>();
                if (mapArea != null)
                {
                    Undo.RecordObject(mapArea, "Restyle minimap area");
                    mapArea.color = new Color(0.12f, 0.32f, 0.18f, 1f);
                    EditorUtility.SetDirty(mapArea);
                }
                TMP_Text zoneName =
                    minimap.Find("ZoneNameText")?.GetComponent<TMP_Text>();
                if (zoneName != null)
                {
                    Undo.RecordObject(zoneName, "Restyle minimap label");
                    zoneName.fontSize = 22f;
                    zoneName.fontStyle = FontStyles.Bold;
                    zoneName.color = Color.white;
                    EditorUtility.SetDirty(zoneName);
                }
                EditorUtility.SetDirty(minimapRect);
            }

            Transform target = canvas.Find("TargetPanel");
            if (target is RectTransform targetRect)
            {
                Undo.RecordObject(targetRect, "Reposition target panel");
                targetRect.anchoredPosition = new Vector2(-335f, -20f);
                EditorUtility.SetDirty(targetRect);
            }
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
            text.fontStyle = FontStyles.Bold;
            text.outlineColor = new Color32(0, 0, 0, 255);
            text.outlineWidth = 0.18f;
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

        private static void SetBottomLeft(
            RectTransform rect,
            Vector2 position,
            Vector2 size)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void SetBottomRight(
            RectTransform rect,
            Vector2 position,
            Vector2 size)
        {
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void SetBottomCenter(
            RectTransform rect,
            Vector2 position,
            Vector2 size)
        {
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void SetMiddleCenter(
            RectTransform rect,
            Vector2 position,
            Vector2 size)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
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
