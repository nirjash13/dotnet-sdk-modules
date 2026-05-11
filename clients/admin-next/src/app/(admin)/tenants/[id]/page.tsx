"use client";

import { useEffect, useState } from "react";
import { useParams } from "next/navigation";
import { api, ApiError } from "@/lib/api";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";

interface TenantDetail {
  id: string;
  name: string;
  slug: string;
  status: "active" | "suspended" | "trial";
  plan: string;
  memberCount: number;
  storageUsedMb: number;
  apiCallsThisMonth: number;
  createdAt: string;
}

interface Member {
  id: string;
  name: string;
  email: string;
  role: string;
  joinedAt: string;
}

interface AuditEvent {
  id: string;
  action: string;
  actor: string;
  occurredAt: string;
}

function useTenant(id: string) {
  const [tenant, setTenant] = useState<TenantDetail | null>(null);
  const [members, setMembers] = useState<Member[]>([]);
  const [events, setEvents] = useState<AuditEvent[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    async function load() {
      try {
        const [t, m, e] = await Promise.all([
          api.get<TenantDetail>(`/api/v1/admin/tenants/${id}`),
          api
            .get<{ items: Member[] }>(`/api/v1/admin/tenants/${id}/members`)
            .then((r) => r.items),
          api
            .get<{ items: AuditEvent[] }>(`/api/v1/admin/tenants/${id}/events?pageSize=10`)
            .then((r) => r.items),
        ]);
        setTenant(t);
        setMembers(m);
        setEvents(e);
      } catch (err) {
        if (err instanceof ApiError) {
          setError(err.message);
        } else {
          setError("Failed to load tenant.");
        }
      } finally {
        setLoading(false);
      }
    }
    void load();
  }, [id]);

  return { tenant, members, events, loading, error, setTenant };
}

export default function TenantInspectorPage() {
  const { id } = useParams<{ id: string }>();
  const { tenant, members, events, loading, error, setTenant } = useTenant(id);
  const [actionLoading, setActionLoading] = useState(false);

  async function handleSuspend() {
    setActionLoading(true);
    try {
      await api.post(`/api/v1/admin/tenants/${id}/suspend`, {});
      setTenant((t) => (t ? { ...t, status: "suspended" } : t));
    } finally {
      setActionLoading(false);
    }
  }

  async function handleRestore() {
    setActionLoading(true);
    try {
      await api.post(`/api/v1/admin/tenants/${id}/restore`, {});
      setTenant((t) => (t ? { ...t, status: "active" } : t));
    } finally {
      setActionLoading(false);
    }
  }

  if (loading) {
    return (
      <div className="space-y-4">
        <Skeleton className="h-8 w-64" />
        <Skeleton className="h-40 w-full" />
        <Skeleton className="h-64 w-full" />
      </div>
    );
  }

  if (error || !tenant) {
    return (
      <div className="rounded-lg border border-dashed py-16 text-center">
        <p className="text-muted-foreground">
          {error ?? "Tenant not found — backend may not be connected yet."}
        </p>
      </div>
    );
  }

  const isSuspended = tenant.status === "suspended";

  return (
    <div>
      <div className="mb-6 flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold">{tenant.name}</h1>
          <p className="text-sm text-muted-foreground">
            {tenant.slug} &middot; ID: {tenant.id}
          </p>
        </div>
        <div className="flex items-center gap-3">
          <Badge
            variant={
              tenant.status === "active"
                ? "success"
                : tenant.status === "suspended"
                  ? "destructive"
                  : "warning"
            }
          >
            {tenant.status}
          </Badge>
          {isSuspended ? (
            <Button
              size="sm"
              variant="outline"
              onClick={handleRestore}
              disabled={actionLoading}
            >
              Restore
            </Button>
          ) : (
            <Button
              size="sm"
              variant="destructive"
              onClick={handleSuspend}
              disabled={actionLoading}
            >
              Suspend
            </Button>
          )}
        </div>
      </div>

      <div className="mb-6 grid grid-cols-2 gap-4 md:grid-cols-4">
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-sm font-medium text-muted-foreground">Plan</CardTitle>
          </CardHeader>
          <CardContent>
            <p className="text-xl font-bold">{tenant.plan}</p>
          </CardContent>
        </Card>
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-sm font-medium text-muted-foreground">Members</CardTitle>
          </CardHeader>
          <CardContent>
            <p className="text-xl font-bold">{tenant.memberCount}</p>
          </CardContent>
        </Card>
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-sm font-medium text-muted-foreground">Storage</CardTitle>
          </CardHeader>
          <CardContent>
            <p className="text-xl font-bold">{tenant.storageUsedMb} MB</p>
          </CardContent>
        </Card>
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-sm font-medium text-muted-foreground">API calls (mo.)</CardTitle>
          </CardHeader>
          <CardContent>
            <p className="text-xl font-bold">{tenant.apiCallsThisMonth.toLocaleString()}</p>
          </CardContent>
        </Card>
      </div>

      <Tabs defaultValue="members">
        <TabsList>
          <TabsTrigger value="members">Members</TabsTrigger>
          <TabsTrigger value="events">Recent Events</TabsTrigger>
        </TabsList>

        <TabsContent value="members">
          {members.length === 0 ? (
            <p className="py-8 text-center text-muted-foreground">No members.</p>
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Name</TableHead>
                  <TableHead>Email</TableHead>
                  <TableHead>Role</TableHead>
                  <TableHead>Joined</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {members.map((m) => (
                  <TableRow key={m.id}>
                    <TableCell className="font-medium">{m.name}</TableCell>
                    <TableCell>{m.email}</TableCell>
                    <TableCell>{m.role}</TableCell>
                    <TableCell className="text-muted-foreground">
                      {new Date(m.joinedAt).toLocaleDateString()}
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </TabsContent>

        <TabsContent value="events">
          {events.length === 0 ? (
            <p className="py-8 text-center text-muted-foreground">No recent events.</p>
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Action</TableHead>
                  <TableHead>Actor</TableHead>
                  <TableHead>Time</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {events.map((e) => (
                  <TableRow key={e.id}>
                    <TableCell className="font-mono text-xs">{e.action}</TableCell>
                    <TableCell>{e.actor}</TableCell>
                    <TableCell className="text-muted-foreground">
                      {new Date(e.occurredAt).toLocaleString()}
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </TabsContent>
      </Tabs>
    </div>
  );
}
