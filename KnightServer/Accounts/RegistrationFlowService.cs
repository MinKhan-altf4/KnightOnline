using System.Security.Cryptography;
using System.Text;

namespace KnightOnline.Server.Accounts;

public enum RegistrationStartFailure : byte
{
    None = 0,
    InvalidRequest = 1,
    GuestNotFound = 2,
    DuplicateRequest = 3,
}

public sealed record RegistrationStartResult(
    Guid? TransactionId,
    string? RegistrationUrl,
    string? DevelopmentAuthorizationCode,
    DateTime ExpiresAtUtc,
    RegistrationStartFailure Failure)
{
    public bool IsSuccess => TransactionId != null;
}

public sealed record RegistrationCompletionResult(
    AuthenticationResult Authentication,
    bool IsReplay);

public interface IRegistrationTransactionStore
{
    ValueTask<RegistrationTransaction?> FindByRequestIdAsync(
        Guid requestId,
        CancellationToken cancellationToken);

    ValueTask AddAsync(
        RegistrationTransaction transaction,
        CancellationToken cancellationToken);

    ValueTask<RegistrationTransaction?> TryConsumeAsync(
        Guid transactionId,
        string authorizationCodeHash,
        string pkceChallenge,
        DateTime now,
        CancellationToken cancellationToken);
}

public sealed class RegistrationTransaction
{
    public required Guid Id { get; init; }
    public required Guid RequestId { get; init; }
    public required string GuestCredentialHandle { get; init; }
    public required string DeviceId { get; init; }
    public required string PkceChallenge { get; init; }
    public required string AuthorizationCodeHash { get; init; }
    public required DateTime ExpiresAtUtc { get; init; }
    public DateTime? ConsumedAtUtc { get; set; }
}

public interface IRegistrationPortal
{
    string CreateRegistrationUrl(Guid transactionId);
}

public interface IGuestRegistrationConverter
{
    Task<AuthenticationResult> ConvertAsync(
        string guestCredentialHandle,
        string username,
        string password,
        string deviceId,
        CancellationToken cancellationToken);
}

public sealed class ExistingGuestRegistrationConverter(
    AccountAuthenticationService authentication) :
    IGuestRegistrationConverter
{
    public Task<AuthenticationResult> ConvertAsync(
        string guestCredentialHandle,
        string username,
        string password,
        string deviceId,
        CancellationToken cancellationToken) =>
        authentication.ConvertGuestAsync(
            guestCredentialHandle,
            username,
            password,
            deviceId,
            cancellationToken);
}

