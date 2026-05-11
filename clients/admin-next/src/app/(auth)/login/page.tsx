"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { z } from "zod";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { Shield } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from "@/components/ui/card";
import { setToken } from "@/lib/api";

const loginSchema = z.object({
  email: z.string().email("Enter a valid email address"),
  password: z.string().min(1, "Password is required"),
});

type LoginForm = z.infer<typeof loginSchema>;

interface TokenResponse {
  access_token: string;
  token_type: string;
}

export default function AdminLoginPage() {
  const router = useRouter();
  const [serverError, setServerError] = useState<string | null>(null);

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<LoginForm>({
    resolver: zodResolver(loginSchema),
  });

  async function onSubmit(data: LoginForm) {
    setServerError(null);

    const apiBase =
      process.env.NEXT_PUBLIC_API_BASE_URL ?? "http://localhost:5000";

    const body = new URLSearchParams({
      grant_type: "password",
      username: data.email,
      password: data.password,
      scope: "openid offline_access",
      client_id: process.env.NEXT_PUBLIC_ADMIN_CLIENT_ID ?? "admin-ui",
    });

    const response = await fetch(`${apiBase}/connect/token`, {
      method: "POST",
      headers: { "Content-Type": "application/x-www-form-urlencoded" },
      body: body.toString(),
    });

    if (!response.ok) {
      setServerError("Invalid credentials or insufficient permissions.");
      return;
    }

    const json: TokenResponse = await response.json();

    // Decode JWT to verify role=admin claim before storing
    const payload = JSON.parse(atob(json.access_token.split(".")[1]!)) as Record<string, unknown>;
    const role = payload["role"] ?? payload["http://schemas.microsoft.com/ws/2008/06/identity/claims/role"];

    if (role !== "admin" && !Array.isArray(role)) {
      setServerError("Your account does not have admin access.");
      return;
    }

    setToken(json.access_token);
    // Also store in a cookie so server-side middleware can read the token.
    document.cookie = `admin_access_token=${json.access_token}; path=/; SameSite=Strict`;
    router.push("/admin/tenants");
  }

  return (
    <div className="flex min-h-screen items-center justify-center bg-muted/50">
      <Card className="w-full max-w-sm">
        <CardHeader className="text-center">
          <div className="mb-3 flex justify-center">
            <Shield className="h-10 w-10 text-primary" />
          </div>
          <CardTitle>Admin Sign In</CardTitle>
          <CardDescription>Platform operators only</CardDescription>
        </CardHeader>
        <CardContent>
          <form onSubmit={handleSubmit(onSubmit)} className="space-y-4" noValidate>
            {serverError && (
              <p className="rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive">
                {serverError}
              </p>
            )}

            <div className="space-y-1">
              <Label htmlFor="email">Email</Label>
              <Input
                id="email"
                type="email"
                autoComplete="email"
                {...register("email")}
              />
              {errors.email && (
                <p className="text-xs text-destructive">{errors.email.message}</p>
              )}
            </div>

            <div className="space-y-1">
              <Label htmlFor="password">Password</Label>
              <Input
                id="password"
                type="password"
                autoComplete="current-password"
                {...register("password")}
              />
              {errors.password && (
                <p className="text-xs text-destructive">{errors.password.message}</p>
              )}
            </div>

            <Button type="submit" className="w-full" disabled={isSubmitting}>
              {isSubmitting ? "Signing in..." : "Sign In"}
            </Button>
          </form>
        </CardContent>
      </Card>
    </div>
  );
}
