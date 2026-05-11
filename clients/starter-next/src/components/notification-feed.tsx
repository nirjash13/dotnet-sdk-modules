"use client";

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

interface NotificationFeedProps {
  accessToken: string;
}

export function NotificationFeed({
  accessToken,
}: NotificationFeedProps): React.JSX.Element {
  const [notifications, setNotifications] = React.useState<Notification[]>([]);
  const [loading, setLoading] = React.useState(true);

  React.useEffect(() => {
    let mounted = true;

    apiGet<{ items: Notification[] }>("/api/v1/notifications", accessToken)
      .then((data) => { if (mounted) { setNotifications(data.items); setLoading(false); } })
      .catch(() => { if (mounted) setLoading(false); });

    getConnection(accessToken)
      .then((conn) => {
        conn.on("NotificationReceived", (n: Notification) => {
          if (mounted) {
            setNotifications((prev) => [n, ...prev]);
          }
        });
      })
      .catch(() => undefined);

    return () => { mounted = false; };
  }, [accessToken]);

  async function markAllRead(): Promise<void> {
    await apiPost("/api/v1/notifications/read-all", undefined, accessToken);
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
