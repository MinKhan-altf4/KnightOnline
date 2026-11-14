#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;

namespace KnightOnline.Client.UI
{
    /// <summary>
    /// Dev-only overlay (F9) to jump the HUD between every tutorial flow state
    /// without playing through the server flow.
    ///
    /// It only calls the HUD preview API: nothing is published on the event
    /// bus, nothing is sent to the server and CharacterData is never modified.
    /// "Khôi phục" re-renders the last authoritative values. The whole file is
    /// compiled out of release builds (CORE-27 build boundary).
    /// </summary>
    public sealed class HudDebugPanel : MonoBehaviour
    {
        private const KeyCode ToggleKey = KeyCode.F9;
        private const int WolfRequired = 20;
        // IMGUI window id; only one HudDebugPanel exists per scene.
        private const int WindowId = 0x48554444;

        private VillageUIToolkitHud _villageHud;
        private InGameHUD _playerHud;
        private bool _visible;
        private Rect _window = new Rect(16f, 120f, 340f, 0f);
        private Vector2 _scroll;

        private int _wolfProgress;
        private int _level = 1;
        private float _hpRatio = 1f;
        private float _mpRatio = 1f;
        private float _expRatio;
        private string _lastAction = "-";

        public void Initialize(VillageUIToolkitHud villageHud, InGameHUD playerHud)
        {
            _villageHud = villageHud;
            _playerHud = playerHud;
            var character = playerHud != null ? playerHud.DebugCharacterData : null;
            if (character != null)
                _level = Mathf.Max(1, character.Level);
        }

        private void OnGUI()
        {
            Event current = Event.current;
            if (current.type == EventType.KeyDown && current.keyCode == ToggleKey)
            {
                _visible = !_visible;
                current.Use();
            }

            if (!_visible)
            {
                if (GUI.Button(new Rect(16f, Screen.height - 40f, 130f, 26f),
                        "HUD Debug (F9)"))
                    _visible = true;
                return;
            }

            _window.height = Mathf.Min(Screen.height - 140f, 620f);
            _window = GUILayout.Window(WindowId, _window, DrawWindow,
                "HUD Debug — chỉ xem trước (F9)");
        }

        private void DrawWindow(int id)
        {
            _scroll = GUILayout.BeginScrollView(_scroll);
            GUILayout.Label("Không gửi gì lên server, không đổi dữ liệu nhân vật.");
            DrawAuthoritativeState();
            DrawQuestSection();
            DrawMapSection();
            DrawPortalSection();
            DrawStatusSection();

            GUILayout.Space(8f);
            if (GUILayout.Button("Khôi phục dữ liệu server"))
                Restore();
            GUILayout.Label($"Thao tác cuối: {_lastAction}");
            GUILayout.EndScrollView();
            GUI.DragWindow(new Rect(0f, 0f, 10000f, 20f));
        }

        private void DrawAuthoritativeState()
        {
            GUILayout.Label("— Trạng thái server hiện tại —");
            if (_villageHud != null)
                GUILayout.Label(_villageHud.DebugAuthoritativeSummary);
            var character = _playerHud != null ? _playerHud.DebugCharacterData : null;
            if (character != null)
            {
                GUILayout.Label(
                    $"Lv.{character.Level} | HP {character.CurrentHp}/{character.MaxHp} " +
                    $"| MP {character.CurrentMana}/{character.MaxMana} " +
                    $"| EXP {character.ExperienceIntoLevel}/{character.ExperienceToNextLevel}");
            }
        }

        private void DrawQuestSection()
        {
            GUILayout.Label("— Nhiệm vụ tân thủ —");
            if (GUILayout.Button("1. Nói chuyện với Mẹ"))
                PreviewQuest(HudPresentation.StepTalkToMother, 0);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button($"2. Săn Wolf ({_wolfProgress}/{WolfRequired})"))
                PreviewQuest(HudPresentation.StepHuntWolves, _wolfProgress);
            if (GUILayout.Button("-1", GUILayout.Width(36f)))
                StepWolf(-1);
            if (GUILayout.Button("+1", GUILayout.Width(36f)))
                StepWolf(1);
            GUILayout.EndHorizontal();

