using System;

namespace KnightOnline.Client.UI
{
    /// <summary>Text and fill values for one HUD quest card state.</summary>
    public readonly struct QuestCardModel
    {
        public readonly string Title;
        public readonly string Description;
        public readonly string Status;
        public readonly string ProgressText;
        public readonly string SummaryLine;
        public readonly float ProgressRatio;

        public QuestCardModel(string title, string description, string status,
            string progressText, string summaryLine, float progressRatio)
        {
            Title = title;
            Description = description;
            Status = status;
            ProgressText = progressText;
            SummaryLine = summaryLine;
            ProgressRatio = progressRatio;
        }
    }

    /// <summary>Label and fill amount (0..1) for a HUD bar.</summary>
    public readonly struct HudBarModel
    {
        public readonly string Label;
        public readonly float Fill;

        public HudBarModel(string label, float fill)
        {
            Label = label;
            Fill = fill;
        }
    }

    /// <summary>
    /// Pure presentation mapping shared by every HUD view.
    /// Views only bind these values; the authoritative state still comes from
    /// server events. No Unity API here so it can be covered by EditMode tests.
    /// </summary>
    public static class HudPresentation
    {
        public const string StepTalkToMother = "talk_to_mother";
        public const string StepHuntWolves = "hunt_20_wolves";
        public const string StepReturnToMother = "return_to_mother";
        public const string StepDepartForSafeZone = "depart_for_safe_zone_01";

        public const string MapTutorial = "tutorial_map_01";
        public const string MapWolfField = "wolf_field_01";
        public const string MapSafeZone = "safe_zone_01";

        public const byte PortalResultSuccess = 0;
        public const byte PortalResultNotInRange = 4;
        public const byte PortalResultLevelTooLow = 6;

        public static string MapDisplayName(string mapId) => mapId switch
        {
            MapTutorial => "LÀNG TÂN THỦ",
            MapWolfField => "BÃI SÓI",
            MapSafeZone => "SAFE ZONE 1",
            _ => string.IsNullOrWhiteSpace(mapId) ? "KHU VỰC" : mapId,
        };

        /// <summary>Returns null when the portal result needs no message.</summary>
        public static string PortalResultMessage(byte result) => result switch
        {
            PortalResultSuccess => null,
            PortalResultLevelTooLow => "Safe Zone 1 yêu cầu nhân vật đạt level 2.",
            PortalResultNotInRange => "Hãy bước vào chính giữa cổng dịch chuyển.",
            _ => $"Hiện chưa thể sử dụng cổng này. (mã {result})",
        };

        public static QuestCardModel DescribeQuest(string stepId, int progress,
            int required)
        {
            required = Math.Max(1, required);
            progress = Math.Clamp(progress, 0, required);
            float ratio = (float)progress / required;
            string progressText = $"{progress:00} / {required:00}";

            switch (stepId)
            {
                case StepTalkToMother:
                    return new QuestCardModel("LỜI NHẮN CỦA MẸ",
                        "Tìm Mẹ trong làng và nhận nhiệm vụ đầu tiên.", "MỚI",
                        "CHƯA NHẬN", "Nhiệm vụ: Hãy nói chuyện với Mẹ", ratio);
                case StepHuntWolves:
                    return new QuestCardModel("BẦY SÓI NGOÀI BÌA LÀNG",
                        "Qua cổng đến Bãi Sói và hạ gục 20 Wolf.", "ĐANG LÀM",
                        progressText,
                        $"Nhiệm vụ: Tiêu diệt Wolf ({progress}/{required})", ratio);
                case StepReturnToMother:
                    return new QuestCardModel("TRỞ VỀ GẶP MẸ",
                        "Đã đủ số Wolf. Quay về làng để nhận thưởng.",
                        "HOÀN THÀNH MỤC TIÊU", progressText,
                        "Nhiệm vụ: Qua cổng về gặp Mẹ", ratio);
                case StepDepartForSafeZone:
                    return new QuestCardModel("LÊN ĐƯỜNG",
                        "Nhiệm vụ tân thủ hoàn tất. Cổng Safe Zone 1 đang chờ.",
                        "ĐÃ XONG", $"{required:00} / {required:00}",
                        "Đã hoàn thành: Lên đường tới Safe Zone 1", 1f);
                default:
                    string step = string.IsNullOrWhiteSpace(stepId) ? "?" : stepId;
                    return new QuestCardModel("HÀNH TRÌNH TÂN THỦ", step,
                        "NHIỆM VỤ", progressText, $"Nhiệm vụ: {step}", ratio);
            }
        }

        public static HudBarModel ResourceBar(string prefix, int current,
            int maximum)
        {
            int safeMaximum = Math.Max(0, maximum);
            int safeCurrent = Math.Clamp(current, 0, safeMaximum);
            float fill = safeMaximum > 0 ? (float)safeCurrent / safeMaximum : 0f;
            return new HudBarModel(
                $"{prefix} {safeCurrent:N0}/{safeMaximum:N0}", fill);
        }

        public static HudBarModel ExperienceBar(long intoLevel, long toNextLevel)
        {
            long safeToNext = Math.Max(0, toNextLevel);
            if (safeToNext == 0)
                return new HudBarModel("EXP MAX", 1f);

            long safeInto = Math.Clamp(intoLevel, 0, safeToNext);
            double percentage = safeInto * 100d / safeToNext;
            return new HudBarModel($"EXP {percentage:F1}%",
                (float)safeInto / safeToNext);
        }

        public static string LevelLabel(int level) => $"Lv.{level}";
    }
}
