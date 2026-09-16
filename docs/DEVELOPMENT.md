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

PostgreSQL integration tests are skipped unless `FELION_POSTGRES_TEST_CONNECTION` is set. The fixture creates, migrates and removes a random `felion_test_*` database, so the configured PostgreSQL login must have permission to create and drop databases. For example:

```powershell
$env:FELION_POSTGRES_TEST_CONNECTION = "Host=localhost;Port=5432;Database=postgres;Username=postgres;Password=<password>"
dotnet test tests/Felion.IntegrationTests/Felion.IntegrationTests.csproj --configuration Release
```

The host exposes:

- `/health/live` for process liveness.
- `/health/ready` for configured dependencies such as PostgreSQL and Discord Gateway.
- `/openapi/v1.json` in the Development environment.

## Abuse protection

Felion applies process-local fixed-window limits through the application services so the same rules cover HTTP and Discord transports: five Discord-link attempts per Discord user in ten minutes, and ten evaluation submissions per reviewer in one minute. HTTP evaluation rejections return `429` with `Retry-After`; Discord linking replies ephemerally. The limiter does not persist counters and therefore assumes one application process. A future multi-instance deployment needs a shared limiter store before relying on these limits across replicas.

## Transport security and reverse proxies

The application session cookie is `HttpOnly`, `Secure` and `SameSite=Lax`; authentication cookies are therefore issued only over HTTPS. Outside Development and Testing, the host enables HSTS and HTTPS redirection. It also removes Kestrel's `Server` header, limits total request headers to 32 KiB with a 30-second header timeout, and sends these response headers: `Content-Security-Policy`, `Permissions-Policy`, `Referrer-Policy`, `X-Content-Type-Options` and `X-Frame-Options`.

For a production reverse proxy, configure forwarded headers only for the known proxy networks/addresses and ensure that configuration executes before HTTPS redirection. Do not trust arbitrary `X-Forwarded-*` request headers. Alternatively, have the edge proxy terminate TLS and enforce the HTTPS redirect itself. Set `AllowedHosts` in production to the deployed public hostnames; the checked-in `*` value is only a local-development default. Cookie-authenticated JSON mutation anti-forgery remains deferred by ADR-018.

Milestone 0 does not create database entities or migrations. EF Core migrations start with the first persistence/domain milestone.