/// <summary>
/// Application boundary shared by the local simulator and the future web
/// account service. Production replaces the transaction store and portal;
/// packet handlers and Unity flow remain unchanged.
/// </summary>
public sealed class RegistrationFlowService(
    IRegistrationTransactionStore transactions,
    IRegistrationPortal portal,
    IGuestRegistrationConverter converter,
    AuthTokenProtector tokens,
    TimeSpan transactionLifetime,
    Time.IServerClock clock)
{
    public async Task<RegistrationStartResult> BeginAsync(
        Guid requestId,
        string guestRefreshToken,
        string deviceId,
        string pkceChallenge,
        CancellationToken cancellationToken)
    {
        if (requestId == Guid.Empty ||
            !AuthenticationInputPolicy.IsValidRefreshToken(
                guestRefreshToken) ||
            !AuthenticationInputPolicy.IsValidDeviceId(deviceId) ||
            !IsValidPkceValue(pkceChallenge))
        {
            return new RegistrationStartResult(
                null, null, null, default,
                RegistrationStartFailure.InvalidRequest);
        }

        RegistrationTransaction? existing =
            await transactions.FindByRequestIdAsync(
                requestId,
                cancellationToken);
        if (existing != null)
        {
            return new RegistrationStartResult(
                null,
                null,
                null,
                default,
                RegistrationStartFailure.DuplicateRequest);
        }

        DateTime now = clock.UtcNow;
        string authorizationCode = tokens.CreateToken();
        var transaction = new RegistrationTransaction
        {
            Id = Guid.NewGuid(),
            RequestId = requestId,
            // The local adapter uses the token as an opaque handle. A
            // production store/converter must replace it with a server-side
            // credential reference and never persist a raw token.
            GuestCredentialHandle = guestRefreshToken,
            DeviceId = deviceId,
            PkceChallenge = pkceChallenge,
            AuthorizationCodeHash = tokens.Hash(authorizationCode),
            ExpiresAtUtc = now.Add(transactionLifetime),
        };
        await transactions.AddAsync(transaction, cancellationToken);

        return new RegistrationStartResult(
            transaction.Id,
            portal.CreateRegistrationUrl(transaction.Id),
            authorizationCode,
            transaction.ExpiresAtUtc,
            RegistrationStartFailure.None);
    }

    public async Task<RegistrationCompletionResult> CompleteDevelopmentAsync(
        Guid transactionId,
        string authorizationCode,
        string pkceVerifier,
        string username,
        string password,
        CancellationToken cancellationToken)
    {
        if (transactionId == Guid.Empty ||
            !IsValidPkceValue(pkceVerifier) ||
            string.IsNullOrWhiteSpace(authorizationCode))
        {
            return Failed(AuthenticationFailure.InvalidRequest);
        }

        string codeHash = tokens.Hash(authorizationCode);
        string pkceChallenge = CreatePkceChallenge(pkceVerifier);
        RegistrationTransaction? transaction =
            await transactions.TryConsumeAsync(
                transactionId,
                codeHash,
                pkceChallenge,
                clock.UtcNow,
                cancellationToken);
        if (transaction == null)
            return Failed(AuthenticationFailure.InvalidOrExpiredToken);

        AuthenticationResult result = await converter.ConvertAsync(
            transaction.GuestCredentialHandle,
            username,
            password,
            transaction.DeviceId,
            cancellationToken);
        return new RegistrationCompletionResult(result, false);
    }

    public static string CreatePkceChallenge(string verifier)
    {
        byte[] hash = SHA256.HashData(Encoding.ASCII.GetBytes(verifier));
        return Convert.ToBase64String(hash)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static bool IsValidPkceValue(string? value) =>
        value is { Length: >= 43 and <= 128 } &&
        value.All(character =>
            char.IsAsciiLetterOrDigit(character) ||
            character is '-' or '.' or '_' or '~');

    private static RegistrationCompletionResult Failed(
        AuthenticationFailure failure) =>
        new(AuthenticationResult.Failed(failure), false);
}

public sealed class InMemoryRegistrationTransactionStore :
    IRegistrationTransactionStore
{
    private readonly object _syncRoot = new();
    private readonly Dictionary<Guid, RegistrationTransaction> _byId = [];
    private readonly Dictionary<Guid, Guid> _byRequestId = [];

    public ValueTask<RegistrationTransaction?> FindByRequestIdAsync(
        Guid requestId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_syncRoot)
        {
            return ValueTask.FromResult(
                _byRequestId.TryGetValue(requestId, out Guid id) &&
                _byId.TryGetValue(id, out RegistrationTransaction? value)
                    ? value
                    : null);
        }
    }

    public ValueTask AddAsync(
        RegistrationTransaction transaction,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_syncRoot)
        {
            if (_byRequestId.ContainsKey(transaction.RequestId))
                throw new InvalidOperationException(
                    "Registration request already exists.");

            _byId.Add(transaction.Id, transaction);
            _byRequestId.Add(transaction.RequestId, transaction.Id);
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask<RegistrationTransaction?> TryConsumeAsync(
        Guid transactionId,
        string authorizationCodeHash,
        string pkceChallenge,
        DateTime now,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_syncRoot)
        {
            if (!_byId.TryGetValue(
                    transactionId,
                    out RegistrationTransaction? value) ||
                value.ConsumedAtUtc != null ||
                value.ExpiresAtUtc <= now ||
                !CryptographicOperations.FixedTimeEquals(
                    Convert.FromHexString(value.AuthorizationCodeHash),
                    Convert.FromHexString(authorizationCodeHash)) ||
                !CryptographicOperations.FixedTimeEquals(
                    Encoding.ASCII.GetBytes(value.PkceChallenge),
                    Encoding.ASCII.GetBytes(pkceChallenge)))
            {
                return ValueTask.FromResult<RegistrationTransaction?>(null);
            }

            value.ConsumedAtUtc = now;
            return ValueTask.FromResult<RegistrationTransaction?>(value);
        }
    }
}

public sealed class DevelopmentRegistrationPortal(string baseUrl) :
    IRegistrationPortal
{
    public string CreateRegistrationUrl(Guid transactionId) =>
        $"{baseUrl.TrimEnd('/')}?transactionId={transactionId:D}";
}
