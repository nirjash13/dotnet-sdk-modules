using System.Threading;
using System.Threading.Tasks;
using Ai.Infrastructure.Safety;
using FluentAssertions;
using Xunit;

namespace SaasBuilder.IntegrationTests.Ai;

/// <summary>PII redaction round-trip tests.</summary>
public sealed class PiiRedactorTests
{
    private readonly RegexPiiRedactor _redactor = new();

    [Fact]
    public async Task RedactPiiAsync_WhenTextContainsEmail_ReplacesWithToken()
    {
        // Arrange
        string input = "Contact me at alice@example.com for details.";

        // Act
        string result = await _redactor.RedactPiiAsync(input, CancellationToken.None);

        // Assert
        result.Should().Contain("[REDACTED:EMAIL]");
        result.Should().NotContain("alice@example.com");
    }

    [Fact]
    public async Task RedactPiiAsync_WhenTextContainsSsn_ReplacesWithToken()
    {
        // Arrange — SSN format: 123-45-6789
        string input = "My SSN is 123-45-6789, please keep it safe.";

        // Act
        string result = await _redactor.RedactPiiAsync(input, CancellationToken.None);

        // Assert
        result.Should().Contain("[REDACTED:SSN]");
        result.Should().NotContain("123-45-6789");
    }
}
