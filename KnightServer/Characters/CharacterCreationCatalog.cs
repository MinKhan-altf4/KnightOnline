using KnightOnline.Client.Shared.Packets;

namespace KnightOnline.Server.Characters;

public interface ICharacterCreationCatalog
{
    GetCharacterCreationCatalogResponsePacket GetSnapshot(string serverId);
    CharacterCreationValidationResult Validate(
        CreateCharacterRequestPacket request);
}

public sealed record CharacterCreationValidationResult(
    bool IsValid,
    CreateCharacterResult Result,
    string Message)
{
    public static CharacterCreationValidationResult Success { get; } =
        new(true, CreateCharacterResult.Success, string.Empty);
}

public sealed class ConfiguredCharacterCreationCatalog :
    ICharacterCreationCatalog
{
    private static readonly StringComparer IdComparer =
        StringComparer.OrdinalIgnoreCase;
    private readonly GetCharacterCreationCatalogResponsePacket _catalog;
    private readonly IReadOnlyList<string> _requiredAppearanceSlotIds;

    public ConfiguredCharacterCreationCatalog(
        GetCharacterCreationCatalogResponsePacket catalog,
        IReadOnlyList<string>? requiredAppearanceSlotIds = null)
    {
        _catalog = catalog ??
            throw new ArgumentNullException(nameof(catalog));
        _requiredAppearanceSlotIds =
            requiredAppearanceSlotIds?.ToArray() ??
            ["base_body", "hair", "bottom", "expression"];
    }

    public GetCharacterCreationCatalogResponsePacket GetSnapshot(
        string serverId)
    {
        if (!IdComparer.Equals(serverId, _catalog.ServerId))
            throw new ArgumentException("Unknown server.", nameof(serverId));

        return _catalog;
    }

    public CharacterCreationValidationResult Validate(
        CreateCharacterRequestPacket request)
    {
        if (!IdComparer.Equals(request.ServerId, _catalog.ServerId))
            return Invalid(
                CreateCharacterResult.InvalidSlot,
                "The selected server is not available.");

        if (request.CatalogVersion != _catalog.CatalogVersion)
            return Invalid(
                CreateCharacterResult.CatalogVersionMismatch,
                "Character creation data changed. Please reload the catalog.");

        CharacterClassDefinitionPacket? selectedClass =
            _catalog.Classes.FirstOrDefault(
                definition => IdComparer.Equals(
                    definition.DefinitionId,
                    request.ClassDefinitionId));
        if (selectedClass == null)
            return Invalid(
                CreateCharacterResult.InvalidClass,
                "The selected class is not available.");

        if (!selectedClass.AllowedBodyTypeIds.Contains(
                request.BodyTypeDefinitionId,
                IdComparer) ||
            !_catalog.BodyTypes.Any(
                definition => IdComparer.Equals(
                    definition.DefinitionId,
                    request.BodyTypeDefinitionId)))
        {
            return Invalid(
                CreateCharacterResult.InvalidBodyType,
                "The selected body type is not compatible with this class.");
        }

        var selectedSlots = new HashSet<string>(IdComparer);
        foreach (AppearanceSelectionPacket selection in
                 request.AppearanceSelections)
        {
            if (!selectedSlots.Add(selection.SlotDefinitionId))
                return Invalid(
                    CreateCharacterResult.InvalidAppearance,
                    "Only one appearance may be selected for each slot.");

            AppearanceDefinitionPacket? option =
                _catalog.AppearanceOptions.FirstOrDefault(
                    definition =>
                        IdComparer.Equals(
                            definition.DefinitionId,
                            selection.OptionDefinitionId) &&
                        IdComparer.Equals(
                            definition.SlotDefinitionId,
                            selection.SlotDefinitionId));
            if (option == null ||
                !option.IsStarterOption ||
                !IsAllowed(
                    option.AllowedBodyTypeIds,
                    request.BodyTypeDefinitionId) ||
                !IsAllowed(
                    option.AllowedClassDefinitionIds,
                    request.ClassDefinitionId))
            {
                return Invalid(
                    CreateCharacterResult.InvalidAppearance,
                    "An appearance selection is unavailable or incompatible.");
            }
        }

        if (_requiredAppearanceSlotIds.Any(
                required => !selectedSlots.Contains(required)))
            return Invalid(
                CreateCharacterResult.InvalidAppearance,
                "All required appearance slots must be selected.");

        return CharacterCreationValidationResult.Success;
    }

    private static bool IsAllowed(
        IReadOnlyList<string> allowedIds,
        string selectedId) =>
        allowedIds.Count == 0 || allowedIds.Contains(selectedId, IdComparer);

    private static CharacterCreationValidationResult Invalid(
        CreateCharacterResult result,
        string message) =>
        new(false, result, message);
}
