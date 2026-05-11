/**
 * dashboard.tsx — Copy-paste example for SaasBuilder dashboard page.
 * Drop into app/(app)/dashboard/page.tsx in your Next.js project.
 * No build pipeline — illustrative source only.
 */
import { cookies } from "next/headers";
import { redirect } from "next/navigation";
import { SaasBuilderClient } from "@saasbuilder/client";

interface Me {
  id: string;
  name: string;
  email: string;
  role: string;
}

interface TenantBranding {
  name: string;
  primaryColor: string;
  logoUrl: string | null;
}

interface Subscription {
  planName: string;
  status: string;
  currentPeriodEnd: string;
}

async function getServerData() {
  const jar = await cookies();
  const token = jar.get("sb_token")?.value;
  if (!token) redirect("/login");

  const client = new SaasBuilderClient({
    baseUrl: process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5000",
    token,
  });

  const [me, tenant, subscription] = await Promise.all([
    client.request<Me>("/api/v1/identity/me"),
    client.request<TenantBranding>("/api/v1/identity/me/tenant"),
    client.request<Subscription>("/api/v1/billing/subscription").catch(() => null),
  ]);

  return { me, tenant, subscription };
}

export default async function DashboardPage() {
  const { me, tenant, subscription } = await getServerData();

  const primaryColor = tenant.primaryColor ?? "#6366f1";

  return (
    <main style={{ fontFamily: "sans-serif", padding: 32 }}>
      <header style={{ display: "flex", alignItems: "center", gap: 16, marginBottom: 32 }}>
        {tenant.logoUrl && <img src={tenant.logoUrl} alt={tenant.name} height={40} />}
        <h1 style={{ color: primaryColor }}>{tenant.name}</h1>
      </header>

      <section style={{ marginBottom: 24 }}>
        <h2>Welcome, {me.name}</h2>
        <p style={{ color: "#666" }}>{me.email} &mdash; {me.role}</p>
      </section>

      {subscription && (
        <section style={{ background: "#f9f9f9", borderRadius: 8, padding: 16, marginBottom: 24 }}>
          <h3>Subscription</h3>
          <p>Plan: <strong>{subscription.planName}</strong></p>
          <p>Status: <span style={{ color: subscription.status === "active" ? "green" : "orange" }}>{subscription.status}</span></p>
          <p>Renews: {new Date(subscription.currentPeriodEnd).toLocaleDateString()}</p>
          <a href="/billing">Manage billing</a>
        </section>
      )}

      <nav style={{ display: "flex", gap: 16, flexWrap: "wrap" }}>
        {[
          { href: "/members", label: "Team Members" },
          { href: "/billing", label: "Billing" },
          { href: "/settings", label: "Settings" },
          { href: "/webhooks", label: "Webhooks" },
        ].map(({ href, label }) => (
          <a
            key={href}
            href={href}
            style={{
              display: "block",
              padding: "12px 20px",
              background: primaryColor,
              color: "#fff",
              borderRadius: 6,
              textDecoration: "none",
              fontWeight: 600,
            }}
          >
            {label}
          </a>
        ))}
      </nav>
    </main>
  );
}
