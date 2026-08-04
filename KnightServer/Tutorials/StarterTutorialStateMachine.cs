using KnightOnline.Server.Configuration;

namespace KnightOnline.Server.Tutorials;

public enum TutorialState : byte
{
    NotStarted = 0,
    CoreTutorial = 1,
    ContinueOffered = 2,
    ExtendedTutorial = 3,
    Skipped = 4,
    Completed = 5,
}

public enum StarterTutorialOutcome : byte
{
    NoChange = 0,
    QuestAccepted = 1,
    KillCredited = 2,
    ReadyToTurnIn = 3,
    QuestCompleted = 4,
}

public readonly record struct StarterTutorialSnapshot(
    TutorialState State,
    string CurrentStepDefinitionId,
    int ObjectiveProgress);

public readonly record struct StarterTutorialTransition(
    StarterTutorialOutcome Outcome,
    StarterTutorialSnapshot Snapshot);

public sealed class StarterTutorialStateMachine(
    TutorialDefinitionOptions definition)
{
    public StarterTutorialTransition TalkToNpc(
        StarterTutorialSnapshot current,
        string npcDefinitionId)
    {
        if (!string.Equals(
                npcDefinitionId,
                definition.QuestNpcDefinitionId,
                StringComparison.OrdinalIgnoreCase))
        {
            return NoChange(current);
        }

        if (current.State == TutorialState.NotStarted &&
            string.Equals(
                current.CurrentStepDefinitionId,
                definition.InitialStepDefinitionId,
                StringComparison.OrdinalIgnoreCase))
        {
            return new StarterTutorialTransition(
                StarterTutorialOutcome.QuestAccepted,
                new StarterTutorialSnapshot(
                    TutorialState.CoreTutorial,
                    definition.KillStepDefinitionId,
                    0));
        }

        if (current.State == TutorialState.CoreTutorial &&
            string.Equals(
                current.CurrentStepDefinitionId,
                definition.ReturnStepDefinitionId,
                StringComparison.OrdinalIgnoreCase) &&
            current.ObjectiveProgress >= definition.RequiredKillCount)
        {
            return new StarterTutorialTransition(
                StarterTutorialOutcome.QuestCompleted,
                new StarterTutorialSnapshot(
                    TutorialState.Completed,
                    definition.CompletedStepDefinitionId,
                    definition.RequiredKillCount));
        }

        return NoChange(current);
    }

    public StarterTutorialTransition RecordMonsterKill(
        StarterTutorialSnapshot current,
        int monsterDefinitionId)
    {
        if (current.State != TutorialState.CoreTutorial ||
            !string.Equals(
                current.CurrentStepDefinitionId,
                definition.KillStepDefinitionId,
                StringComparison.OrdinalIgnoreCase) ||
            monsterDefinitionId != definition.RequiredMonsterDefinitionId)
        {
            return NoChange(current);
        }

        int progress = Math.Min(
            definition.RequiredKillCount,
            current.ObjectiveProgress + 1);
        bool ready = progress >= definition.RequiredKillCount;
        return new StarterTutorialTransition(
            ready
                ? StarterTutorialOutcome.ReadyToTurnIn
                : StarterTutorialOutcome.KillCredited,
            new StarterTutorialSnapshot(
                TutorialState.CoreTutorial,
                ready
                    ? definition.ReturnStepDefinitionId
                    : definition.KillStepDefinitionId,
                progress));
    }

    private static StarterTutorialTransition NoChange(
        StarterTutorialSnapshot current) =>
        new(StarterTutorialOutcome.NoChange, current);
}
