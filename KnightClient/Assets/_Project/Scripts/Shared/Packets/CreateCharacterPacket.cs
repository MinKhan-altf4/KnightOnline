#nullable enable
using System;
using System.Collections.Generic;

namespace KnightOnline.Client.Shared.Packets
{
    public enum CreateCharacterResult : byte
    {
        Success = 0,
        NameEmpty = 1,
        NameTooLong = 2,
        NameAlreadyTaken = 3,
        CharacterLimitReached = 4,
        Unauthorized = 5,
        InvalidSlot = 6,
        SlotAlreadyOccupied = 7,
        InvalidClass = 8,
        InvalidBodyType = 9,
        InvalidAppearance = 10,
        CatalogVersionMismatch = 11,
        InvalidName = 12,
        DuplicateRequest = 13,
        MalformedRequest = 14,
    }

    public sealed class CreateCharacterRequestPacket
    {
        public Guid RequestId { get; }
        public string ServerId { get; }
        public int SlotIndex { get; }
        public string CharacterName { get; }
        public string ClassDefinitionId { get; }
        public string BodyTypeDefinitionId { get; }
        public IReadOnlyList<AppearanceSelectionPacket> AppearanceSelections { get; }
        public int CatalogVersion { get; }

        public CreateCharacterRequestPacket(
            Guid requestId,
            string serverId,
            int slotIndex,
            string characterName,
            string classDefinitionId,
            string bodyTypeDefinitionId,
            IReadOnlyList<AppearanceSelectionPacket> appearanceSelections,
            int catalogVersion)
        {
            RequestId = requestId;
            ServerId = serverId ?? string.Empty;
            SlotIndex = slotIndex;
            CharacterName = characterName;
            ClassDefinitionId = classDefinitionId ?? string.Empty;
            BodyTypeDefinitionId = bodyTypeDefinitionId ?? string.Empty;
            AppearanceSelections =
                appearanceSelections ?? Array.Empty<AppearanceSelectionPacket>();
            CatalogVersion = catalogVersion;
        }
    }

    public sealed class CreateCharacterResponsePacket
    {
        public CreateCharacterResult Result { get; }
        public string Message { get; }
        public Guid RequestId { get; }
        public CharacterSummaryPacket? Character { get; }

        public CreateCharacterResponsePacket(
            CreateCharacterResult result,
            string message,
            Guid requestId = default,
            CharacterSummaryPacket? character = null)
        {
            Result = result;
            Message = message;
            RequestId = requestId;
            Character = character;
        }
    }

    public sealed class AppearanceSelectionPacket
    {
        public string SlotDefinitionId { get; }
        public string OptionDefinitionId { get; }

        public AppearanceSelectionPacket(
            string slotDefinitionId,
            string optionDefinitionId)
        {
            SlotDefinitionId = slotDefinitionId ?? string.Empty;
            OptionDefinitionId = optionDefinitionId ?? string.Empty;
        }
    }

    public sealed class GetCharacterCreationCatalogRequestPacket
    {
        public string ServerId { get; }

        public GetCharacterCreationCatalogRequestPacket(string serverId) =>
            ServerId = serverId ?? string.Empty;
    }

    public sealed class CharacterClassDefinitionPacket
    {
        public string DefinitionId { get; }
        public string DisplayName { get; }
        public string Description { get; }
        public IReadOnlyList<string> AllowedBodyTypeIds { get; }
        public string PreviewAssetAddress { get; }

        public CharacterClassDefinitionPacket(
            string definitionId,
            string displayName,
            string description,
            IReadOnlyList<string> allowedBodyTypeIds,
            string previewAssetAddress)
        {
            DefinitionId = definitionId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            Description = description ?? string.Empty;
            AllowedBodyTypeIds = allowedBodyTypeIds ?? Array.Empty<string>();
            PreviewAssetAddress = previewAssetAddress ?? string.Empty;
        }
    }

    public sealed class BodyTypeDefinitionPacket
    {
        public string DefinitionId { get; }
        public string DisplayName { get; }

        public BodyTypeDefinitionPacket(string definitionId, string displayName)
        {
            DefinitionId = definitionId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
        }
    }

    public sealed class AppearanceDefinitionPacket
    {
        public string DefinitionId { get; }
        public string SlotDefinitionId { get; }
        public string DisplayName { get; }
        public IReadOnlyList<string> AllowedBodyTypeIds { get; }
        public IReadOnlyList<string> AllowedClassDefinitionIds { get; }
        public string AssetAddress { get; }
        public bool IsStarterOption { get; }

        public AppearanceDefinitionPacket(
            string definitionId,
            string slotDefinitionId,
            string displayName,
            IReadOnlyList<string> allowedBodyTypeIds,
            IReadOnlyList<string> allowedClassDefinitionIds,
            string assetAddress,
            bool isStarterOption)
        {
            DefinitionId = definitionId ?? string.Empty;
            SlotDefinitionId = slotDefinitionId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            AllowedBodyTypeIds = allowedBodyTypeIds ?? Array.Empty<string>();
            AllowedClassDefinitionIds =
                allowedClassDefinitionIds ?? Array.Empty<string>();
            AssetAddress = assetAddress ?? string.Empty;
            IsStarterOption = isStarterOption;
        }
    }

    public sealed class GetCharacterCreationCatalogResponsePacket
    {
        public int CatalogVersion { get; }
        public string ServerId { get; }
        public IReadOnlyList<CharacterClassDefinitionPacket> Classes { get; }
        public IReadOnlyList<BodyTypeDefinitionPacket> BodyTypes { get; }
        public IReadOnlyList<AppearanceDefinitionPacket> AppearanceOptions { get; }

        public GetCharacterCreationCatalogResponsePacket(
            int catalogVersion,
            string serverId,
            IReadOnlyList<CharacterClassDefinitionPacket> classes,
            IReadOnlyList<BodyTypeDefinitionPacket> bodyTypes,
            IReadOnlyList<AppearanceDefinitionPacket> appearanceOptions)
        {
            CatalogVersion = catalogVersion;
            ServerId = serverId ?? string.Empty;
            Classes = classes ?? Array.Empty<CharacterClassDefinitionPacket>();
            BodyTypes = bodyTypes ?? Array.Empty<BodyTypeDefinitionPacket>();
            AppearanceOptions =
                appearanceOptions ?? Array.Empty<AppearanceDefinitionPacket>();
        }
    }

    public sealed class CheckCharacterNameRequestPacket
    {
        public string ServerId { get; }
        public string CharacterName { get; }

        public CheckCharacterNameRequestPacket(
            string serverId,
            string characterName)
        {
            ServerId = serverId ?? string.Empty;
            CharacterName = characterName ?? string.Empty;
        }
    }

    public sealed class CheckCharacterNameResponsePacket
    {
        public bool IsAvailable { get; }
        public string NormalizedName { get; }
        public string Message { get; }

        public CheckCharacterNameResponsePacket(
            bool isAvailable,
            string normalizedName,
            string message)
        {
            IsAvailable = isAvailable;
            NormalizedName = normalizedName ?? string.Empty;
            Message = message ?? string.Empty;
        }
    }
}
