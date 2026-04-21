using System;
using System.Net.Http;
using System.Threading.Tasks;
using NBomber.Contracts;
using NBomber.CSharp;

namespace Dispatch.Benchmarks;

/// <summary>
/// NBomber micro-benchmarks comparing in-proc Mediator dispatch vs out-of-proc Bus dispatch.
///
/// Scenario A — In-process Mediator:
///   Posts to POST /api/v1/ledger/accounts/{id}/transactions with X-Dispatch-Mode: mediator.
///   Measures pure handler overhead without transport serialization.
///
/// Scenario B — Out-of-process Bus (HTTP):
///   Posts to the same endpoint without X-Dispatch-Mode, triggering the MassTransit
///   bus publish + consumer round-trip.
///   Measures total latency including network + serialization + consumer processing.
///
/// Both scenarios report mean + p95 in NBomber's default stdout report format.
///
/// NOTE: The actual in-proc vs bus routing is controlled by the chassis host
/// configuration (Transport__Mode=InMemory vs Transport__Mode=Bus). Run two
/// separate instances — one per mode — to compare. See README.md.
/// </summary>
public static class DispatcherBenchmarks
{
    private static readonly string BaseUrl =
        Environment.GetEnvironmentVariable("BASE_URL") ?? "http://localhost:5000";

    private static readonly string TenantId =
        Environment.GetEnvironmentVariable("TENANT_ID") ?? "00000000-0000-0000-0000-000000000001";

    private static readonly string BearerToken =
        Environment.GetEnvironmentVariable("BEARER_TOKEN") ?? string.Empty;

    /// <summary>
    /// Builds the NBomber scenario for in-process Mediator dispatch.
    /// Sends X-Dispatch-Mode: mediator header to signal the host to route via in-proc path.
    /// </summary>
    public static ScenarioProps BuildInProcMediatorScenario(HttpClient httpClient)
    {
        return Scenario.Create("in_proc_mediator", async _ =>
        {
            var accountId = Guid.NewGuid();
            var payload = BuildTransactionPayload("mediator");

            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                $"{BaseUrl}/api/v1/ledger/accounts/{accountId}/transactions")
            {
                Content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json"),
            };

            if (!string.IsNullOrEmpty(BearerToken))
            {
                request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {BearerToken}");
            }

            request.Headers.TryAddWithoutValidation("X-Tenant-Id", TenantId);
            request.Headers.TryAddWithoutValidation("X-Dispatch-Mode", "mediator");

            try
            {
                var response = await httpClient.SendAsync(request);

                return response.IsSuccessStatusCode
                    ? Response.Ok()
                    : Response.Fail(message: $"HTTP {(int)response.StatusCode}");
            }
            catch (Exception ex)
            {
                return Response.Fail(message: ex.Message);
            }
        })
        .WithWarmUpDuration(TimeSpan.FromSeconds(10))
        .WithLoadSimulations(
            Simulation.KeepConstant(copies: 10, during: TimeSpan.FromSeconds(60))
        );
    }

    /// <summary>
    /// Builds the NBomber scenario for out-of-process Bus dispatch.
    /// Posts without X-Dispatch-Mode, causing chassis to route via MassTransit bus.
    /// </summary>
    public static ScenarioProps BuildOutOfProcBusScenario(HttpClient httpClient)
    {
        return Scenario.Create("out_of_proc_bus", async _ =>
        {
            var accountId = Guid.NewGuid();
            var payload = BuildTransactionPayload("bus");

            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                $"{BaseUrl}/api/v1/ledger/accounts/{accountId}/transactions")
            {
                Content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json"),
            };

            if (!string.IsNullOrEmpty(BearerToken))
            {
                request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {BearerToken}");
            }

            request.Headers.TryAddWithoutValidation("X-Tenant-Id", TenantId);

            try
            {
                var response = await httpClient.SendAsync(request);

                return response.IsSuccessStatusCode
                    ? Response.Ok()
                    : Response.Fail(message: $"HTTP {(int)response.StatusCode}");
            }
            catch (Exception ex)
            {
                return Response.Fail(message: ex.Message);
            }
        })
        .WithWarmUpDuration(TimeSpan.FromSeconds(10))
        .WithLoadSimulations(
            Simulation.KeepConstant(copies: 10, during: TimeSpan.FromSeconds(60))
        );
    }

    private static string BuildTransactionPayload(string dispatchMode)
    {
        var idempotencyKey = $"nbomber-{dispatchMode}-{Guid.NewGuid():N}";
        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");

        return $$"""
            {
              "idempotencyKey": "{{idempotencyKey}}",
              "transactionDate": "{{today}}",
              "currency": "USD",
              "reference": "NBOMBER-BENCH-{{dispatchMode.ToUpperInvariant()}}",
              "description": "NBomber dispatcher benchmark — {{dispatchMode}} path",
              "lines": [
                {
                  "accountCode": "ACC-001001",
                  "debit": "100.00",
                  "credit": null,
                  "description": "Benchmark debit line"
                },
                {
                  "accountCode": "ACC-001002",
                  "debit": null,
                  "credit": "100.00",
                  "description": "Benchmark credit line"
                }
              ],
              "metadata": {
                "source": "nbomber-dispatch-benchmarks",
                "dispatchMode": "{{dispatchMode}}"
              }
            }
            """;
    }
}
