"use client";

import { useEffect, useState } from "react";
import { z } from "zod";
import { api } from "@/lib/api";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";

const flagSchema = z.object({
  id: z.string(),
  name: z.string(),
  description: z.string().optional(),
  enabledGlobally: z.boolean(),
  enabledTenants: z.array(z.string()),
});

type FeatureFlag = z.infer<typeof flagSchema>;

export default function FeatureFlagsPage() {
  const [flags, setFlags] = useState<FeatureFlag[]>([]);
  const [loading, setLoading] = useState(true);
  const [search, setSearch] = useState("");
  const [tenantFilter, setTenantFilter] = useState("");
  const [toggling, setToggling] = useState<string | null>(null);

  useEffect(() => {
    async function load() {
      setLoading(true);
      try {
        const result = await api.get<{ items: unknown[] }>("/api/v1/admin/feature-flags");
        const parsed = result.items
          .map((item) => flagSchema.safeParse(item))
          .filter((r): r is z.SafeParseSuccess<FeatureFlag> => r.success)
          .map((r) => r.data);
        setFlags(parsed);
      } catch {
        setFlags([]);
      } finally {
        setLoading(false);
      }
    }
    void load();
  }, []);

  async function toggleGlobal(flag: FeatureFlag) {
    setToggling(flag.id);
    try {
      await api.patch(`/api/v1/admin/feature-flags/${flag.id}`, {
        enabledGlobally: !flag.enabledGlobally,
      });
      setFlags((prev) =>
        prev.map((f) =>
          f.id === flag.id
            ? { ...f, enabledGlobally: !f.enabledGlobally }
            : f,
        ),
      );
    } finally {
      setToggling(null);
    }
  }

  async function toggleTenant(flag: FeatureFlag, tenantId: string) {
    setToggling(`${flag.id}:${tenantId}`);
    const isEnabled = flag.enabledTenants.includes(tenantId);
    try {
      if (isEnabled) {
        await api.delete(`/api/v1/admin/feature-flags/${flag.id}/tenants/${tenantId}`);
        setFlags((prev) =>
          prev.map((f) =>
            f.id === flag.id
              ? { ...f, enabledTenants: f.enabledTenants.filter((t) => t !== tenantId) }
              : f,
          ),
        );
      } else {
        await api.post(`/api/v1/admin/feature-flags/${flag.id}/tenants`, { tenantId });
        setFlags((prev) =>
          prev.map((f) =>
            f.id === flag.id
              ? { ...f, enabledTenants: [...f.enabledTenants, tenantId] }
              : f,
          ),
        );
      }
    } finally {
      setToggling(null);
    }
  }

  const visibleFlags = flags.filter(
    (f) =>
      f.name.toLowerCase().includes(search.toLowerCase()) &&
      (tenantFilter === "" ||
        f.enabledTenants.some((t) =>
          t.toLowerCase().includes(tenantFilter.toLowerCase()),
        )),
  );

  return (
    <div>
      <div className="mb-6">
        <h1 className="text-2xl font-bold">Feature Flags</h1>
        <p className="text-sm text-muted-foreground">
          Global on/off toggles and per-tenant overrides
        </p>
      </div>

      <div className="mb-4 flex gap-3">
        <Input
          className="max-w-xs"
          placeholder="Search flags..."
          value={search}
          onChange={(e) => setSearch(e.target.value)}
        />
        <Input
          className="max-w-xs"
          placeholder="Filter by tenant ID..."
          value={tenantFilter}
          onChange={(e) => setTenantFilter(e.target.value)}
        />
      </div>

      {loading ? (
        <p className="py-8 text-center text-muted-foreground">Loading...</p>
      ) : visibleFlags.length === 0 ? (
        <div className="rounded-lg border border-dashed py-16 text-center">
          <p className="text-muted-foreground">
            No flags found — backend may not be connected yet.
          </p>
        </div>
      ) : (
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Flag</TableHead>
              <TableHead>Description</TableHead>
              <TableHead>Global</TableHead>
              <TableHead>Enabled Tenants</TableHead>
              <TableHead></TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {visibleFlags.map((flag) => (
              <TableRow key={flag.id}>
                <TableCell>
                  <code className="rounded bg-muted px-1.5 py-0.5 text-sm">
                    {flag.name}
                  </code>
                </TableCell>
                <TableCell className="text-muted-foreground">
                  {flag.description ?? "—"}
                </TableCell>
                <TableCell>
                  <Badge variant={flag.enabledGlobally ? "success" : "outline"}>
                    {flag.enabledGlobally ? "On" : "Off"}
                  </Badge>
                </TableCell>
                <TableCell>
                  <div className="flex flex-wrap gap-1">
                    {flag.enabledTenants.length === 0 ? (
                      <span className="text-muted-foreground text-xs">none</span>
                    ) : (
                      flag.enabledTenants.map((t) => (
                        <Badge key={t} variant="secondary" className="text-xs">
                          {t}
                        </Badge>
                      ))
                    )}
                  </div>
                </TableCell>
                <TableCell>
                  <Button
                    size="sm"
                    variant="outline"
                    onClick={() => toggleGlobal(flag)}
                    disabled={toggling === flag.id}
                  >
                    {flag.enabledGlobally ? "Disable" : "Enable"} globally
                  </Button>
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      )}
    </div>
  );
}
