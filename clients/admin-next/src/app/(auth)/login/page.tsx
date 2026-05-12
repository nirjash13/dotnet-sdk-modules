"use client";

// C-22 + C-23 FIX:
// - Credentials are sent to /api/admin/auth/login (BFF server route) instead of
//   directly to /connect/token from the browser.
// - The BFF validates role=admin server-side via /userinfo (signed response) before
//   setting the HttpOnly + Secure + SameSite=Strict cookie.
// - Client-side JWT decoding has been removed — the unsigned JWT body cannot be
//   trusted for authorization decisions.
// - setToken() / sessionStorage writes have been removed — the token lives only in
//   the HttpOnly cookie managed by the server.

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

const loginSchema = z.object({
  email: z.string().email("Enter a valid email address"),
  password: z.string().min(1, "Password is required"),
});

type LoginForm = z.infer<typeof loginSchema>;

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

    // Credentials go to the BFF server route — never directly to the backend from the browser.
    // The BFF validates role=admin via /userinfo (server-to-server) before setting an
    // HttpOnly cookie. No token ever appears in the browser JS heap.
    const response = await fetch("/api/admin/auth/login", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ email: data.email, password: data.password }),
    });

    if (response.status === 403) {
      setServerError("Your account does not have admin access.");
      return;
    }

    if (!response.ok) {
      setServerError("Invalid credentials or server error. Please try again.");
      return;
    }

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
