using KnightOnline.Server.Configuration;
using KnightOnline.Server.Tutorials;
using Xunit;

namespace KnightServer.Tests.Tutorials;

public sealed class StarterTutorialStateMachineTests
{
    private readonly TutorialDefinitionOptions _definition = new()
    {
        DefinitionId = "starter_tutorial_v1",
        InitialStepDefinitionId = "talk_to_mother",
        KillStepDefinitionId = "hunt_20_wolves",
        ReturnStepDefinitionId = "return_to_mother",
        CompletedStepDefinitionId = "depart_for_safe_zone_01",
        QuestNpcDefinitionId = "mother_tutorial",
        RequiredMonsterDefinitionId = 1,
        RequiredKillCount = 20,
    };

    [Fact]
    public void TalkToMother_AcceptsQuestOnlyFromInitialStep()
    {
        var machine = new StarterTutorialStateMachine(_definition);

        StarterTutorialTransition transition = machine.TalkToNpc(
            new StarterTutorialSnapshot(
                TutorialState.NotStarted,
                "talk_to_mother",
                0),
            "mother_tutorial");

        Assert.Equal(
            StarterTutorialOutcome.QuestAccepted,
            transition.Outcome);
        Assert.Equal(
            "hunt_20_wolves",
            transition.Snapshot.CurrentStepDefinitionId);
    }

    [Fact]
    public void WrongMonster_DoesNotAdvanceObjective()
    {
        var machine = new StarterTutorialStateMachine(_definition);
        var current = new StarterTutorialSnapshot(
            TutorialState.CoreTutorial,
            "hunt_20_wolves",
            7);

        StarterTutorialTransition transition =
            machine.RecordMonsterKill(current, monsterDefinitionId: 2);

        Assert.Equal(StarterTutorialOutcome.NoChange, transition.Outcome);
        Assert.Equal(current, transition.Snapshot);
    }

    [Fact]
    public void TwentiethWolf_MovesQuestToReturnStep()
    {
        var machine = new StarterTutorialStateMachine(_definition);

        StarterTutorialTransition transition = machine.RecordMonsterKill(
            new StarterTutorialSnapshot(
                TutorialState.CoreTutorial,
                "hunt_20_wolves",
                19),
            monsterDefinitionId: 1);

        Assert.Equal(
            StarterTutorialOutcome.ReadyToTurnIn,
            transition.Outcome);
        Assert.Equal(20, transition.Snapshot.ObjectiveProgress);
        Assert.Equal(
            "return_to_mother",
            transition.Snapshot.CurrentStepDefinitionId);
    }

    [Fact]
    public void TalkToMother_CompletesOnlyAfterTwentyKills()
    {
        var machine = new StarterTutorialStateMachine(_definition);

        StarterTutorialTransition early = machine.TalkToNpc(
            new StarterTutorialSnapshot(
                TutorialState.CoreTutorial,
                "hunt_20_wolves",
                19),
            "mother_tutorial");
        StarterTutorialTransition completed = machine.TalkToNpc(
            new StarterTutorialSnapshot(
                TutorialState.CoreTutorial,
                "return_to_mother",
                20),
            "mother_tutorial");

        Assert.Equal(StarterTutorialOutcome.NoChange, early.Outcome);
        Assert.Equal(
            StarterTutorialOutcome.QuestCompleted,
            completed.Outcome);
        Assert.Equal(TutorialState.Completed, completed.Snapshot.State);
    }
}
