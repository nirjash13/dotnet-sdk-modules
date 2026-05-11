/**
 * login.tsx — Copy-paste example for SaasBuilder login page.
 * Drop into app/(auth)/login/page.tsx in your Next.js project.
 * No build pipeline — illustrative source only.
 */
"use client";

import { useState } from "react";
import { SaasBuilderClient, MfaRequiredError, UnauthorizedError } from "@saasbuilder/client";

const client = new SaasBuilderClient({
  baseUrl: process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5000",
});

export default function LoginPage() {
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [mfaToken, setMfaToken] = useState<string | null>(null);
  const [mfaCode, setMfaCode] = useState("");
  const [loading, setLoading] = useState(false);
  const [magicLinkSent, setMagicLinkSent] = useState(false);

  async function handleLogin(e: React.FormEvent) {
    e.preventDefault();
    setLoading(true);
    setError(null);
    try {
      const form = new URLSearchParams({
        grant_type: "password",
        username: email,
        password,
        scope: "openid offline_access",
      });
      const res = await fetch(`${process.env.NEXT_PUBLIC_API_URL}/connect/token`, {
        method: "POST",
        headers: { "Content-Type": "application/x-www-form-urlencoded" },
        body: form.toString(),
      });
      if (!res.ok) throw new UnauthorizedError();
      const data = await res.json();
      if (data.requires_mfa) {
        setMfaToken(data.mfa_token);
        return;
      }
      await storeTokens(data.access_token, data.refresh_token);
      window.location.href = "/dashboard";
    } catch (err) {
      if (err instanceof MfaRequiredError) { setMfaToken(err.mfaToken); return; }
      setError(err instanceof Error ? err.message : "Login failed.");
    } finally {
      setLoading(false);
    }
  }

  async function handleMfa(e: React.FormEvent) {
    e.preventDefault();
    setLoading(true);
    try {
      await client.verifyMfa(mfaCode, mfaToken!);
      window.location.href = "/dashboard";
    } catch (err) {
      setError(err instanceof Error ? err.message : "MFA failed.");
    } finally {
      setLoading(false);
    }
  }

  async function handleMagicLink(e: React.FormEvent) {
    e.preventDefault();
    setLoading(true);
    try {
      await client.request("/api/v1/identity/magic-link", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ email }),
      });
      setMagicLinkSent(true);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to send link.");
    } finally {
      setLoading(false);
    }
  }

  async function storeTokens(token: string, refresh?: string) {
    await fetch("/api/auth/callback", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ access_token: token, refresh_token: refresh }),
    });
  }

  if (mfaToken) {
    return (
      <main style={{ maxWidth: 400, margin: "80px auto", fontFamily: "sans-serif" }}>
        <h1>Two-Factor Verification</h1>
        {error && <p style={{ color: "red" }}>{error}</p>}
        <form onSubmit={handleMfa}>
          <input value={mfaCode} onChange={e => setMfaCode(e.target.value)}
            placeholder="Enter 6-digit code" maxLength={6} style={{ display: "block", marginBottom: 8, width: "100%" }} />
          <button type="submit" disabled={loading}>Verify</button>
        </form>
      </main>
    );
  }

  return (
    <main style={{ maxWidth: 400, margin: "80px auto", fontFamily: "sans-serif" }}>
      <h1>Sign In</h1>
      {error && <p style={{ color: "red" }}>{error}</p>}
      {magicLinkSent
        ? <p>Check your email for a sign-in link.</p>
        : (
          <>
            <form onSubmit={handleLogin} style={{ marginBottom: 16 }}>
              <input value={email} onChange={e => setEmail(e.target.value)} type="email"
                placeholder="Email" required style={{ display: "block", marginBottom: 8, width: "100%" }} />
              <input value={password} onChange={e => setPassword(e.target.value)} type="password"
                placeholder="Password" required style={{ display: "block", marginBottom: 8, width: "100%" }} />
              <button type="submit" disabled={loading} style={{ width: "100%" }}>Sign in</button>
            </form>
            <form onSubmit={handleMagicLink}>
              <button type="submit" disabled={loading || !email} style={{ width: "100%", marginBottom: 8 }}>
                Send magic link to {email || "..."}
              </button>
            </form>
            <hr />
            <a href={`${process.env.NEXT_PUBLIC_API_URL}/connect/authorize?provider=Google&redirect_uri=${encodeURIComponent(window.location.origin + "/api/auth/callback")}`}>
              Sign in with Google
            </a>
          </>
        )}
    </main>
  );
}
