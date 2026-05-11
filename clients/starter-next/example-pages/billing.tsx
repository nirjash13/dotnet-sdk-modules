/**
 * billing.tsx — Copy-paste example for SaasBuilder billing page.
 * Drop into app/(app)/billing/page.tsx in your Next.js project.
 * No build pipeline — illustrative source only.
 */
"use client";

import { useEffect, useState } from "react";
import { SaasBuilderClient } from "@saasbuilder/client";

const client = new SaasBuilderClient({
  baseUrl: process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5000",
});

interface Subscription {
  planName: string;
  status: string;
  currentPeriodEnd: string;
  cancelAtPeriodEnd: boolean;
  seats: number;
}

export default function BillingPage() {
  const [subscription, setSubscription] = useState<Subscription | null>(null);
  const [loading, setLoading] = useState(true);
  const [portalLoading, setPortalLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    client
      .request<Subscription>("/api/v1/billing/subscription")
      .then(setSubscription)
      .catch((err) => setError(err instanceof Error ? err.message : "Load failed."))
      .finally(() => setLoading(false));
  }, []);

  async function openPortal() {
    setPortalLoading(true);
    try {
      const { url } = await client.request<{ url: string }>(
        "/api/v1/billing/customer-portal/session",
        { method: "POST" },
      );
      window.location.href = url;
    } catch (err) {
      setError(err instanceof Error ? err.message : "Portal error.");
    } finally {
      setPortalLoading(false);
    }
  }

  if (loading) return <p style={{ fontFamily: "sans-serif", padding: 32 }}>Loading...</p>;

  return (
    <main style={{ fontFamily: "sans-serif", padding: 32, maxWidth: 600 }}>
      <h1>Billing</h1>
      {error && <p style={{ color: "red" }}>{error}</p>}

      {subscription ? (
        <div style={{ background: "#f9f9f9", borderRadius: 8, padding: 24, marginBottom: 24 }}>
          <h2>{subscription.planName}</h2>
          <p>Status: <strong style={{ color: subscription.status === "active" ? "green" : "orange" }}>
            {subscription.status}
          </strong></p>
          <p>Seats: {subscription.seats}</p>
          <p>{subscription.cancelAtPeriodEnd ? "Cancels" : "Renews"}: {new Date(subscription.currentPeriodEnd).toLocaleDateString()}</p>
        </div>
      ) : (
        <p>No active subscription. <a href="/pricing">View plans</a></p>
      )}

      <button
        onClick={() => void openPortal()}
        disabled={portalLoading}
        style={{
          padding: "12px 24px",
          background: "#6366f1",
          color: "#fff",
          border: "none",
          borderRadius: 6,
          cursor: portalLoading ? "wait" : "pointer",
          fontWeight: 600,
        }}
      >
        {portalLoading ? "Opening..." : "Manage Billing in Stripe"}
      </button>
    </main>
  );
}
