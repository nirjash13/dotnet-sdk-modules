using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Files.Application.Abstractions;

/// <summary>
/// Image processing operations.
/// TODO(Phase 5.2): Implement via SixLabors.ImageSharp — evaluate license (Six Labors Split License)
///                  before adding to Directory.Packages.props. Ensure it is compatible with SaaS deployment.
/// </summary>
public interface IImageProcessor
{
    /// <summary>Resizes the image to the specified dimensions.</summary>
    Task<Stream> ResizeAsync(Stream input, int width, int height, CancellationToken ct = default);

    /// <summary>Generates a thumbnail of the image.</summary>
    Task<Stream> ThumbnailAsync(Stream input, int maxDimension, CancellationToken ct = default);

    /// <summary>Converts the image to WebP format.</summary>
    Task<Stream> ToWebPAsync(Stream input, int quality = 80, CancellationToken ct = default);
}
