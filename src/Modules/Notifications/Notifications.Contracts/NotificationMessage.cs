using System;
using System.Collections.Generic;

namespace Notifications.Contracts;

/// <summary>
/// Immutable notification payload delivered via one or more <see cref="Channels"/>.
/// </summary>
/// <param name="RecipientUserId">Target user identifier.</param>
/// <param name="Subject">Short subject or title (email subject line / push title).</param>
/// <param name="Body">Full body text (plain-text fallback).</param>
/// <param name="Channels">Delivery channels. At least one is required.</param>
/// <param name="NotificationType">Application-defined type key (e.g. "invoice.payment_due").</param>
/// <param name="TemplateKey">Optional template key; when set the infrastructure renderer is used.</param>
/// <param name="TemplateModel">Model data passed to the template renderer.</param>
/// <param name="RecipientEmail">Override email address; if null, resolved from user profile.</param>
/// <param name="RecipientPhone">Override phone number for SMS; if null, resolved from user profile.</param>
/// <param name="ActionUrl">Optional deep-link URL surfaced in push/in-app notifications.</param>
/// <param name="CorrelationId">Tracing correlation identifier.</param>
public record NotificationMessage(
    Guid RecipientUserId,
    string Subject,
    string Body,
    IReadOnlyList<NotificationChannel> Channels,
    string NotificationType,
    string? TemplateKey = null,
    object? TemplateModel = null,
    string? RecipientEmail = null,
    string? RecipientPhone = null,
    string? ActionUrl = null,
    string? CorrelationId = null);
