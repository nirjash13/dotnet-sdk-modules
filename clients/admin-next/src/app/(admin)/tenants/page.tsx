import Link from "next/link";
import { cookies } from "next/headers";
import { Badge } from "@/components/ui/badge";
import { Input } from "@/components/ui/input";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";

interface Tenant {
  id: string;
  name: string;
  slug: string;
  status: "active" | "suspended" | "trial";
  plan: string;
  memberCount: number;
  createdAt: string;
}

interface TenantsResponse {
  items: Tenant[];
  total: number;
}

async function fetchTenants(search?: string, status?: string): Promise<TenantsResponse> {
  const apiBase = process.env.NEXT_PUBLIC_API_BASE_URL ?? "http://localhost:5000";
  const params = new URLSearchParams({ pageSize: "50" });
  if (search) params.set("search", search);
  if (status) params.set("status", status);

  const jar = await cookies();
  const token = jar.get("admin_access_token")?.value;

  try {
    const res = await fetch(`${apiBase}/api/v1/admin/tenants?${params}`, {
      headers: token ? { Authorization: `Bearer ${token}` } : {},
      cache: "no-store",
    });
    if (!res.ok) return { items: [], total: 0 };
    return res.json() as Promise<TenantsResponse>;
  } catch {
    return { items: [], total: 0 };
  }
}

function StatusBadge({ status }: { status: Tenant["status"] }) {
  const variant =
    status === "active"
      ? "success"
      : status === "suspended"
        ? "destructive"
        : "warning";
  return <Badge variant={variant}>{status}</Badge>;
}

function EmptyState() {
  return (
    <div className="flex flex-col items-center justify-center rounded-lg border border-dashed py-16 text-center">
      <p className="text-lg font-medium text-muted-foreground">No tenants found</p>
      <p className="mt-1 text-sm text-muted-foreground">
        Tenants will appear here once the backend is connected.
      </p>
    </div>
  );
}

export default async function TenantsPage({
  searchParams,
}: {
  searchParams: Promise<{ search?: string; status?: string }>;
}) {
  const params = await searchParams;
  const { items, total } = await fetchTenants(params.search, params.status);

  return (
    <div>
      <div className="mb-6 flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold">Tenants</h1>
          <p className="text-sm text-muted-foreground">
            {total} tenant{total !== 1 ? "s" : ""} total
          </p>
        </div>
      </div>

      <div className="mb-4 flex gap-3">
        <form className="flex-1">
          <Input
            name="search"
            placeholder="Search by name or slug..."
            defaultValue={params.search}
          />
        </form>
        <form>
          <select
            name="status"
            defaultValue={params.status ?? ""}
            className="h-10 rounded-md border border-input bg-background px-3 text-sm"
          >
            <option value="">All statuses</option>
            <option value="active">Active</option>
            <option value="suspended">Suspended</option>
            <option value="trial">Trial</option>
          </select>
        </form>
      </div>

      {items.length === 0 ? (
        <EmptyState />
      ) : (
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Name</TableHead>
              <TableHead>Slug</TableHead>
              <TableHead>Status</TableHead>
              <TableHead>Plan</TableHead>
              <TableHead>Members</TableHead>
              <TableHead>Created</TableHead>
              <TableHead></TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {items.map((tenant) => (
              <TableRow key={tenant.id}>
                <TableCell className="font-medium">{tenant.name}</TableCell>
                <TableCell className="text-muted-foreground">{tenant.slug}</TableCell>
                <TableCell>
                  <StatusBadge status={tenant.status} />
                </TableCell>
                <TableCell>{tenant.plan}</TableCell>
                <TableCell>{tenant.memberCount}</TableCell>
                <TableCell className="text-muted-foreground">
                  {new Date(tenant.createdAt).toLocaleDateString()}
                </TableCell>
                <TableCell>
                  <Link
                    href={`/admin/tenants/${tenant.id}`}
                    className="text-sm font-medium text-primary hover:underline"
                  >
                    Inspect
                  </Link>
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      )}
    </div>
  );
}
