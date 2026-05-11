using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.AspNetCore.SignalR.Client;

namespace SaasBuilder.Starter.Blazor.Services;

/// <summary>
/// Manages a SignalR connection to <c>/hubs/notifications</c>.
/// Raises <see cref="OnNotificationReceived"/> for each incoming notification.
/// Register as scoped via DI; call <see cref="StartAsync"/> once per session.
/// </summary>
public sealed class NotificationsHubClient : IAsyncDisposable
{
    private readonly ApiClient _apiClient;
    private readonly string _hubUrl;
    private HubConnection? _connection;

    public event Action<NotificationMessage>? OnNotificationReceived;

    public NotificationsHubClient(ApiClient apiClient, IWebAssemblyHostEnvironment env)
    {
        _apiClient = apiClient;
        _hubUrl = env.BaseAddress.TrimEnd('/') + "/hubs/notifications";
    }

    public HubConnectionState State =>
        _connection?.State ?? HubConnectionState.Disconnected;

    public async Task StartAsync(CancellationToken ct = default)
    {
        if (_connection is not null)
        {
            return;
        }

        _connection = new HubConnectionBuilder()
            .WithUrl(_hubUrl, options =>
            {
                // Forward the Bearer token so the hub can authenticate the connection.
                options.AccessTokenProvider = () =>
                    Task.FromResult(_apiClient.AccessToken);
            })
            .WithAutomaticReconnect()
            .Build();

        _connection.On<NotificationMessage>("ReceiveNotification", message =>
        {
            OnNotificationReceived?.Invoke(message);
        });

        await _connection.StartAsync(ct).ConfigureAwait(false);
    }

    public async Task StopAsync(CancellationToken ct = default)
    {
        if (_connection is null)
        {
            return;
        }

        await _connection.StopAsync(ct).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
        {
            await _connection.DisposeAsync().ConfigureAwait(false);
        }
    }

    public sealed record NotificationMessage(
        string Id,
        string Title,
        string Body,
        DateTimeOffset CreatedAt,
        bool IsRead);
}
