using KnightOnline.Client.Shared.Packets;
using KnightOnline.Server.Characters;

namespace KnightOnline.Server.Tests.Characters;

public sealed class CharacterCreationCatalogTests
{
    private readonly ConfiguredCharacterCreationCatalog _catalog =
        new(CreateCatalog());

    [Fact]
    public void Validate_AcceptsCompleteCompatibleStarterSelection()
    {
        CharacterCreationValidationResult result = _catalog.Validate(
            CreateRequest(
                [
                    Selection("base_body", "body_male_001"),
                    Selection("hair", "hair_001"),
                    Selection("bottom", "bottom_001"),
                    Selection("expression", "expression_001"),
                ]));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_RejectsMissingRequiredAppearanceSlot()
    {
        CharacterCreationValidationResult result = _catalog.Validate(
            CreateRequest(
                [
                    Selection("base_body", "body_male_001"),
                    Selection("hair", "hair_001"),
                    Selection("bottom", "bottom_001"),
                ]));

        Assert.False(result.IsValid);
        Assert.Equal(
            CreateCharacterResult.InvalidAppearance,
            result.Result);
    }

    [Fact]
    public void Validate_RejectsStaleCatalogVersion()
    {
        CreateCharacterRequestPacket request = CreateRequest(
            [
                Selection("base_body", "body_male_001"),
                Selection("hair", "hair_001"),
                Selection("bottom", "bottom_001"),
                Selection("expression", "expression_001"),
            ],
            catalogVersion: 99);

        CharacterCreationValidationResult result = _catalog.Validate(request);

        Assert.Equal(
            CreateCharacterResult.CatalogVersionMismatch,
            result.Result);
    }

    [Fact]
    public void Validate_UsesConfiguredRequiredAppearanceSlots()
    {
        var catalog = new ConfiguredCharacterCreationCatalog(
            CreateCatalog(),
            ["base_body", "hair"]);

        CharacterCreationValidationResult result = catalog.Validate(
            CreateRequest(
                [
                    Selection("base_body", "body_male_001"),
                    Selection("hair", "hair_001"),
                ]));

        Assert.True(result.IsValid);
    }

    private static CreateCharacterRequestPacket CreateRequest(
        IReadOnlyList<AppearanceSelectionPacket> appearance,
        int catalogVersion = 1) =>
        new(
            Guid.NewGuid(),
            "server-1",
            1,
            "Knight",
            "warrior",
            "male",
            appearance,
            catalogVersion);

    private static AppearanceSelectionPacket Selection(
        string slot,
        string option) =>
        new(slot, option);

    private static GetCharacterCreationCatalogResponsePacket CreateCatalog()
    {
        string[] bodies = ["male", "female"];
        return new GetCharacterCreationCatalogResponsePacket(
            1,
            "server-1",
            [
                new CharacterClassDefinitionPacket(
                    "warrior",
                    "Chiến binh",
                    string.Empty,
                    bodies,
                    string.Empty),
            ],
            [
                new BodyTypeDefinitionPacket("male", "Nam"),
                new BodyTypeDefinitionPacket("female", "Nữ"),
            ],
            [
                Appearance("body_male_001", "base_body", ["male"]),
                Appearance("hair_001", "hair", bodies),
                Appearance("bottom_001", "bottom", bodies),
                Appearance("expression_001", "expression", bodies),
            ]);
    }

    private static AppearanceDefinitionPacket Appearance(
        string id,
        string slot,
        IReadOnlyList<string> bodies) =>
        new(
            id,
            slot,
            id,
            bodies,
            [],
            string.Empty,
            true);
}
