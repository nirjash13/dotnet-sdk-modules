// Chassis.SharedKernel net10.0 smoke test.
// Build success = the SharedKernel package is consumable from net10.0.

using System;
using Chassis.SharedKernel.Abstractions;
using Chassis.SharedKernel.Contracts;

namespace Chassis.SharedKernel.PackageTests.Net10;

internal static class SmokeTest
{
    internal static int Main()
    {
        // Result<T> success path
        var success = Result<int>.Success(1);
        if (!success.IsSuccess || success.Value != 1)
        {
            Console.Error.WriteLine("FAIL: Result<int>.Success(1)");
            return 1;
        }

        // Result<T> failure path
        var failure = Result<int>.Failure("error");
        if (!failure.IsFailure || failure.Error != "error")
        {
            Console.Error.WriteLine("FAIL: Result<int>.Failure()");
            return 1;
        }

        // Non-generic Result
        if (!Result.Success().IsSuccess)
        {
            Console.Error.WriteLine("FAIL: Result.Success()");
            return 1;
        }

        // Implicit operator
        Result<string> fromImplicit = "hello";
        if (!fromImplicit.IsSuccess)
        {
            Console.Error.WriteLine("FAIL: implicit operator Result<string>");
            return 1;
        }

        // Contracts
        _ = CorrelationHeaders.TenantId;
        _ = new CloudEventEnvelope(Guid.NewGuid().ToString(), "/chassis/smoke", "com.chassis.test.v1", null);

        Console.WriteLine("net10.0 smoke test passed.");
        return 0;
    }
}
