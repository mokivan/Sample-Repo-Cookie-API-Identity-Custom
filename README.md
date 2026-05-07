[![.NET 10 LTS](https://img.shields.io/badge/.NET-10%20LTS-512BD4)](https://dotnet.microsoft.com/)
[![Integration Tests](https://img.shields.io/badge/tests-integration%20%2B%20CI-0A7E3B)](.github/workflows/ci.yml)

# Cookie authentication with custom ASP.NET Identity stores

This repository is a public sample that demonstrates:

- Cookie-based authentication for an API.
- Custom `UserStore` and `RoleStore` implementations.
- Role-based and permission-based authorization.
- Server-side session storage backed by Redis to keep the auth cookie small.
- Two API instances behind nginx sharing both session state and data protection keys.

The project is intentionally educational, but the implementation is polished enough to be a serious public sample. It is not meant to be copied blindly into production as-is.

## What is deliberately custom here

The following pieces are custom on purpose because they help explain how ASP.NET Identity works internally:

- `UserStore` and `RoleStore`
- Permission-based authorization using a custom authorization requirement and policy provider
- Distributed `ITicketStore` implementation for server-side sessions
- A minimal domain model (`AppUser`, `AppRole`, `AppPermission`) instead of inheriting from `IdentityUser`

## What is good practice here

- Migration-based schema management with EF Core
- Unique indexes for normalized usernames, emails, and role names
- Explicit cookie settings and predictable API auth responses (`401` / `403`)
- Safer defaults for cookies, forwarded headers, and operational debug headers
- Session ownership checks before targeted logout
- Integration tests against PostgreSQL and Redis with Testcontainers
- CI that restores, builds, tests, and smoke-builds the Docker image

## What not to copy directly into production

- There is no account recovery flow, email confirmation flow, MFA, lockout tuning, audit trail, or admin UI.
- Roles and permissions are seeded for demonstration, not managed dynamically.
- The sample optimizes for clarity over abstraction reuse.
- Local/demo conveniences are intentionally configuration-driven and should stay disabled in production.

## Solution layout

- `Controllers`
  Contains the auth endpoints plus sample role/permission-protected endpoints.
- `DataAccess`
  EF Core `DbContext`, migration metadata, and design-time context factory.
- `Identity/CustomModel`
  The custom user, role, permission, and claim type definitions.
- `Identity/DTO`
  Request and response models for the auth API.
- `Identity/Filters`
  Custom permission authorization requirement, handler, policy provider, and attribute.
- `Identity/Stores`
  The custom stores that back Identity and the Redis ticket/session store.
- `Postman`
  A collection that mirrors the main demo flow.
- `TestIdentity.IntegrationTests`
  End-to-end tests using `WebApplicationFactory` plus PostgreSQL and Redis containers.

## Reference docs

- [docs/dotnet-knowledge-base.md](docs/dotnet-knowledge-base.md)
  Practical .NET and C# tips, best practices, and architecture guidance derived from current Microsoft guidance and a curated Medium reference article.

## Run locally

### Prerequisites

- .NET 10 SDK
- PostgreSQL
- Redis
- Docker Desktop if you want to run the integration tests or the full compose stack

### Local API run

The repository already includes [launchSettings.json](Properties/launchSettings.json), which runs the app in the `Local` environment on `http://localhost:5098`.

1. Start PostgreSQL and Redis locally.
2. Make sure the values in [appsettings.Local.json](appsettings.Local.json) match your local services.
3. Run:

```bash
dotnet restore
dotnet build
dotnet run --launch-profile http
```

At startup, non-production environments automatically apply EF Core migrations.

## Security configuration

The sample now separates secure defaults from local/demo behavior with a `Security` section in configuration.

Default behavior from [appsettings.json](appsettings.json):

- `AllowSelfAssignedRoles = false`
  Anonymous registration does not honor role IDs sent by the client.
- `ExposeMachineDebugHeaders = false`
  `X-Machine-Name` and `X-Machine-Ip` are off by default.
- `RequireHttpsForAuthCookie = true`
  Authentication cookies default to `SecurePolicy = Always`.
- `TrustedProxies` and `TrustedNetworks` are empty
  Forwarded headers are enabled, but trust should only be extended explicitly when running behind a known reverse proxy.

Local/demo behavior from [appsettings.Local.json](appsettings.Local.json):

- `AllowSelfAssignedRoles = true`
  Keeps the public demo flow simple by allowing role selection at registration time.
- `ExposeMachineDebugHeaders = true`
  Useful when demonstrating multiple API instances behind nginx.
- `RequireHttpsForAuthCookie = false`
  Allows local HTTP development without TLS termination.

Testing behavior:

- The integration test host enables self-assigned roles and relaxes the cookie HTTPS requirement so the test flow can exercise role and permission scenarios over the in-memory test server.

Behind a reverse proxy:

- Set `Security:TrustedProxies` to explicit IP addresses, or `Security:TrustedNetworks` to explicit CIDR ranges, for the proxy layer that is allowed to send `X-Forwarded-*` headers.
- Do not clear proxy trust lists globally in production.
- Keep `RequireHttpsForAuthCookie = true` when TLS is terminated at the proxy.

## Run with Docker Compose

1. Copy `.env.example` to `.env`.
2. Adjust the PostgreSQL values if needed.
3. Build and start the stack:

```bash
docker compose -f docker-compose-build.yaml build
docker compose up
```

The compose file starts:

- PostgreSQL
- Redis
- two API containers
- nginx as reverse proxy

Add `api.identity.local` to your hosts file:

```text
127.0.0.1 api.identity.local
```

Then call the API via `http://api.identity.local`.

## Recommended demo flow

This flow assumes the `Local` environment, where self-assigned roles are intentionally enabled for demonstration.

1. Register a user with roles `1` and `2`.
2. Log in with that user.
3. Call `/me` to inspect authentication state, roles, permissions, and current SID.
4. Call `/sessions` to see active distributed sessions.
5. Call `/api/test-role` to verify role-based authorization.
6. Call `/api/weatherforecast/single` and `POST /api/weatherforecast` to verify permission-based authorization.
7. Create a second login for the same user, then revoke one session with `POST /logout?sid=...`.
8. Finish with `POST /logout-all`.

## Tests and CI

Build:

```bash
dotnet build TestIdentityCookie.sln
```

Integration tests:

```bash
dotnet test TestIdentity.IntegrationTests/TestIdentity.IntegrationTests.csproj
```

The integration tests require a working Docker daemon because they spin up PostgreSQL and Redis with Testcontainers.

GitHub Actions runs:

- restore
- build with warnings as errors
- integration tests
- Docker image smoke build

## Postman

The collection is available at [Postman/C# Custom-ish Identity implementation.postman_collection.json](Postman/C%23%20Custom-ish%20Identity%20implementation.postman_collection.json).

Use the `URL` variable for either:

- `http://localhost:5098`
- `http://api.identity.local`

## License

This project is licensed under the [MIT License](LICENSE).
