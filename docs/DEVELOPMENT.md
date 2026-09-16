# Felion Development

## Prerequisites

- .NET SDK `10.0.400`.
- PostgreSQL is optional for the foundation milestone. It becomes required when persistence features are enabled.

## Configuration

Use `src/Felion.Host/appsettings.json` as the local configuration template, or configure values through environment variables and .NET user-secrets.

Secrets must be supplied through environment variables or .NET user-secrets. Never commit a Discord token, Google client secret, or database password.

Development and Testing use single-line human-readable console logs. Other environments use structured JSON console logs for centralized log ingestion.

The Discord Gateway is disabled when `Discord:Token` is empty. When a token is configured, `Discord:GuildId` must contain the single positive guild snowflake used by Felion.

Google Workspace authentication reads `Authentication:Google:ClientId` and `Authentication:Google:ClientSecret` from environment variables or user-secrets. `Authentication:Google:WorkspaceDomain` is an optional domain restriction/hint; it never grants access by itself. The OAuth callback must resolve the normalized Google email to an active `Member` before issuing a Felion cookie. Configure local secrets with:

```powershell
dotnet user-secrets set "Authentication:Google:ClientId" "<client-id>" --project src/Felion.Host
dotnet user-secrets set "Authentication:Google:ClientSecret" "<client-secret>" --project src/Felion.Host
```

Use `GET /api/v1/auth/login` to start the flow, `GET /api/v1/auth/me` to inspect the current application identity, and `POST /api/v1/auth/logout` to clear the session. In Development and Testing only, `X-Felion-Actor-Member-Id` remains a compatibility header; it is resolved against an active Member and is rejected outside those environments.

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
