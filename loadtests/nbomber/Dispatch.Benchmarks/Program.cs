using System;
using System.Net.Http;
using Dispatch.Benchmarks;
using NBomber.CSharp;

// ── NBomber Dispatcher Benchmark Runner ───────────────────────────────────
//
// Compares in-proc Mediator dispatch vs out-of-proc Bus dispatch.
// Both scenarios target the running chassis host via HTTP.
//
// Prerequisites:
//   - Chassis host running: dotnet run --project src/Chassis.Host
//   - (Optional) Obtain a bearer token and set BEARER_TOKEN env var.
//   - For Bus mode: docker compose -f deploy/docker-compose.yml up -d rabbitmq
//
// Run:
//   dotnet run -c Release --project loadtests/nbomber/Dispatch.Benchmarks
//
// Env vars:
//   BASE_URL      Default: http://localhost:5000
//   TENANT_ID     Default: 00000000-0000-0000-0000-000000000001
//   BEARER_TOKEN  Default: "" (no auth — set this for protected endpoints)
//
// Output: NBomber stdout report with mean + p95 per scenario step.
//
// See README.md for the recommended two-run workflow to compare dispatcher modes.

Console.WriteLine("NBomber Dispatcher Benchmarks — Chassis v1");
Console.WriteLine($"Target: {Environment.GetEnvironmentVariable("BASE_URL") ?? "http://localhost:5000"}");
Console.WriteLine();

using var httpClient = new HttpClient
{
    Timeout = TimeSpan.FromSeconds(30),
};

var mediatorScenario = DispatcherBenchmarks.BuildInProcMediatorScenario(httpClient);
var busScenario = DispatcherBenchmarks.BuildOutOfProcBusScenario(httpClient);

NBomberRunner
    .RegisterScenarios(mediatorScenario, busScenario)
    .WithTestSuite("chassis_dispatcher_benchmarks")
    .WithTestName("mediator_vs_bus")
    .Run();
