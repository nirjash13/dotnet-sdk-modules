using System;
using System.Collections.Generic;
using Identity.Domain.Exceptions;
using SaasBuilder.SharedKernel.Tenancy;

namespace Identity.Domain.Organizations;

/// <summary>
/// Aggregate root representing a B2B organization (team/workspace) within a tenant.
/// An Organization is a logical grouping of users with shared RBAC permissions,
/// branding, and settings. It is distinct from a Tenant (the billing/isolation unit).
/// </summary>
/// <remarks>
/// Invariants enforced by this aggregate:
/// <list type="bullet">
///   <item>Slug and Name must be non-empty.</item>
///   <item>Status transitions: Active ↔ Suspended; Active/Suspended → Archived (one-way).</item>
///   <item>At least one active Owner member must exist at all times (enforced via <see cref="Member"/>).</item>
/// </list>
/// </remarks>
public sealed class Organization : ITenantScoped
{
    private readonly List<Member> _members = new List<Member>();

    // Private constructor — use factory method to enforce invariants.
    private Organization()
    {
    }

    /// <summary>Gets the organization's unique identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>Gets the tenant this organization belongs to.</summary>
    public Guid TenantId { get; private set; }

    /// <summary>
    /// Gets the URL-safe slug for this organization (e.g., "acme-corp").
    /// Unique within a tenant.
    /// </summary>
    public string Slug { get; private set; } = string.Empty;

    /// <summary>Gets the human-readable display name.</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the branding configuration as a JSON blob.
    /// Null when no custom branding has been configured.
    /// </summary>
    public string? BrandingJson { get; private set; }

    /// <summary>
    /// Gets the organization settings as a JSON blob.
    /// Null when using default settings.
    /// </summary>
    public string? SettingsJson { get; private set; }

    /// <summary>Gets the current lifecycle status of the organization.</summary>
    public OrganizationStatus Status { get; private set; }

    /// <summary>Gets the UTC timestamp when the organization was created.</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Gets the UTC timestamp of the last modification, if any.</summary>
    public DateTimeOffset? UpdatedAt { get; private set; }

    /// <summary>Gets the organization's members (read-only projection).</summary>
    public IReadOnlyList<Member> Members => _members;

    /// <summary>
    /// Creates a new <see cref="Organization"/> with the given identity.
    /// </summary>
    /// <param name="id">The unique identifier. Must not be <see cref="Guid.Empty"/>.</param>
    /// <param name="tenantId">The owning tenant. Must not be <see cref="Guid.Empty"/>.</param>
    /// <param name="slug">URL-safe slug. Must not be null or whitespace.</param>
    /// <param name="name">Display name. Must not be null or whitespace.</param>
    /// <returns>A new <see cref="Organization"/> in <see cref="OrganizationStatus.Active"/> status.</returns>
    public static Organization Create(Guid id, Guid tenantId, string slug, string name)
    {
        if (id == Guid.Empty)
        {
            throw new IdentityDomainException("Organization id must not be empty.");
        }

        if (tenantId == Guid.Empty)
        {
            throw new IdentityDomainException("TenantId must not be empty.");
        }

        if (string.IsNullOrWhiteSpace(slug))
        {
            throw new IdentityDomainException("Organization slug must not be empty.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new IdentityDomainException("Organization name must not be empty.");
        }

        return new Organization
        {
            Id = id,
            TenantId = tenantId,
            Slug = slug.Trim().ToLowerInvariant(),
            Name = name.Trim(),
            Status = OrganizationStatus.Active,
            CreatedAt = DateTimeOffset.UtcNow,
        };
    }

    /// <summary>Updates the organization's display name.</summary>
    /// <param name="name">New name. Must not be null or whitespace.</param>
    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new IdentityDomainException("Organization name must not be empty.");
        }

        Name = name.Trim();
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>Replaces the branding configuration JSON.</summary>
    /// <param name="brandingJson">The new branding JSON, or <see langword="null"/> to clear.</param>
    public void UpdateBranding(string? brandingJson)
    {
        BrandingJson = brandingJson;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>Transitions the organization to <see cref="OrganizationStatus.Suspended"/>.</summary>
    public void Suspend()
    {
        if (Status == OrganizationStatus.Archived)
        {
            throw new IdentityDomainException("Cannot suspend an archived organization.");
        }

        Status = OrganizationStatus.Suspended;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>Transitions the organization back to <see cref="OrganizationStatus.Active"/>.</summary>
    public void Reactivate()
    {
        if (Status == OrganizationStatus.Archived)
        {
            throw new IdentityDomainException("Cannot reactivate an archived organization.");
        }

        Status = OrganizationStatus.Active;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Internal helper called by <see cref="Member"/> to register a member with this aggregate.
    /// </summary>
    internal void AddMemberInternal(Member member) => _members.Add(member);
}
