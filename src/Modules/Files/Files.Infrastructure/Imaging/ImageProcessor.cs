using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Files.Application.Abstractions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace Files.Infrastructure.Imaging;

/// <summary>
/// Image processing implementation backed by SixLabors.ImageSharp.
/// License: Six Labors Split License — free for open-source and non-commercial deployments.
/// Commercial deployments require a commercial license from Six Labors.
/// </summary>
internal sealed class ImageProcessor : IImageProcessor
{
    /// <inheritdoc />
    public async Task<Stream> ResizeAsync(Stream input, int width, int height, CancellationToken ct = default)
    {
        Image image = await Image.LoadAsync(input, ct).ConfigureAwait(false);
        image.Mutate(x => x.Resize(new ResizeOptions
        {
            Size = new Size(width, height),
            Mode = ResizeMode.Max,
        }));

        MemoryStream output = new MemoryStream();
        await image.SaveAsync(output, image.Metadata.DecodedImageFormat!, ct).ConfigureAwait(false);
        output.Position = 0;
        return output;
    }

    /// <inheritdoc />
    public async Task<Stream> ThumbnailAsync(Stream input, int maxDimension, CancellationToken ct = default)
    {
        Image image = await Image.LoadAsync(input, ct).ConfigureAwait(false);
        image.Mutate(x => x.Resize(new ResizeOptions
        {
            Size = new Size(maxDimension, maxDimension),
            Mode = ResizeMode.Max,
        }));

        MemoryStream output = new MemoryStream();
        await image.SaveAsync(output, image.Metadata.DecodedImageFormat!, ct).ConfigureAwait(false);
        output.Position = 0;
        return output;
    }

    /// <inheritdoc />
    public async Task<Stream> ToWebPAsync(Stream input, int quality = 80, CancellationToken ct = default)
    {
        Image image = await Image.LoadAsync(input, ct).ConfigureAwait(false);

        MemoryStream output = new MemoryStream();
        WebpEncoder encoder = new WebpEncoder { Quality = quality };
        await image.SaveAsync(output, encoder, ct).ConfigureAwait(false);
        output.Position = 0;
        return output;
    }
}
