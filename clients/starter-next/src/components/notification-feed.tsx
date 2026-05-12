"use client";

// C-28 FIX: This component no longer accepts an accessToken prop.
// The long-lived access token must NOT appear in the client JS heap or RSC payload.
// Instead, SignalR hub auth is obtained by fetching /api/signalr-token (a BFF route
// that reads the HttpOnly sb_token cookie server-side and returns a short-lived hub token).
// API calls to /api/v1/notifications go through the BFF auto-refresh path (no token arg).

import * as React from "react";
import { Bell, CheckCheck } from "lucide-react";
import { getConnection } from "@/lib/signalr";
import { apiGet, apiPost } from "@/lib/api";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";

interface Notification {
  id: string;
  title: string;
  body: string;
  createdAt: string;
  isRead: boolean;
}

export function NotificationFeed(): React.JSX.Element {
  const [notifications, setNotifications] = React.useState<Notification[]>([]);
  const [loading, setLoading] = React.useState(true);

  React.useEffect(() => {
    let mounted = true;
    const controller = new AbortController();

    // Fetch notifications server-side via BFF (no token in args — uses cookie auto-refresh)
    apiGet<{ items: Notification[] }>("/api/v1/notifications")
      .then((data) => {
        if (mounted) {
          setNotifications(data.items);
          setLoading(false);
        }
      })
      .catch(() => {
        if (mounted) setLoading(false);
      });

    // Fetch a short-lived hub token from the BFF route (reads HttpOnly cookie server-side)
    fetch("/api/signalr-token", { method: "POST", signal: controller.signal })
      .then((res) => (res.ok ? (res.json() as Promise<{ hub_token: string }>) : null))
      .then((data) => {
        if (!data || !mounted) return;
        return getConnection(data.hub_token);
      })
      .then((conn) => {
        if (!conn || !mounted) return;
        conn.on("NotificationReceived", (n: Notification) => {
          if (mounted) {
            setNotifications((prev) => [n, ...prev]);
          }
        });
      })
      .catch(() => undefined);

    return () => {
      mounted = false;
      controller.abort();
    };
  }, []);

  async function markAllRead(): Promise<void> {
    await apiPost("/api/v1/notifications/read-all");
    setNotifications((prev) => prev.map((n) => ({ ...n, isRead: true })));
  }

  const unreadCount = notifications.filter((n) => !n.isRead).length;

  if (loading) {
    return (
      <div className="space-y-3">
        {Array.from({ length: 3 }).map((_, i) => (
          <Skeleton key={i} className="h-16 w-full" />
        ))}
      </div>
    );
  }

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-2">
          <Bell className="h-5 w-5" />
          <span className="font-semibold">Notifications</span>
          {unreadCount > 0 && (
            <Badge variant="default">{unreadCount}</Badge>
          )}
        </div>
        {unreadCount > 0 && (
          <Button
            variant="ghost"
            size="sm"
            onClick={() => void markAllRead()}
          >
            <CheckCheck className="mr-1 h-4 w-4" />
            Mark all read
          </Button>
        )}
      </div>

      {notifications.length === 0 ? (
        <div className="rounded-lg border border-dashed p-8 text-center text-muted-foreground">
          No notifications yet.
        </div>
      ) : (
        <ul className="space-y-2">
          {notifications.map((n) => (
            <li
              key={n.id}
              className={`rounded-lg border p-4 ${n.isRead ? "opacity-60" : "border-primary/30 bg-primary/5"}`}
            >
              <p className="text-sm font-medium">{n.title}</p>
              <p className="mt-0.5 text-sm text-muted-foreground">{n.body}</p>
              <p className="mt-1 text-xs text-muted-foreground">
                {new Date(n.createdAt).toLocaleString()}
              </p>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
