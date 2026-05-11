"use client";

import * as React from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { Shield } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import {
  Form,
  FormControl,
  FormField,
  FormItem,
  FormLabel,
  FormMessage,
} from "@/components/ui/form";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";

const schema = z.object({
  code: z
    .string()
    .min(6, "Enter your 6-digit code")
    .max(8, "Code too long")
    .regex(/^\d+$/, "Digits only"),
});

type FormData = z.infer<typeof schema>;

interface MfaChallengeProps {
  mfaToken: string;
  onSuccess: () => void;
  onCancel?: () => void;
}

export function MfaChallenge({
  mfaToken,
  onSuccess,
  onCancel,
}: MfaChallengeProps): React.JSX.Element {
  const [serverError, setServerError] = React.useState<string | null>(null);

  const form = useForm<FormData>({
    resolver: zodResolver(schema),
    defaultValues: { code: "" },
  });

  async function onSubmit({ code }: FormData): Promise<void> {
    setServerError(null);
    const apiBase = process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5000";
    const res = await fetch(`${apiBase}/api/v1/identity/mfa/verify`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ code, mfa_token: mfaToken }),
    });

    if (!res.ok) {
      const data = (await res.json().catch(() => ({}))) as { detail?: string };
      setServerError(data.detail ?? "Verification failed. Try again.");
      return;
    }

    const data = (await res.json()) as {
      access_token: string;
      refresh_token?: string;
    };

    await fetch("/api/auth/callback", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        access_token: data.access_token,
        refresh_token: data.refresh_token,
      }),
    });

    onSuccess();
  }

  return (
    <Card className="w-full max-w-sm">
      <CardHeader className="text-center">
        <div className="mx-auto mb-2 flex h-12 w-12 items-center justify-center rounded-full bg-primary/10">
          <Shield className="h-6 w-6 text-primary" />
        </div>
        <CardTitle>Two-factor verification</CardTitle>
        <CardDescription>
          Enter the 6-digit code from your authenticator app.
        </CardDescription>
      </CardHeader>
      <CardContent>
        {serverError && (
          <p className="mb-4 rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive">
            {serverError}
          </p>
        )}
        <Form {...form}>
          <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-4">
            <FormField
              control={form.control}
              name="code"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Authentication code</FormLabel>
                  <FormControl>
                    <Input
                      {...field}
                      placeholder="000000"
                      maxLength={8}
                      inputMode="numeric"
                      autoComplete="one-time-code"
                      autoFocus
                    />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />
            <div className="flex gap-2">
              <Button
                type="submit"
                className="flex-1"
                disabled={form.formState.isSubmitting}
              >
                {form.formState.isSubmitting ? "Verifying..." : "Verify"}
              </Button>
              {onCancel && (
                <Button type="button" variant="outline" onClick={onCancel}>
                  Cancel
                </Button>
              )}
            </div>
          </form>
        </Form>
      </CardContent>
    </Card>
  );
}
