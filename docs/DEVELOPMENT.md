# Felion Development

## Prerequisites

- .NET SDK `10.0.400`.
- PostgreSQL is optional for the foundation milestone. It becomes required when persistence features are enabled.

## Configuration

Use `src/Felion.Host/appsettings.json` as the local configuration template, or configure values through environment variables and .NET user-secrets.

Secrets must be supplied through environment variables or .NET user-secrets. Never commit a Discord token, Google client secret, or database password.

Development and Testing use single-line human-readable console logs. Other environments use structured JSON console logs for centralized log ingestion.

The Discord Gateway is disabled when `Discord:Token` is empty. When a token is configured, `Discord:GuildId` must contain the single positive guild snowflake used by Felion.

Until Google Workspace authentication is implemented, Member management endpoints can be exercised only in Development or Testing with `X-Felion-Actor-Member-Id` set to an active Core/Admin Member ID. This header is intentionally rejected in other environments and is not an authentication mechanism for production.

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
