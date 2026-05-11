"use client";

import { useEffect, useState } from "react";
import { api } from "@/lib/api";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";

interface WebhookSubscription {
  id: string;
  tenantId: string;
  tenantName: string;
  url: string;
  events: string[];
  isActive: boolean;
}

interface DeliveryLog {
  id: string;
  subscriptionId: string;
  event: string;
  statusCode: number;
  attemptedAt: string;
  success: boolean;
}

export default function WebhooksPage() {
  const [search, setSearch] = useState("");
  const [subscriptions, setSubscriptions] = useState<WebhookSubscription[]>([]);
  const [deliveries, setDeliveries] = useState<DeliveryLog[]>([]);
  const [selectedSub, setSelectedSub] = useState<WebhookSubscription | null>(null);
  const [loading, setLoading] = useState(true);
  const [replayId, setReplayId] = useState<string | null>(null);

  useEffect(() => {
    async function load() {
      setLoading(true);
      try {
        const params = search ? `?search=${encodeURIComponent(search)}` : "";
        const result = await api.get<{ items: WebhookSubscription[] }>(
          `/api/v1/admin/webhooks${params}`,
        );
        setSubscriptions(result.items);
      } catch {
        setSubscriptions([]);
      } finally {
        setLoading(false);
      }
    }
    void load();
  }, [search]);

  async function loadDeliveries(sub: WebhookSubscription) {
    setSelectedSub(sub);
    try {
      const result = await api.get<{ items: DeliveryLog[] }>(
        `/api/v1/admin/webhooks/${sub.id}/deliveries?pageSize=20`,
      );
      setDeliveries(result.items);
    } catch {
      setDeliveries([]);
    }
  }

  async function replay(deliveryId: string) {
    setReplayId(deliveryId);
    try {
      await api.post(`/api/v1/admin/webhooks/deliveries/${deliveryId}/replay`, {});
    } finally {
      setReplayId(null);
    }
  }

  return (
    <div>
      <div className="mb-6">
        <h1 className="text-2xl font-bold">Webhook Deliveries</h1>
        <p className="text-sm text-muted-foreground">
          View delivery logs and replay failed events per tenant
        </p>
      </div>

      <div className="mb-4 max-w-sm">
        <Input
          placeholder="Filter by tenant name..."
          value={search}
          onChange={(e) => setSearch(e.target.value)}
        />
      </div>

      {loading ? (
        <p className="py-8 text-center text-muted-foreground">Loading...</p>
      ) : subscriptions.length === 0 ? (
        <div className="rounded-lg border border-dashed py-16 text-center">
          <p className="text-muted-foreground">
            No webhook subscriptions found — backend may not be connected yet.
          </p>
        </div>
      ) : (
        <div className="grid gap-6 lg:grid-cols-[1fr_2fr]">
          <div className="space-y-2">
            <p className="text-sm font-medium text-muted-foreground">Subscriptions</p>
            {subscriptions.map((sub) => (
              <Card
                key={sub.id}
                className={`cursor-pointer transition-colors ${selectedSub?.id === sub.id ? "border-primary" : ""}`}
                onClick={() => loadDeliveries(sub)}
              >
                <CardHeader className="pb-2 pt-4 px-4">
                  <CardTitle className="text-sm">{sub.tenantName}</CardTitle>
                </CardHeader>
                <CardContent className="px-4 pb-4">
                  <p className="truncate text-xs text-muted-foreground">{sub.url}</p>
                  <div className="mt-2 flex gap-1 flex-wrap">
                    {sub.events.slice(0, 3).map((e) => (
                      <Badge key={e} variant="secondary" className="text-xs">
                        {e}
                      </Badge>
                    ))}
                    {sub.events.length > 3 && (
                      <Badge variant="outline" className="text-xs">
                        +{sub.events.length - 3}
                      </Badge>
                    )}
                  </div>
                </CardContent>
              </Card>
            ))}
          </div>

          <div>
            {selectedSub ? (
              <>
                <p className="mb-3 text-sm font-medium text-muted-foreground">
                  Deliveries for {selectedSub.tenantName}
                </p>
                {deliveries.length === 0 ? (
                  <p className="py-8 text-center text-muted-foreground">
                    No deliveries recorded.
                  </p>
                ) : (
                  <Table>
                    <TableHeader>
                      <TableRow>
                        <TableHead>Event</TableHead>
                        <TableHead>Status</TableHead>
                        <TableHead>Time</TableHead>
                        <TableHead></TableHead>
                      </TableRow>
                    </TableHeader>
                    <TableBody>
                      {deliveries.map((d) => (
                        <TableRow key={d.id}>
                          <TableCell className="font-mono text-xs">{d.event}</TableCell>
                          <TableCell>
                            <Badge
                              variant={d.success ? "success" : "destructive"}
                            >
                              {d.statusCode}
                            </Badge>
                          </TableCell>
                          <TableCell className="text-muted-foreground">
                            {new Date(d.attemptedAt).toLocaleString()}
                          </TableCell>
                          <TableCell>
                            {!d.success && (
                              <Button
                                size="sm"
                                variant="outline"
                                onClick={() => replay(d.id)}
                                disabled={replayId === d.id}
                              >
                                Replay
                              </Button>
                            )}
                          </TableCell>
                        </TableRow>
                      ))}
                    </TableBody>
                  </Table>
                )}
              </>
            ) : (
              <div className="flex h-40 items-center justify-center rounded-lg border border-dashed">
                <p className="text-muted-foreground">Select a subscription to view deliveries</p>
              </div>
            )}
          </div>
        </div>
      )}
    </div>
  );
}
