# Integration.Framework48Bridge

A .NET Framework 4.8 console application that bridges legacy SOAP/XML events to RabbitMQ via the CloudEvents 1.0 envelope format.

## Prerequisites

- Windows host with .NET Framework 4.8 Developer Pack installed
- RabbitMQ broker accessible from the host

## Build

### Using `dotnet build` (requires .NET Framework reference assemblies)

```powershell
dotnet build integration\Integration.Framework48Bridge -f net48
```

If reference assemblies are not found, install the [.NET Framework 4.8 Developer Pack](https://dotnet.microsoft.com/download/dotnet-framework/net48) and retry.

### Using MSBuild directly

```powershell
msbuild integration\Integration.Framework48Bridge\Integration.Framework48Bridge.csproj `
  /p:Configuration=Release `
  /p:TargetFramework=net48
```

If MSBuild cannot resolve reference assemblies automatically, provide the path explicitly:

```powershell
msbuild integration\Integration.Framework48Bridge\Integration.Framework48Bridge.csproj `
  /p:Configuration=Release `
  /p:TargetFramework=net48 `
  /p:ReferenceAssemblyPath="C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.8"
```

## Run

Set environment variables (never put credentials in `app.config` for production):

```powershell
$env:RABBITMQ_HOST = "localhost"
$env:RABBITMQ_USER = "guest"
$env:RABBITMQ_PASS = "guest"
.\Integration.Framework48Bridge.exe
```

Paste legacy XML events on stdin:

```xml
<LegacyEvent type="order.created" source="/legacy/orders" id="abc-123">
  <Payload><Order><Id>42</Id></Order></Payload>
</LegacyEvent>
```

The bridge maps the event to a CloudEvents envelope, adds `ce-*` headers, and publishes to the `legacy-bridge` exchange with routing key `legacy.order.created`.

## Graceful shutdown

Press `Ctrl+C`. The bridge drains the current message (if any) and exits cleanly.

## Deferral note

This project targets `net48` and requires the .NET Framework Developer Pack on the build host. In environments where only the .NET 10 SDK is available (e.g., Linux CI), the `net48` target will not resolve reference assemblies and the build will fail. The project is therefore **excluded from `Chassis.sln`** in Phase 6. The source files are present on disk for use on Windows hosts with the Developer Pack installed. Add the project to the solution file manually once a Windows-based CI step is available.
