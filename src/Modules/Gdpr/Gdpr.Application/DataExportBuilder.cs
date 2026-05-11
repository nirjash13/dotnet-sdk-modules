using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Gdpr.Application.Abstractions;

namespace Gdpr.Application;

/// <summary>
/// Aggregates PII from all registered <see cref="IExportable"/> providers and
/// produces a zip archive with one JSON file per table.
/// </summary>
public sealed class DataExportBuilder : IDataExportBuilder
{
    private readonly IEnumerable<IExportable> _exportables;

    /// <summary>Initializes a new instance of <see cref="DataExportBuilder"/>.</summary>
    public DataExportBuilder(IEnumerable<IExportable> exportables)
    {
        _exportables = exportables;
    }

    /// <inheritdoc />
    public async Task<Stream> BuildAsync(Guid tenantId, Guid userId, CancellationToken ct = default)
    {
        var ms = new MemoryStream();

        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (IExportable exportable in _exportables)
            {
                IReadOnlyList<IReadOnlyDictionary<string, object?>> rows =
                    await exportable.ExportAsync(tenantId, userId, ct).ConfigureAwait(false);

                string entryName = $"{exportable.TableName}.json";
                ZipArchiveEntry entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);

                await using Stream entryStream = entry.Open();
                string json = JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = false });
                byte[] bytes = Encoding.UTF8.GetBytes(json);
                await entryStream.WriteAsync(bytes, ct).ConfigureAwait(false);
            }
        }

        ms.Position = 0;
        return ms;
    }
}
