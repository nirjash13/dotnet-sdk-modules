import { cookies } from "next/headers";
import { redirect } from "next/navigation";
import { AppShell } from "@/components/app-shell";
import { apiGet } from "@/lib/api";

interface Me {
  id: string;
  name: string;
  email: string;
}

interface TenantBranding {
  name: string;
  primaryColor: string;
  logoUrl: string | null;
}

export default async function AppLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  const jar = await cookies();
  const token = jar.get("sb_token")?.value;
  if (!token) redirect("/login");

  const [me, tenant] = await Promise.all([
    apiGet<Me>("/api/v1/identity/me", token).catch(() => null),
    apiGet<TenantBranding>("/api/v1/identity/me/tenant", token).catch(
      () => null,
    ),
  ]);

  if (!me) redirect("/login");

  return (
    <AppShell
      userName={me.name}
      userEmail={me.email}
      tenantName={tenant?.name}
      logoUrl={tenant?.logoUrl}
    >
      {children}
    </AppShell>
  );
}
