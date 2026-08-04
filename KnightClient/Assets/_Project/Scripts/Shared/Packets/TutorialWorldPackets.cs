#nullable enable
using System;
using System.Collections.Generic;

namespace KnightOnline.Client.Shared.Packets
{
    public sealed class ListNpcsRequestPacket { }

    public sealed class NpcSnapshotPacket
    {
        public string DefinitionId { get; }
        public string DisplayName { get; }
        public float PositionX { get; }
        public float PositionY { get; }

        public NpcSnapshotPacket(string definitionId, string displayName,
            float positionX, float positionY)
        {
            DefinitionId = definitionId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            PositionX = positionX;
            PositionY = positionY;
        }
    }

    public sealed class ListNpcsResponsePacket
    {
        public IReadOnlyList<NpcSnapshotPacket> Npcs { get; }
        public ListNpcsResponsePacket(IReadOnlyList<NpcSnapshotPacket>? npcs) =>
            Npcs = npcs ?? Array.Empty<NpcSnapshotPacket>();
    }

    public sealed class InteractNpcRequestPacket
    {
        public Guid RequestId { get; }
        public string NpcDefinitionId { get; }
        public InteractNpcRequestPacket(Guid requestId, string npcDefinitionId)
        {
            RequestId = requestId;
            NpcDefinitionId = npcDefinitionId ?? string.Empty;
        }
    }

    public enum NpcInteractionResult : byte
    {
        Success = 0, InvalidRequest = 1, NpcNotFound = 2, WrongMap = 3,
        OutOfRange = 4, InvalidQuestState = 5, AlreadyProcessed = 6,
        InternalError = 7,
    }

    public sealed class InteractNpcResponsePacket
    {
        public Guid RequestId { get; }
        public NpcInteractionResult Result { get; }
        public string Dialogue { get; }
        public InteractNpcResponsePacket(Guid requestId,
            NpcInteractionResult result, string dialogue)
        {
            RequestId = requestId;
            Result = result;
            Dialogue = dialogue ?? string.Empty;
        }
    }

    public sealed class TutorialProgressSnapshotPacket
    {
        public string TutorialDefinitionId { get; }
        public string StepDefinitionId { get; }
        public byte State { get; }
        public int Progress { get; }
        public int RequiredProgress { get; }
        public TutorialProgressSnapshotPacket(string tutorialDefinitionId,
            string stepDefinitionId, byte state, int progress,
            int requiredProgress)
        {
            TutorialDefinitionId = tutorialDefinitionId ?? string.Empty;
            StepDefinitionId = stepDefinitionId ?? string.Empty;
            State = state;
            Progress = progress;
            RequiredProgress = requiredProgress;
        }
    }

    public sealed class ListPortalsRequestPacket { }
    public sealed class PortalSnapshotPacket
    {
        public string DefinitionId { get; }
        public string DisplayName { get; }
        public float PositionX { get; }
        public float PositionY { get; }
        public PortalSnapshotPacket(string definitionId, string displayName,
            float positionX, float positionY)
        {
            DefinitionId = definitionId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            PositionX = positionX;
            PositionY = positionY;
        }
    }
    public sealed class ListPortalsResponsePacket
    {
        public IReadOnlyList<PortalSnapshotPacket> Portals { get; }
        public float MinimumX { get; }
        public float MaximumX { get; }
        public float MinimumY { get; }
        public float MaximumY { get; }
        public ListPortalsResponsePacket(
            IReadOnlyList<PortalSnapshotPacket>? portals,
            float minimumX = 0, float maximumX = 0,
            float minimumY = 0, float maximumY = 0)
        {
            Portals = portals ?? Array.Empty<PortalSnapshotPacket>();
            MinimumX = minimumX; MaximumX = maximumX;
            MinimumY = minimumY; MaximumY = maximumY;
        }
    }
    public sealed class UsePortalRequestPacket
    {
        public Guid RequestId { get; }
        public string PortalDefinitionId { get; }
        public UsePortalRequestPacket(Guid requestId, string portalDefinitionId)
        {
            RequestId = requestId;
            PortalDefinitionId = portalDefinitionId ?? string.Empty;
        }
    }
    public enum PortalUseResult : byte
    {
        Success = 0, InvalidRequest = 1, PortalNotFound = 2,
        WrongMap = 3, OutOfRange = 4, QuestStateDenied = 5,
        LevelRequired = 6,
    }
    public sealed class UsePortalResponsePacket
    {
        public Guid RequestId { get; }
        public PortalUseResult Result { get; }
        public UsePortalResponsePacket(Guid requestId, PortalUseResult result)
        {
            RequestId = requestId;
            Result = result;
        }
    }

    public sealed class MapTransitionSnapshotPacket
    {
        public string MapDefinitionId { get; }
        public string SpawnPointId { get; }
        public float PositionX { get; }
        public float PositionY { get; }
        public DateTime ServerTimeUtc { get; }
        public MapTransitionSnapshotPacket(string mapDefinitionId,
            string spawnPointId, float positionX, float positionY,
            DateTime serverTimeUtc)
        {
            MapDefinitionId = mapDefinitionId ?? string.Empty;
            SpawnPointId = spawnPointId ?? string.Empty;
            PositionX = positionX;
            PositionY = positionY;
            ServerTimeUtc = serverTimeUtc;
        }
    }

    public sealed class InventoryItemPacket
    {
        public Guid ItemInstanceId { get; }
        public string ItemDefinitionId { get; }
        public int Quantity { get; }
        public InventoryItemPacket(Guid itemInstanceId,
            string itemDefinitionId, int quantity)
        {
            ItemInstanceId = itemInstanceId;
            ItemDefinitionId = itemDefinitionId ?? string.Empty;
            Quantity = quantity;
        }
    }
    public sealed class InventorySnapshotPacket
    {
        public IReadOnlyList<InventoryItemPacket> Items { get; }
        public InventorySnapshotPacket(IReadOnlyList<InventoryItemPacket>? items) =>
            Items = items ?? Array.Empty<InventoryItemPacket>();
    }
}
