# Security

## Scope

The application is designed for a single person running it on a machine they control. It does not
currently provide authentication, multi-user roles, or multi-tenancy.

Do not expose it directly to the public internet until an authentication and authorization layer is
added.

## Secrets

API keys and credentials must not be stored in `AppSetting` rows.

`SettingKeys.SecretKeys` rejects:

- `ai.apiKey`
- `ai.apikey`
- `ai.key`
- `ai.token`

Use configuration or environment variables:

```powershell
$env:AI__ApiKey = "YOUR_API_KEY"
```

## Database

The default local configuration uses SQL Server Windows authentication. For a deployment with a
service account:

- Keep the credential outside source control.
- Grant least-privilege SQL Server permissions.
- Use encryption and `TrustServerCertificate` only with an appropriate certificate policy.
- Backup the database, because it contains decisions, profiles, observations, and AI data.

## Remote AI providers

Remote access is blocked unless `AI:AllowRemoteProviders=true`. A provider call is also rejected if
its sensitivity exceeds `AI:MaxRemoteSensitivity`.

The settings page explicitly tells the user that configuration is the secret boundary. The app does
not copy an API key into backups by storing it in the application settings table.

## AI proposals

The assistant cannot directly apply a change. It creates an `AIProposal` that is stored until the
person confirms it. Confirmation goes through the normal application service and domain invariants.

## Documents

Local document storage is configured by `DocumentStorage`:

- `RootPath` controls the filesystem location.
- `MaxFileSizeBytes` limits uploads.

The storage implementation should keep generated relative paths outside the app's source tree and
validate file names and content types before writing.

The domain record for a document stores its relative path, content type, size, and checksum. A
checksum helps detect file replacement or corruption.

## Data sensitivity

The application distinguishes data from assumptions, but no data is automatically harmless. Before
sending context to a remote provider:

- Remove data not needed for the question.
- Respect the sensitivity ceiling.
- Keep personal decisions local if remote processing is not necessary.

## Audit data

`AuditLog` records changes that matter. It is append-oriented, not a replacement for database
backups. A review is the place for a person's narrative of what happened; an audit log is the place
for the fact that it changed.

## Browser and web hosting

- Use HTTPS in deployment.
- Keep ASP.NET Core antiforgery enabled.
- Do not put production secrets in `appsettings.json`.
- Set a known allowed-host policy for production rather than `*`.

## Logging

Serilog writes console and rolling application logs. Do not log prompt contents, API keys, database
passwords, document contents, or raw user financial records. Source-generated logger messages keep
event IDs and avoid ad hoc string formatting.

## Backup warning

A backup contains the user's decisions and observations. Protect backups with the same care as the
database. Encryption at rest, access control, and retention should be defined at deployment time.
