# Felion Development

## Prerequisites

- .NET SDK `10.0.400`.
- PostgreSQL is optional for the foundation milestone. It becomes required when persistence features are enabled.

## Configuration

Copy `appsettings.example.json` or configure environment variables from `.env.example`.

Secrets must be supplied through environment variables or .NET user-secrets. Never commit a Discord token, Google client secret, or database password.

The Discord Gateway is disabled when `Discord:Token` is empty. When a token is configured, `Discord:GuildId` must contain the single positive guild snowflake used by Felion.

## Verification commands

```powershell
dotnet restore Felion.slnx
dotnet format Felion.slnx --verify-no-changes --no-restore
dotnet build Felion.slnx --configuration Release --no-restore
dotnet test Felion.slnx --configuration Release --no-build
```

The host exposes:

- `/health/live` for process liveness.
- `/health/ready` for configured dependencies such as PostgreSQL and Discord Gateway.
- `/openapi/v1.json` in the Development environment.

Milestone 0 does not create database entities or migrations. EF Core migrations start with the first persistence/domain milestone.
