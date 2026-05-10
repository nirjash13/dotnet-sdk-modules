using System;

namespace SaasBuilder.SharedKernel.Tenancy.Encryption;

/// <summary>
/// Marks an entity property as containing PII that must be encrypted at rest using the
/// tenant's envelope encryption key via <see cref="ITenantKeyProvider"/>.
/// </summary>
/// <remarks>
/// The EF Core value converters <c>EncryptedString</c> and <c>EncryptedBytes</c> detect this
/// attribute when applied to entity properties and apply AES-256-GCM encryption transparently.
/// In dev mode (when <c>FileSystemTenantKeyProvider</c> is configured), a WARNING is logged
/// on first use and the value is stored in plaintext — never use dev mode in production.
/// </remarks>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public sealed class EncryptedAttribute : Attribute
{
}
