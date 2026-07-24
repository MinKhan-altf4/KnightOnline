# KnightServer - Game Server

## PostgreSQL development setup

KnightServer uses PostgreSQL through EF Core/Npgsql. The first persistence
vertical slice stores a development account and its characters. Authentication
is not implemented yet, so every local connection currently uses the account
key `local-dev`.

### 1. Create the application role and database

From the repository root:

```powershell
psql -U postgres -h localhost -f KnightServer/Database/bootstrap.sql
```

The script asks for a new password for `knightonline_app`. This is separate from
the PostgreSQL administrator password.

### 2. Store the connection string outside Git

Replace `YOUR_APP_PASSWORD` locally:

```powershell
dotnet user-secrets set "ConnectionStrings:KnightOnline" "Host=localhost;Port=5432;Database=knightonline_dev;Username=knightonline_app;Password=YOUR_APP_PASSWORD" --project KnightServer
```

For CI or deployment, use this environment variable instead:

```text
KNIGHTONLINE_ConnectionStrings__KnightOnline
```

Never commit a real password or connection string.

### 3. Restore tools and apply migrations

```powershell
dotnet tool restore
dotnet tool run dotnet-ef database update --project KnightServer
```

KnightServer also applies pending migrations during startup. Applying them
explicitly here makes setup failures easier to diagnose.

### 4. Run

```powershell
dotnet run --project KnightServer
```

Expected startup output:

```text
[Server] Listening on port 7777.
```

### Persistence verification

1. Create a character from the Unity client.
2. Stop both Client and Server.
3. Start Server and reconnect.
4. The character must still appear with the same `CharacterId` and `Level`.

Current development constraints:

- Maximum four characters per account.
- Character names are globally unique and case-insensitive.
- All clients use `local-dev` until authentication is implemented.
