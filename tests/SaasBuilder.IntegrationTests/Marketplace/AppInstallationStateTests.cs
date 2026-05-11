using System;
using System.Collections.Generic;
using FluentAssertions;
using Marketplace.Domain.Entities;
using Xunit;

namespace SaasBuilder.IntegrationTests.Marketplace;

/// <summary>
/// State machine tests for AppInstallation lifecycle:
/// Pending → Active and invalid transitions.
/// </summary>
public sealed class AppInstallationStateTests
{
    [Fact]
    public void Approve_WhenStatusIsPending_TransitionsToActive()
    {
        // Arrange
        AppInstallation installation = AppInstallation.CreatePending(
            tenantId: Guid.NewGuid(),
            appId: Guid.NewGuid(),
            oAuthClientId: "client-abc",
            grantedScopes: new[] { "read:data" },
            installedByUserId: Guid.NewGuid());

        installation.Status.Should().Be(AppInstallationStatus.Pending);

        // Act
        installation.Approve();

        // Assert
        installation.Status.Should().Be(AppInstallationStatus.Active);
    }

    [Fact]
    public void Approve_WhenStatusIsActive_ThrowsInvalidOperationException()
    {
        // Arrange
        AppInstallation installation = AppInstallation.CreatePending(
            tenantId: Guid.NewGuid(),
            appId: Guid.NewGuid(),
            oAuthClientId: "client-abc",
            grantedScopes: Array.Empty<string>(),
            installedByUserId: Guid.NewGuid());

        installation.Approve(); // first approval is valid

        // Act
        Action act = () => installation.Approve();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Pending*");
    }

    [Fact]
    public void Uninstall_AfterApproval_TransitionsToUninstalled()
    {
        // Arrange
        AppInstallation installation = AppInstallation.CreatePending(
            tenantId: Guid.NewGuid(),
            appId: Guid.NewGuid(),
            oAuthClientId: "client-xyz",
            grantedScopes: new[] { "write:webhooks" },
            installedByUserId: Guid.NewGuid());

        installation.Approve();

        // Act
        installation.Uninstall();

        // Assert
        installation.Status.Should().Be(AppInstallationStatus.Uninstalled);
    }

    [Fact]
    public void CreatePending_StoresGrantedScopesAsJsonArray()
    {
        // Arrange
        var scopes = new List<string> { "read:contacts", "write:webhooks" };

        // Act
        AppInstallation installation = AppInstallation.CreatePending(
            tenantId: Guid.NewGuid(),
            appId: Guid.NewGuid(),
            oAuthClientId: "client-scope-test",
            grantedScopes: scopes,
            installedByUserId: Guid.NewGuid());

        // Assert — verify the JSON representation contains both scopes
        installation.GrantedScopesJson.Should().Contain("read:contacts");
        installation.GrantedScopesJson.Should().Contain("write:webhooks");

        // Confirm it deserialises back correctly
        List<string>? deserialized = System.Text.Json.JsonSerializer.Deserialize<List<string>>(
            installation.GrantedScopesJson);

        deserialized.Should().BeEquivalentTo(scopes);
    }
}
