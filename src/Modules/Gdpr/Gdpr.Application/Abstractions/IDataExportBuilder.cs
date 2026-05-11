using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Gdpr.Application.Abstractions;

/// <summary>
/// Collects PII from all registered <see cref="IExportable"/> providers and produces
/// a zip archive containing one JSON file per provider table.
/// </summary>
public interface IDataExportBuilder
{
    /// <summary>
    /// Builds the export zip and writes it to the returned stream.
    /// Callers are responsible for disposing the stream.
    /// </summary>
    Task<Stream> BuildAsync(Guid tenantId, Guid userId, CancellationToken ct = default);
}
