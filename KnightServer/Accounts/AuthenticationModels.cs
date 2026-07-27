namespace KnightOnline.Server.Accounts;

public sealed record AuthenticatedAccount(
    string AccountKey,
    string DisplayName,
    bool IsGuest,
    string RefreshToken,
    DateTime RefreshTokenExpiresAtUtc);

public enum AuthenticationFailure
{
    None = 0,
    InvalidCredentials = 1,
    InvalidOrExpiredToken = 2,
    UsernameUnavailable = 3,
    GuestNotFound = 4,
    InvalidRequest = 5,
}

public sealed record AuthenticationResult(
    AuthenticatedAccount? Account,
    AuthenticationFailure Failure)
{
    public bool IsSuccess => Account != null;

    public static AuthenticationResult Success(AuthenticatedAccount account) =>
        new(account, AuthenticationFailure.None);

    public static AuthenticationResult Failed(AuthenticationFailure failure) =>
        new(null, failure);
}
