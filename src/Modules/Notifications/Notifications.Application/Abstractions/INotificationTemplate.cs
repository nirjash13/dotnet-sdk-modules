namespace Notifications.Application.Abstractions;

/// <summary>
/// Renders a notification template identified by <paramref name="templateKey"/>.
/// </summary>
public interface INotificationTemplate
{
    /// <summary>
    /// Renders the named template with <paramref name="model"/> as its data context.
    /// Returns a plain-text or HTML body depending on the template format.
    /// Returns an empty fallback string when the template is not found; never throws.
    /// </summary>
    /// <param name="templateKey">Logical template identifier (e.g. "invoice.payment_due").</param>
    /// <param name="model">Data model injected into the template.</param>
    string Render(string templateKey, object model);
}
