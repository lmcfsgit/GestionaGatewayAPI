# Gestiona Gateway API

ASP.NET Core gateway for the Gestiona API. It exposes endpoints for processes,
documents, thirds, activities, queue connectors, and add-on authorizations while
keeping Gestiona-specific HTTP and response handling in a reusable core library.

The current application version is **1.9.0**. See [CHANGELOG.md](CHANGELOG.md) for
the release history.

### Requirements

- .NET 8 SDK. The repository's `global.json` selects SDK 8.0.425 or a compatible
  later patch.
- Access to a Gestiona API instance and the required access tokens.

### Project structure

- `GestionaGatewayAPI.csproj` - ASP.NET Core API, controllers, middleware, and
  application startup.
- `GestionaGateway.Core/` - Gestiona client, service interfaces, implementations,
  configuration, and shared models.
- `GestionaGatewayAPI.Tests/` - xUnit test suite.

### Configuration

The API reads Gestiona settings from the `Gestiona` configuration section:

| Setting | Environment variable | Purpose |
| --- | --- | --- |
| `Gestiona:GestionaApiBaseUrl` | `Gestiona__GestionaApiBaseUrl` | Base URL of the Gestiona API. |
| `Gestiona:AccessToken` | `Gestiona__AccessToken` | Default Gestiona access token. |
| `Gestiona:AddonToken` | `Gestiona__AddonToken` | Token used by add-on authorization operations. |

Use environment variables for secrets and do not commit access tokens. Requests
that support `X-User-Access-Token` use that header in preference to the
configured default access token.

### Build and run

Restore and build the solution:

```powershell
dotnet restore GestionaGatewayAPI.sln
dotnet build GestionaGatewayAPI.sln
```

Run the API locally:

```powershell
dotnet run --project GestionaGatewayAPI.csproj
```

The development profiles listen on `http://localhost:5123` and
`https://localhost:7217`. Swagger UI is available in the Development environment
at `/swagger`.

Example requests can be run from `GestionaGatewayAPI.http` in an editor with HTTP
file support.

### Tests

Run the complete test suite with:

```powershell
dotnet test GestionaGatewayAPI.sln
```

### Logging

The application uses Serilog for console and rolling file logging. Local log
files are written under `logs/`, which is excluded from Git.

### Publishing

The PowerShell publishing script builds the API in Release configuration and
writes the deployable output to `C:\publish\GestionaGatewayAPI`:

```powershell
.\publish.ps1
```

Pass `-Configuration Debug` when a Debug publish is required.

#### IIS installation

Compress the published application files into a ZIP, then run the installer from
an elevated PowerShell session:

```powershell
.\Install-IIS.ps1 `
    -ZipPath C:\publish\GestionaGatewayAPI.zip `
    -Port 8080 `
    -InstallPath C:\inetpub\GestionaGatewayAPI `
    -SiteName GestionaGatewayAPI
```

The optional `SiteName` parameter defaults to `GestionaGatewayAPI`. Use a
different site name, port, and installation path to run another installation
alongside it. The script creates a matching website and application pool, adds an
HTTP binding for the requested port, and grants the application pool permission
to write application logs.
