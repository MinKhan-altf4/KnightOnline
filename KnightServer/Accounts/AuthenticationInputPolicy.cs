namespace KnightOnline.Server.Accounts;

public static class AuthenticationInputPolicy
{
    public const int MinimumUsernameLength = 3;
    public const int MaximumUsernameLength = 32;
    public const int MinimumPasswordLength = 8;
    public const int MaximumPasswordLength = 128;
    public const int MinimumDeviceIdLength = 16;
    public const int MaximumDeviceIdLength = 128;
    public const int MinimumRefreshTokenLength = 32;
    public const int MaximumRefreshTokenLength = 256;

    public static bool IsValidDeviceId(string? deviceId) =>
        HasLength(
            deviceId,
            MinimumDeviceIdLength,
            MaximumDeviceIdLength);

    public static bool IsValidRefreshToken(string? refreshToken) =>
        HasLength(
            refreshToken,
            MinimumRefreshTokenLength,
            MaximumRefreshTokenLength);

    public static bool IsValidLoginUsername(string? username) =>
        HasLength(username, MinimumUsernameLength, MaximumUsernameLength);

    public static bool IsValidLoginPassword(string? password) =>
        HasLength(password, 1, MaximumPasswordLength);

    public static bool IsValidRegistrationPassword(string? password) =>
        HasLength(password, MinimumPasswordLength, MaximumPasswordLength);

    private static bool HasLength(
        string? value,
        int minimum,
        int maximum) =>
        !string.IsNullOrWhiteSpace(value) &&
        value.Length >= minimum &&
        value.Length <= maximum &&
        !value.Any(char.IsControl);
}
