# Portable Release Inventory

## Product identity

- Product: SQL Server Diagnostics
- Application version: `0.1.0`, from `ApplicationInfo.ApplicationVersion` and assembly metadata
- Report schema version: `1.0`
- Framework: .NET Framework 4.8
- Platform: Windows x64
- Build configuration: `Release|x64`
- UI technology: Windows Forms

No new version was assigned for Phase 7.2. The project already has a centralized application version.

## Portable package

Run from the repository root:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\release\New-PortablePackage.ps1
```

The script rebuilds `Release|x64` with the installed Visual Studio MSBuild, cleans and recreates `artifacts\SqlServerDiagnostics-portable`, and validates all copied content. Use `-SkipBuild` only when a separately verified Release output is intentionally being consumed.

Required package contents:

- `SqlServerDiagnostics.exe`
- `SqlServerDiagnostics.exe.config`
- `TheMaxLab.SqlServerDiagnostics.Core.dll`
- `TheMaxLab.SqlServerDiagnostics.Diagnostics.dll`
- `TheMaxLab.SqlServerDiagnostics.Infrastructure.dll`
- `TheMaxLab.SqlServerDiagnostics.Reporting.dll`
- `diagnostics\manifest.json`
- the 26 SQL files referenced by the manifest beneath `diagnostics`

The package deliberately excludes PDBs, tests, test configuration/results, source, project files, credentials, profiles, logs, local databases, temporary files, installers, and ZIP archives. User profiles, protected credentials, and logs are created only at runtime under the current user's local application-data directory and are never package inputs.

## Runtime requirements

- 64-bit Windows workstation
- .NET Framework 4.8
- network/client access to a separately managed SQL Server environment
- permissions required by the selected diagnostics

Nothing is installed on SQL Server. The application remains read-only with respect to persistent database data; suggested SQL is output-only data.

## Validation boundary

The corrected `database-sizes` and `missing-primary-keys` diagnostics and the existing integration pipeline have live evidence on SQL Server 2019 Express LocalDB 15.0.4382.1, compatibility level 150, against `master`. This is not certification of boxed SQL Server versions, Azure SQL Database, Azure SQL Managed Instance, other editions, databases, collations, or compatibility levels. See `docs/sql-compatibility-matrix.md`.

## Known limitations

- The portable directory is unsigned and has no installer or automatic update mechanism.
- Git provenance cannot be established from the supplied directory because `.git` metadata is absent.
- End users must protect exported reports because diagnostic evidence can contain operationally sensitive database information.