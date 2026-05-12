"use client";

import * as React from "react";
import { useRouter, useSearchParams } from "next/navigation";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { Github, Mail, Loader2 } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card";
import {
  Form,
  FormControl,
  FormField,
  FormItem,
  FormLabel,
  FormMessage,
} from "@/components/ui/form";
import { MfaChallenge } from "@/components/mfa-challenge";

const API = process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5000";

const schema = z.object({
  email: z.string().email("Enter a valid email"),
  password: z.string().min(1, "Password is required"),
});

type FormData = z.infer<typeof schema>;

type AuthState =
  | { kind: "idle" }
  | { kind: "magic-link-sent" }
  | { kind: "mfa"; mfaToken: string };

export default function LoginPage(): React.JSX.Element {
  const router = useRouter();
  const searchParams = useSearchParams();
  const next = searchParams.get("next") ?? "/dashboard";

  const [authState, setAuthState] = React.useState<AuthState>({ kind: "idle" });
  const [serverError, setServerError] = React.useState<string | null>(null);
  const [magicLinkLoading, setMagicLinkLoading] = React.useState(false);

  const form = useForm<FormData>({
    resolver: zodResolver(schema),
    defaultValues: { email: "", password: "" },
  });

  async function onSubmit({ email, password }: FormData): Promise<void> {
    setServerError(null);

    // C-29 FIX: Credentials are sent to the BFF server route (/api/auth/login),
    // which performs the token exchange server-to-server. The raw access token never
    // reaches the browser JS heap. The BFF sets HttpOnly cookies on success.
    // The old pattern (ROPC directly from the browser + /api/auth/callback injection)
    // has been removed.
    const res = await fetch("/api/auth/login", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ email, password }),
    });

    const data = (await res.json().catch(() => ({}))) as {
      ok?: boolean;
      error?: string;
      mfa_required?: boolean;
      mfa_token?: string;
    };

    if (!res.ok) {
      setServerError("Invalid credentials.");
      return;
    }

    if (data.mfa_required && data.mfa_token) {
      setAuthState({ kind: "mfa", mfaToken: data.mfa_token });
      return;
    }

    router.push(next);
  }

  async function handleMagicLink(): Promise<void> {
    const email = form.getValues("email");
    if (!email) {
      form.setError("email", { message: "Enter your email first" });
      return;
    }

    setMagicLinkLoading(true);
    setServerError(null);

    const res = await fetch(`${API}/api/v1/identity/magic-link`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ email }),
    });

    setMagicLinkLoading(false);

    if (!res.ok) {
      setServerError("Failed to send magic link. Try again.");
      return;
    }

    setAuthState({ kind: "magic-link-sent" });
  }

  function buildSocialUrl(provider: string): string {
    // C-30 FIX: The redirect_uri must be our own origin only.
    // We use the compile-time NEXT_PUBLIC_APP_URL env var (set at build time) as
    // the canonical origin rather than window.location.origin, which an open-redirect
    // attacker could influence by rendering this component on a phishing page.
    // If the env var is absent, we fall back to window.location.origin but assert
    // it matches one of the statically-known allowed origins.
    const ALLOWED_ORIGINS: string[] = (
      process.env.NEXT_PUBLIC_ALLOWED_ORIGINS ?? process.env.NEXT_PUBLIC_APP_URL ?? ""
    )
      .split(",")
      .map((o) => o.trim())
      .filter(Boolean);

    const origin = window.location.origin;
    const safeOrigin =
      ALLOWED_ORIGINS.length > 0 && ALLOWED_ORIGINS.includes(origin)
        ? origin
        : ALLOWED_ORIGINS[0] ?? origin;

    // Social login goes through the backend HostedUI which validates redirect_uri
    // against its registered allowlist before issuing the code.
    const redirect = encodeURIComponent(`${safeOrigin}/api/auth/code-callback`);
    return `${API}/connect/authorize?provider=${encodeURIComponent(provider)}&redirect_uri=${redirect}`;
  }

  if (authState.kind === "mfa") {
    return (
      <div className="flex min-h-screen items-center justify-center p-4">
        <MfaChallenge
          mfaToken={authState.mfaToken}
          onSuccess={() => router.push(next)}
          onCancel={() => setAuthState({ kind: "idle" })}
        />
      </div>
    );
  }

  if (authState.kind === "magic-link-sent") {
    return (
      <div className="flex min-h-screen items-center justify-center p-4">
        <Card className="w-full max-w-sm text-center">
          <CardHeader>
            <CardTitle>Check your email</CardTitle>
            <CardDescription>
              We sent a sign-in link to{" "}
              <strong>{form.getValues("email")}</strong>. It expires in 15
              minutes.
            </CardDescription>
          </CardHeader>
          <CardContent>
            <Button variant="outline" onClick={() => setAuthState({ kind: "idle" })}>
              Back to sign in
            </Button>
          </CardContent>
        </Card>
      </div>
    );
  }

  return (
    <div className="flex min-h-screen items-center justify-center p-4">
      <Card className="w-full max-w-sm">
        <CardHeader>
          <CardTitle className="text-2xl">Sign in</CardTitle>
          <CardDescription>
            Access your account using email &amp; password, magic link, or a
            social provider.
          </CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          {serverError && (
            <p
              role="alert"
              className="rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive"
            >
              {serverError}
            </p>
          )}

          {/* Local credentials */}
          <Form {...form}>
            <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-3">
              <FormField
                control={form.control}
                name="email"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Email</FormLabel>
                    <FormControl>
                      <Input
                        {...field}
                        type="email"
                        placeholder="you@example.com"
                        autoComplete="email"
                      />
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />
              <FormField
                control={form.control}
                name="password"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Password</FormLabel>
                    <FormControl>
                      <Input
                        {...field}
                        type="password"
                        placeholder="••••••••"
                        autoComplete="current-password"
                      />
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />
              <Button
                type="submit"
                className="w-full"
                disabled={form.formState.isSubmitting}
              >
                {form.formState.isSubmitting && (
                  <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                )}
                Sign in
              </Button>
            </form>
          </Form>

          {/* Magic link */}
          <Button
            variant="outline"
            className="w-full"
            disabled={magicLinkLoading}
            onClick={() => void handleMagicLink()}
          >
            {magicLinkLoading ? (
              <Loader2 className="mr-2 h-4 w-4 animate-spin" />
            ) : (
              <Mail className="mr-2 h-4 w-4" />
            )}
            Send magic link
          </Button>

          <div className="relative">
            <div className="absolute inset-0 flex items-center">
              <span className="w-full border-t" />
            </div>
            <div className="relative flex justify-center text-xs uppercase">
              <span className="bg-card px-2 text-muted-foreground">or</span>
            </div>
          </div>

          {/* Social providers */}
          <div className="grid grid-cols-2 gap-2">
            {(["Google", "Microsoft"] as const).map((provider) => (
              <Button
                key={provider}
                variant="outline"
                asChild
              >
                <a href={buildSocialUrl(provider)}>
                  {provider}
                </a>
              </Button>
            ))}
            <Button variant="outline" asChild>
              <a href={buildSocialUrl("GitHub")}>
                <Github className="mr-2 h-4 w-4" />
                GitHub
              </a>
            </Button>
            <Button variant="outline" asChild>
              <a href={buildSocialUrl("Apple")}>
                Apple
              </a>
            </Button>
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
