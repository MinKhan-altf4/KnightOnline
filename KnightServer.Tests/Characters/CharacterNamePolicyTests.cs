using KnightOnline.Server.Characters;

namespace KnightOnline.Server.Tests.Characters;

public sealed class CharacterNamePolicyTests
{
    private readonly CharacterNamePolicy _policy = new();

    [Theory]
    [InlineData("Knight 01", "KNIGHT 01")]
    [InlineData("Hiệp Sĩ", "HIỆP SĨ")]
    public void Validate_AcceptsAndNormalizesSupportedNames(
        string candidate,
        string expected)
    {
        CharacterNameValidationResult result = _policy.Validate(candidate);

        Assert.True(result.IsValid);
        Assert.Equal(expected, result.NormalizedName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("ab")]
    [InlineData("bad_name")]
    [InlineData("two  spaces")]
    public void Validate_RejectsInvalidNames(string candidate)
    {
        CharacterNameValidationResult result = _policy.Validate(candidate);

        Assert.False(result.IsValid);
    }
}
