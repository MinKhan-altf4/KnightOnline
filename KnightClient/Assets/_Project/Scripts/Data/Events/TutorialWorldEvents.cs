using System;
using System.Collections.Generic;
using KnightOnline.Client.Core.Events;

namespace KnightOnline.Client.Data.Events
{
    public readonly struct NpcSnapshotData
    {
        public readonly string DefinitionId, DisplayName;
        public readonly float PositionX, PositionY;
        public NpcSnapshotData(string id, string name, float x, float y)
        { DefinitionId = id; DisplayName = name; PositionX = x; PositionY = y; }
    }
    public readonly struct NpcListReceivedEvent : IGameEvent
    { public readonly IReadOnlyList<NpcSnapshotData> Npcs; public NpcListReceivedEvent(IReadOnlyList<NpcSnapshotData> npcs) => Npcs = npcs; }
    public readonly struct NpcInteractionResultEvent : IGameEvent
    { public readonly Guid RequestId; public readonly byte Result; public readonly string Dialogue; public NpcInteractionResultEvent(Guid id, byte result, string dialogue) { RequestId = id; Result = result; Dialogue = dialogue; } }
    public readonly struct TutorialProgressChangedEvent : IStickyGameEvent
    { public readonly string TutorialId, StepId; public readonly byte State; public readonly int Progress, Required; public TutorialProgressChangedEvent(string tutorialId, string stepId, byte state, int progress, int required) { TutorialId = tutorialId; StepId = stepId; State = state; Progress = progress; Required = required; } }
    public readonly struct MapTransitionedEvent : IGameEvent
    { public readonly string MapId, SpawnId; public readonly float X, Y; public MapTransitionedEvent(string mapId, string spawnId, float x, float y) { MapId = mapId; SpawnId = spawnId; X = x; Y = y; } }
    public readonly struct PortalSnapshotData
    { public readonly string DefinitionId, DisplayName; public readonly float X, Y; public PortalSnapshotData(string id, string name, float x, float y) { DefinitionId = id; DisplayName = name; X = x; Y = y; } }
    public readonly struct PortalListReceivedEvent : IGameEvent
    { public readonly IReadOnlyList<PortalSnapshotData> Portals; public readonly float MinimumX, MaximumX, MinimumY, MaximumY; public PortalListReceivedEvent(IReadOnlyList<PortalSnapshotData> portals, float minimumX, float maximumX, float minimumY, float maximumY) { Portals = portals; MinimumX = minimumX; MaximumX = maximumX; MinimumY = minimumY; MaximumY = maximumY; } }
    public readonly struct PortalUseResultEvent : IGameEvent
    { public readonly byte Result; public PortalUseResultEvent(byte result) => Result = result; }
    public readonly struct InventoryChangedEvent : IStickyGameEvent
    { public readonly IReadOnlyList<string> ItemDefinitionIds; public InventoryChangedEvent(IReadOnlyList<string> ids) => ItemDefinitionIds = ids; }
}
