namespace Notifications.Application.Abstractions;

/// <summary>
/// Converts an MJML template string to HTML.
/// TODO(Phase 5.1): Implement via a sidecar Node.js process or MJML WASM binding.
/// </summary>
public interface IMjmlRenderer
{
    /// <summary>
    /// Renders <paramref name="mjml"/> to HTML.
    /// Stub — always throws <see cref="System.NotImplementedException"/>.
    /// </summary>
    string RenderToHtml(string mjml);
}
