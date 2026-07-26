using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace KnightOnline.Server.Characters;

public sealed partial class CharacterNamePolicy
{
    public const int MinimumLength = 3;
    public const int MaximumLength = 20;

    public CharacterNameValidationResult Validate(string? candidate)
    {
        string displayName = (candidate ?? string.Empty).Trim()
            .Normalize(NormalizationForm.FormC);

        if (displayName.Length < MinimumLength ||
            displayName.Length > MaximumLength)
        {
            return CharacterNameValidationResult.Invalid(
                $"Character name must contain {MinimumLength} to " +
                $"{MaximumLength} characters.");
        }

        if (!AllowedNamePattern().IsMatch(displayName))
            return CharacterNameValidationResult.Invalid(
                "Character name may only contain letters, numbers and spaces.");

        string normalizedName = displayName
            .ToUpper(CultureInfo.InvariantCulture)
            .Normalize(NormalizationForm.FormC);
        return new CharacterNameValidationResult(
            true,
            displayName,
            normalizedName,
            string.Empty);
    }

    [GeneratedRegex(
        @"^[\p{L}\p{Nd}]+(?: [\p{L}\p{Nd}]+)*$",
        RegexOptions.CultureInvariant)]
    private static partial Regex AllowedNamePattern();
}

public sealed record CharacterNameValidationResult(
    bool IsValid,
    string DisplayName,
    string NormalizedName,
    string Message)
{
    public static CharacterNameValidationResult Invalid(string message) =>
        new(false, string.Empty, string.Empty, message);
}
