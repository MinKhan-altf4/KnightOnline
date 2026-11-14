using NUnit.Framework;

namespace KnightOnline.Client.UI.Tests
{
    public sealed class HudPresentationTests
    {
        [TestCase("tutorial_map_01", "LÀNG TÂN THỦ")]
        [TestCase("wolf_field_01", "BÃI SÓI")]
        [TestCase("safe_zone_01", "SAFE ZONE 1")]
        [TestCase("custom_map", "custom_map")]
        [TestCase("", "KHU VỰC")]
        [TestCase(null, "KHU VỰC")]
        public void MapDisplayName_MapsKnownAndFallbackIds(string mapId, string expected)
        {
            Assert.AreEqual(expected, HudPresentation.MapDisplayName(mapId));
        }

        [Test]
        public void PortalResultMessage_SuccessHasNoMessage()
        {
            Assert.IsNull(HudPresentation.PortalResultMessage(0));
        }

        [Test]
        public void PortalResultMessage_KnownFailuresHaveSpecificText()
        {
            StringAssert.Contains("level 2", HudPresentation.PortalResultMessage(6));
            StringAssert.Contains("chính giữa", HudPresentation.PortalResultMessage(4));
        }

        [Test]
        public void PortalResultMessage_UnknownFailureIncludesCode()
        {
            StringAssert.Contains("99", HudPresentation.PortalResultMessage(99));
        }

        [Test]
        public void DescribeQuest_TalkToMother_IsNotAcceptedYet()
        {
            QuestCardModel model = HudPresentation.DescribeQuest(
                HudPresentation.StepTalkToMother, 0, 20);
            Assert.AreEqual("CHƯA NHẬN", model.ProgressText);
            Assert.AreEqual("MỚI", model.Status);
            Assert.AreEqual(0f, model.ProgressRatio);
        }

        [Test]
        public void DescribeQuest_HuntWolves_ShowsProgress()
        {
            QuestCardModel model = HudPresentation.DescribeQuest(
                HudPresentation.StepHuntWolves, 5, 20);
            Assert.AreEqual("05 / 20", model.ProgressText);
            Assert.AreEqual(0.25f, model.ProgressRatio, 0.0001f);
            Assert.AreEqual("Nhiệm vụ: Tiêu diệt Wolf (5/20)", model.SummaryLine);
        }

        [TestCase(-3, 20, 0f, "00 / 20")]
        [TestCase(50, 20, 1f, "20 / 20")]
        [TestCase(3, 0, 1f, "01 / 01")]
        public void DescribeQuest_ClampsInvalidProgress(int progress, int required,
            float expectedRatio, string expectedText)
        {
            QuestCardModel model = HudPresentation.DescribeQuest(
                HudPresentation.StepHuntWolves, progress, required);
            Assert.AreEqual(expectedRatio, model.ProgressRatio, 0.0001f);
            Assert.AreEqual(expectedText, model.ProgressText);
        }

        [Test]
        public void DescribeQuest_Completed_IsAlwaysFull()
        {
            QuestCardModel model = HudPresentation.DescribeQuest(
                HudPresentation.StepDepartForSafeZone, 3, 20);
            Assert.AreEqual(1f, model.ProgressRatio);
            Assert.AreEqual("20 / 20", model.ProgressText);
            Assert.AreEqual("ĐÃ XONG", model.Status);
        }

        [Test]
        public void DescribeQuest_UnknownStep_ShowsStepId()
        {
            QuestCardModel model = HudPresentation.DescribeQuest("new_step", 1, 4);
            Assert.AreEqual("new_step", model.Description);
            Assert.AreEqual("Nhiệm vụ: new_step", model.SummaryLine);
        }

        [TestCase(50, 100, 0.5f)]
        [TestCase(150, 100, 1f)]
        [TestCase(-5, 100, 0f)]
        [TestCase(10, 0, 0f)]
        public void ResourceBar_ClampsFill(int current, int maximum, float expected)
        {
            Assert.AreEqual(expected,
                HudPresentation.ResourceBar("HP", current, maximum).Fill, 0.0001f);
        }

        [Test]
        public void ResourceBar_LabelUsesClampedValues()
        {
            Assert.AreEqual("MP 0/40",
                HudPresentation.ResourceBar("MP", -1, 40).Label);
        }

        [Test]
        public void ExperienceBar_ShowsPercentage()
        {
            HudBarModel bar = HudPresentation.ExperienceBar(250, 1000);
            Assert.AreEqual(0.25f, bar.Fill, 0.0001f);
            StringAssert.StartsWith("EXP 25", bar.Label);
        }

        [Test]
        public void ExperienceBar_ZeroToNext_IsMax()
        {
            HudBarModel bar = HudPresentation.ExperienceBar(10, 0);
            Assert.AreEqual("EXP MAX", bar.Label);
            Assert.AreEqual(1f, bar.Fill);
        }
    }
}
