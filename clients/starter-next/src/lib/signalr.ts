"use client";

/**
 * Reconnecting SignalR singleton for the notifications hub.
 * Call `getConnection()` once per app lifetime; the instance reconnects
 * automatically with exponential back-off on disconnect.
 */

import {
  HubConnection,
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
} from "@microsoft/signalr";

let connection: HubConnection | null = null;

function buildConnection(accessToken: string): HubConnection {
  const apiBase =
    process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5000";

  return new HubConnectionBuilder()
    .withUrl(`${apiBase}/hubs/notifications`, {
      accessTokenFactory: () => accessToken,
    })
    .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
    .configureLogging(
      process.env.NODE_ENV === "development"
        ? LogLevel.Information
        : LogLevel.Warning,
    )
    .build();
}

export async function getConnection(accessToken: string): Promise<HubConnection> {
  if (
    connection &&
    connection.state !== HubConnectionState.Disconnected
  ) {
    return connection;
  }

  connection = buildConnection(accessToken);

  connection.onclose(() => {
    // Automatic reconnect handles restart; log for observability
    if (process.env.NODE_ENV === "development") {
      console.warn("[SignalR] connection closed");
    }
  });

  await connection.start();
  return connection;
}

export async function stopConnection(): Promise<void> {
  if (connection) {
    await connection.stop();
    connection = null;
  }
}
