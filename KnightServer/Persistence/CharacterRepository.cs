using System.Data;
using KnightOnline.Client.Shared.Packets;
using KnightOnline.Server.Characters;
using KnightOnline.Server.Configuration;
using KnightOnline.Server.Persistence.Entities;
using KnightOnline.Server.Time;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace KnightOnline.Server.Persistence;

public sealed class CharacterRepository(
    DbContextOptions<KnightDbContext> options,
    CharacterOptions characterOptions,
    ICharacterCreationCatalog catalog,
    CharacterNamePolicy namePolicy,
    IServerClock clock)
{
    public async Task EnsureAccountExistsAsync(string accountKey)
    {
        await using var db = new KnightDbContext(options);
        AccountEntity? existing = await db.Accounts.SingleOrDefaultAsync(
            account => account.AccountKey == accountKey);
        if (existing != null)
        {
            if (existing.Kind != AccountKind.Development)
            {
                existing.Kind = AccountKind.Development;
                await db.SaveChangesAsync();
            }
            return;
        }

        db.Accounts.Add(new AccountEntity
        {
            AccountKey = accountKey,
            Kind = AccountKind.Development,
            CreatedAtUtc = clock.UtcNow,
        });

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            // Another connection created the development account first.
        }
    }

    public async Task<IReadOnlyList<CharacterSummaryPacket>> ListAsync(
        string accountKey)
    {
        await using var db = new KnightDbContext(options);
        CharacterEntity[] characters = await db.Characters
            .AsNoTracking()
            .Include(character => character.Appearances)
            .Where(character =>
                character.Account.AccountKey == accountKey &&
                character.ServerId == characterOptions.ServerId)
            .OrderBy(character => character.SlotIndex)
            .ToArrayAsync();

        return characters.Select(ToSummary).ToArray();
    }

    public async Task<CharacterSummaryPacket?> FindOwnedAsync(
        string accountKey,
        int characterId)
    {
        await using var db = new KnightDbContext(options);
        CharacterEntity? character = await db.Characters
            .AsNoTracking()
            .Include(value => value.Appearances)
            .SingleOrDefaultAsync(value =>
                value.Id == characterId &&
                value.ServerId == characterOptions.ServerId &&
                value.Account.AccountKey == accountKey);

        return character == null ? null : ToSummary(character);
    }

    public async Task<bool> IsNameAvailableAsync(string normalizedName)
    {
        await using var db = new KnightDbContext(options);
        return !await db.Characters.AsNoTracking().AnyAsync(character =>
            character.ServerId == characterOptions.ServerId &&
            character.NormalizedName == normalizedName);
    }

    public async Task<CreateCharacterResponsePacket> CreateAsync(
        string accountKey,
        CreateCharacterRequestPacket request,
        CancellationToken cancellationToken)
    {
        if (request.RequestId == Guid.Empty)
            return Failure(
                CreateCharacterResult.DuplicateRequest,
                "A non-empty RequestId is required.",
                request.RequestId);
        if (request.SlotIndex is < 1 or > 3)
            return Failure(
                CreateCharacterResult.InvalidSlot,
                "SlotIndex must be between 1 and 3.",
                request.RequestId);

        CharacterNameValidationResult validName =
            namePolicy.Validate(request.CharacterName);
        if (!validName.IsValid)
            return Failure(
                CreateCharacterResult.InvalidName,
                validName.Message,
                request.RequestId);

        CharacterCreationValidationResult validCreation =
            catalog.Validate(request);
        if (!validCreation.IsValid)
            return Failure(
                validCreation.Result,
                validCreation.Message,
                request.RequestId);

        await using var db = new KnightDbContext(options);
        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted,
            cancellationToken);

        AccountEntity? account = await db.Accounts.SingleOrDefaultAsync(
            value => value.AccountKey == accountKey,
            cancellationToken);
        if (account == null)
        {
            return Failure(
                CreateCharacterResult.Unauthorized,
                "The authenticated account no longer exists.",
                request.RequestId);
        }

        CharacterCreationRequestEntity? prior =
            await db.CharacterCreationRequests
                .AsNoTracking()
                .Include(value => value.Character)
                .ThenInclude(value => value!.Appearances)
                .SingleOrDefaultAsync(
                    value => value.RequestId == request.RequestId,
                    cancellationToken);
        if (prior != null)
            return prior.AccountId == account.Id
                ? ToStoredResponse(prior)
                : Failure(
                    CreateCharacterResult.DuplicateRequest,
                    "RequestId was already used.",
                    request.RequestId);

        if (await db.Characters.CountAsync(
                value =>
                    value.AccountId == account.Id &&
                    value.ServerId == characterOptions.ServerId,
                cancellationToken) >= characterOptions.MaximumPerAccount)
        {
            return Failure(
                CreateCharacterResult.CharacterLimitReached,
                $"An account can have at most " +
                $"{characterOptions.MaximumPerAccount} characters.",
                request.RequestId);
        }

        DateTime utcNow = clock.UtcNow;
        var character = new CharacterEntity
        {
            AccountId = account.Id,
            ServerId = characterOptions.ServerId,
            SlotIndex = request.SlotIndex,
            Name = validName.DisplayName,
            NormalizedName = validName.NormalizedName,
            Level = characterOptions.InitialLevel,
            CurrentClassDefinitionId = request.ClassDefinitionId,
            BodyTypeDefinitionId = request.BodyTypeDefinitionId,
            CurrentMapDefinitionId =
                characterOptions.StartingMapDefinitionId,
            CurrentSpawnPointId = characterOptions.StartingSpawnPointId,
            PositionX = characterOptions.SpawnPositionX,
            PositionY = characterOptions.SpawnPositionY,
            CreatedAtUtc = utcNow,
        };

        foreach (AppearanceSelectionPacket selection in
                 request.AppearanceSelections)
        {
            character.Appearances.Add(new CharacterAppearanceEntity
            {
                SlotDefinitionId = selection.SlotDefinitionId,
                AppearanceDefinitionId = selection.OptionDefinitionId,
                UpdatedAtUtc = utcNow,
            });
        }

        character.TutorialProgress.Add(
            new CharacterTutorialProgressEntity
            {
                TutorialDefinitionId =
                    characterOptions.StartingTutorialDefinitionId,
                CurrentStepDefinitionId =
                    characterOptions.StartingTutorialStepDefinitionId,
                State = TutorialProgressState.NotStarted,
                UpdatedAtUtc = utcNow,
            });
        db.Characters.Add(character);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            var storedRequest = new CharacterCreationRequestEntity
            {
                RequestId = request.RequestId,
                AccountId = account.Id,
                CharacterId = character.Id,
                ResultCode = CreateCharacterResult.Success.ToString(),
                ResultMessage = "Character created.",
                CreatedAtUtc = utcNow,
            };
            db.CharacterCreationRequests.Add(storedRequest);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return new CreateCharacterResponsePacket(
                CreateCharacterResult.Success,
                storedRequest.ResultMessage,
                request.RequestId,
                ToSummary(character));
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            await transaction.RollbackAsync(cancellationToken);
            return await ResolveUniqueConflictAsync(
                account.Id,
                request,
                validName.NormalizedName,
                cancellationToken);
        }
    }

    private async Task<CreateCharacterResponsePacket>
        ResolveUniqueConflictAsync(
            int accountId,
            CreateCharacterRequestPacket request,
            string normalizedName,
            CancellationToken cancellationToken)
    {
        await using var db = new KnightDbContext(options);
        CharacterCreationRequestEntity? duplicate =
            await db.CharacterCreationRequests
                .AsNoTracking()
                .Include(value => value.Character)
                .ThenInclude(value => value!.Appearances)
                .SingleOrDefaultAsync(
                    value => value.RequestId == request.RequestId,
                    cancellationToken);
        if (duplicate != null)
            return duplicate.AccountId == accountId
                ? ToStoredResponse(duplicate)
                : Failure(
                    CreateCharacterResult.DuplicateRequest,
                    "RequestId was already used.",
                    request.RequestId);

        if (await db.Characters.AsNoTracking().AnyAsync(
                value =>
                    value.ServerId == characterOptions.ServerId &&
                    value.NormalizedName == normalizedName,
                cancellationToken))
        {
            return Failure(
                CreateCharacterResult.NameAlreadyTaken,
                "That character name already exists.",
                request.RequestId);
        }

        return Failure(
            CreateCharacterResult.SlotAlreadyOccupied,
            "That character slot is already occupied.",
            request.RequestId);
    }

    private static CreateCharacterResponsePacket ToStoredResponse(
        CharacterCreationRequestEntity stored)
    {
        if (!Enum.TryParse(
                stored.ResultCode,
                out CreateCharacterResult result))
        {
            result = CreateCharacterResult.DuplicateRequest;
        }

        return new CreateCharacterResponsePacket(
            result,
            stored.ResultMessage,
            stored.RequestId,
            stored.Character == null ? null : ToSummary(stored.Character));
    }

    private static CharacterSummaryPacket ToSummary(CharacterEntity character) =>
        new(
            character.Name,
            character.Id,
            character.Level,
            character.SlotIndex,
            character.CurrentClassDefinitionId,
            character.BodyTypeDefinitionId,
            character.Appearances
                .OrderBy(value => value.SlotDefinitionId)
                .Select(value => new AppearanceSelectionPacket(
                    value.SlotDefinitionId,
                    value.AppearanceDefinitionId))
                .ToArray(),
            character.CurrentMapDefinitionId,
            character.CurrentSpawnPointId,
            character.PositionX,
            character.PositionY,
            character.TotalExperience);

    private static CreateCharacterResponsePacket Failure(
        CreateCharacterResult result,
        string message,
        Guid requestId) =>
        new(result, message, requestId);

    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
        };
}