            if (GUILayout.Button("3. Về gặp Mẹ (20/20)"))
                PreviewQuest(HudPresentation.StepReturnToMother, WolfRequired);
            if (GUILayout.Button("4. Hoàn tất → Safe Zone 1"))
                PreviewQuest(HudPresentation.StepDepartForSafeZone, WolfRequired);
            if (GUILayout.Button("Bước lạ (unknown_step)"))
                PreviewQuest("unknown_step", 3);
        }

        private void DrawMapSection()
        {
            GUILayout.Label("— Tên bản đồ —");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Làng")) PreviewMap(HudPresentation.MapTutorial);
            if (GUILayout.Button("Bãi Sói")) PreviewMap(HudPresentation.MapWolfField);
            if (GUILayout.Button("Safe Zone")) PreviewMap(HudPresentation.MapSafeZone);
            if (GUILayout.Button("Lạ")) PreviewMap("unknown_map");
            GUILayout.EndHorizontal();
        }

        private void DrawPortalSection()
        {
            GUILayout.Label("— Kết quả dùng cổng —");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("OK")) PreviewPortal(HudPresentation.PortalResultSuccess);
            if (GUILayout.Button("Sai vị trí"))
                PreviewPortal(HudPresentation.PortalResultNotInRange);
            if (GUILayout.Button("Thiếu level"))
                PreviewPortal(HudPresentation.PortalResultLevelTooLow);
            if (GUILayout.Button("Lỗi khác")) PreviewPortal(99);
            GUILayout.EndHorizontal();
        }

        private void DrawStatusSection()
        {
            GUILayout.Label("— HP / MP / EXP —");
            GUI.changed = false;
            GUILayout.BeginHorizontal();
            GUILayout.Label($"Level {_level}", GUILayout.Width(70f));
            if (GUILayout.Button("-", GUILayout.Width(30f))) _level = Mathf.Max(1, _level - 1);
            if (GUILayout.Button("+", GUILayout.Width(30f))) _level++;
            GUILayout.EndHorizontal();
            _hpRatio = LabeledSlider("HP", _hpRatio);
            _mpRatio = LabeledSlider("MP", _mpRatio);
            _expRatio = LabeledSlider("EXP", _expRatio);
            if (GUI.changed)
                PreviewStatus();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Đầy")) SetStatusPreset(1f, 1f);
            if (GUILayout.Button("Nguy kịch")) SetStatusPreset(0.08f, 0.2f);
            if (GUILayout.Button("Chết")) SetStatusPreset(0f, 0f);
            GUILayout.EndHorizontal();
        }

        private static float LabeledSlider(string label, float value)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label($"{label} {value * 100f:0}%", GUILayout.Width(70f));
            value = GUILayout.HorizontalSlider(value, 0f, 1f);
            GUILayout.EndHorizontal();
            return value;
        }

        private void StepWolf(int delta)
        {
            _wolfProgress = Mathf.Clamp(_wolfProgress + delta, 0, WolfRequired);
            PreviewQuest(HudPresentation.StepHuntWolves, _wolfProgress);
        }

        private void SetStatusPreset(float hp, float mp)
        {
            _hpRatio = hp;
            _mpRatio = mp;
            PreviewStatus();
        }

        private void PreviewQuest(string stepId, int progress)
        {
            _villageHud?.DebugPreviewQuest(stepId, progress, WolfRequired);
            _lastAction = $"quest {stepId} {progress}/{WolfRequired}";
        }

        private void PreviewMap(string mapId)
        {
            _villageHud?.DebugPreviewMap(mapId);
            _lastAction = $"map {mapId}";
        }

        private void PreviewPortal(byte result)
        {
            _villageHud?.DebugPreviewPortalResult(result);
            _lastAction = $"portal result {result}";
        }

        private void PreviewStatus()
        {
            const int maxHp = 1200;
            const int maxMp = 400;
            const long expToNext = 1000;
            _playerHud?.DebugPreviewPlayerStatus(_level,
                Mathf.RoundToInt(maxHp * _hpRatio), maxHp,
                Mathf.RoundToInt(maxMp * _mpRatio), maxMp,
                (long)Mathf.Round(expToNext * _expRatio), expToNext);
            _lastAction = $"status Lv.{_level} HP {_hpRatio:P0} MP {_mpRatio:P0}";
        }

        private void Restore()
        {
            _villageHud?.DebugRestoreAuthoritative();
            _playerHud?.DebugRestoreAuthoritativeStatus();
            _lastAction = "khôi phục dữ liệu server";
        }
    }
}
#endif
