# xstackApi

Minimal API backend for XStack: authentication, users, chats, messages and a
SignalR hub at `/hubs/chat`.

## Requirements

- .NET 10 SDK
- PostgreSQL 14+ with the schema from [`../sql.sql`](../sql.sql)

## Local configuration

Secrets are **not** stored in the repository. The API refuses to start without
`Jwt:SigningKey` (at least 32 UTF-8 bytes).

Set it once for local development with user-secrets:

```bash
dotnet user-secrets set "Jwt:SigningKey" "$(openssl rand -base64 48)"
```

Alternatively use environment variables (recommended for production):

```bash
export Jwt__SigningKey="<random 32+ byte secret>"
export ConnectionStrings__DefaultConnection="Host=...;Database=xstack;Username=...;Password=..."
```

Apply the database schema:

```bash
psql -d xstack -f ../sql.sql
```

## Run

```bash
dotnet run
```

OpenAPI/Scalar UI is available in Development at `/swagger`.

## Tests

```bash
dotnet test xstackApi.slnx
```

## Notes

- Access tokens are signed with `Jwt:SigningKey`; rotating the key invalidates
  all existing tokens.
- SignalR chat rooms are membership-checked in `ChatHub.JoinChat`.
- `ChatRoomService` evicts removed/leaving members from live rooms. Its
  connection registry is in-memory and therefore single-instance; a
  multi-node deployment needs a shared registry/backplane.
