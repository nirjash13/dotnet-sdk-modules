using Microsoft.Extensions.Logging;
using Notifications.Application.Abstractions;

namespace Notifications.Infrastructure.Templating;

/// <summary>
/// Fallback template renderer used when no real renderer (e.g. Razor) is registered.
/// Returns an empty body and logs a WARNING.
/// TODO(Phase 5.1): replace with RazorNotificationTemplateRenderer once Razor-based rendering
///                  is wired in (requires Microsoft.AspNetCore.Mvc.RazorPages).
/// </summary>
internal sealed class FallbackNotificationTemplate(ILogger<FallbackNotificationTemplate> logger)
    : INotificationTemplate
{
    /// <inheritdoc />
    public string Render(string templateKey, object model)
    {
        logger.LogWarning(
            "Notifications.Template: template '{TemplateKey}' not found. " +
            "Returning empty body. TODO(Phase 5.1): configure RazorNotificationTemplateRenderer.",
            templateKey);

        return string.Empty;
    }
}
