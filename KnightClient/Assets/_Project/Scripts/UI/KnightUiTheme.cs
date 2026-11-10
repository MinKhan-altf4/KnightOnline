using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KnightOnline.Client.UI
{
    /// <summary>
    /// Presentation-only theme. Gameplay views consume this asset instead of
    /// owning concrete sprites/colors, so the placeholder skin can be replaced
    /// without changing flow or domain code.
    /// </summary>
    [CreateAssetMenu(
        fileName = "KnightUiTheme",
        menuName = "KnightOnline/UI Theme")]
    public sealed class KnightUiTheme : ScriptableObject
    {
        private const string DefaultResourcePath = "UI/KnightUiTheme";

        [Header("Button sprites")]
        [SerializeField] private Sprite _normalButtonSprite;
        [SerializeField] private Sprite _highlightedButtonSprite;
        [SerializeField] private Sprite _pressedButtonSprite;

        [Header("Surfaces")]
        [SerializeField] private Sprite _panelFrameSprite;
        [SerializeField] private Color _screenOverlay =
            new Color(0.035f, 0.055f, 0.09f, 0.34f);
        [SerializeField] private Color _panelColor =
            new Color(0.10f, 0.15f, 0.23f, 0.94f);
        [SerializeField] private Color _inputColor =
            new Color(0.96f, 0.91f, 0.78f, 0.96f);

        [Header("Typography")]
        [SerializeField] private Color _primaryText =
            new Color(1f, 0.96f, 0.84f, 1f);
        [SerializeField] private Color _bodyText =
            new Color(0.96f, 0.95f, 0.90f, 1f);
        [SerializeField] private Color _inputText =
            new Color(0.16f, 0.10f, 0.08f, 1f);

        public static KnightUiTheme LoadDefault() =>
            Resources.Load<KnightUiTheme>(DefaultResourcePath);

        public void ApplyButton(Button button, float height = 60f)
        {
            if (button == null)
                return;

            var image = button.targetGraphic as Image ??
                button.GetComponent<Image>();
            if (image != null)
            {
                image.sprite = _normalButtonSprite;
                image.type = Image.Type.Sliced;
                image.color = Color.white;
                button.targetGraphic = image;
            }

            button.transition = Selectable.Transition.SpriteSwap;
            button.spriteState = new SpriteState
            {
                highlightedSprite = _highlightedButtonSprite,
                selectedSprite = _highlightedButtonSprite,
                pressedSprite = _pressedButtonSprite,
                disabledSprite = _normalButtonSprite,
            };

            var layout = button.GetComponent<LayoutElement>() ??
                button.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = height;
            layout.preferredHeight = height;

            StyleButtonLabel(button);
        }

        public void ApplyCleanButton(
            Button button,
            float height = 60f,
            Color? baseColor = null)
        {
            if (button == null)
                return;

            Image image = button.targetGraphic as Image ??
                button.GetComponent<Image>();
            if (image != null)
            {
                image.sprite = null;
                image.type = Image.Type.Simple;
                image.color = baseColor ?? new Color(0.48f, 0.13f, 0.15f, 1f);
                button.targetGraphic = image;
            }

            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.16f, 1.16f, 1.16f, 1f);
            colors.pressedColor = new Color(0.78f, 0.78f, 0.78f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(0.65f, 0.65f, 0.65f, 0.55f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.12f;
            button.colors = colors;
            button.transition = Selectable.Transition.ColorTint;

            var layout = button.GetComponent<LayoutElement>() ??
                button.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = height;
            layout.preferredHeight = height;
            StyleButtonLabel(button);
        }

        private void StyleButtonLabel(Button button)
        {
            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
            if (label == null)
                return;

            label.color = _primaryText;
            label.fontStyle = FontStyles.Bold;
            label.fontSizeMin = 18f;
            label.fontSizeMax = 28f;
            label.enableAutoSizing = true;
            label.alignment = TextAlignmentOptions.Center;
        }

        public void ApplyInput(TMP_InputField input, float height = 54f)
        {
            if (input == null)
                return;

            Image image = input.GetComponent<Image>();
            if (image != null)
                image.color = _inputColor;

            var layout = input.GetComponent<LayoutElement>() ??
                input.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = height;
            layout.preferredHeight = height;

            if (input.textComponent != null)
            {
                input.textComponent.color = _inputText;
                input.textComponent.fontSize = 22f;
            }
            if (input.placeholder is TMP_Text placeholder)
            {
                placeholder.color =
                    new Color(
                        _inputText.r,
                        _inputText.g,
                        _inputText.b,
                        0.55f);
            }
        }

        public void ApplyDropdown(TMP_Dropdown dropdown, float height = 54f)
        {
            if (dropdown == null)
                return;

            Image image = dropdown.GetComponent<Image>();
            if (image != null)
                image.color = _inputColor;

            var layout = dropdown.GetComponent<LayoutElement>() ??
                dropdown.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = height;
            layout.preferredHeight = height;

            if (dropdown.captionText != null)
            {
                dropdown.captionText.color = _inputText;
                dropdown.captionText.fontSize = 20f;
            }
        }

        public void ApplyPanel(Image image, bool fullScreen = false)
        {
            if (image == null)
                return;

            image.sprite = fullScreen ? null : _panelFrameSprite;
            image.type = Image.Type.Simple;
            image.color = fullScreen ? _screenOverlay : _panelColor;
        }

        public void ApplyCard(Image image)
        {
            if (image == null)
                return;

            image.sprite = null;
            image.type = Image.Type.Simple;
            image.color = _panelColor;
            image.raycastTarget = false;
        }

        public void ApplyBodyText(TMP_Text text, float size = 22f)
        {
            if (text == null)
                return;

            text.color = _bodyText;
            text.fontSize = size;
            text.enableAutoSizing = true;
            text.fontSizeMin = 14f;
            text.fontSizeMax = size;
        }
    }
}
