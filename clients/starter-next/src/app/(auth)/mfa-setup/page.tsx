"use client";

import * as React from "react";
import { useRouter } from "next/navigation";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { Shield, Copy, CheckCheck } from "lucide-react";
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
import { Skeleton } from "@/components/ui/skeleton";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { apiGet, apiPost } from "@/lib/api";

interface TotpSetupResponse {
  qrCodeUri: string;
  manualKey: string;
}

interface RecoveryCodesResponse {
  codes: string[];
}

const verifySchema = z.object({
  code: z
    .string()
    .min(6, "Enter the 6-digit code")
    .max(6)
    .regex(/^\d{6}$/, "Digits only"),
});

type VerifyData = z.infer<typeof verifySchema>;

type Step = "qr" | "verify" | "recovery";

export default function MfaSetupPage(): React.JSX.Element {
  const router = useRouter();
  const [step, setStep] = React.useState<Step>("qr");
  const [setup, setSetup] = React.useState<TotpSetupResponse | null>(null);
  const [recoveryCodes, setRecoveryCodes] = React.useState<string[]>([]);
  const [copied, setCopied] = React.useState(false);
  const [serverError, setServerError] = React.useState<string | null>(null);

  const form = useForm<VerifyData>({
    resolver: zodResolver(verifySchema),
    defaultValues: { code: "" },
  });

  React.useEffect(() => {
    apiGet<TotpSetupResponse>("/api/v1/identity/mfa/setup/totp")
      .then(setSetup)
      .catch(() =>
        setServerError("Failed to load setup. Refresh to try again."),
      );
  }, []);

  async function onVerify({ code }: VerifyData): Promise<void> {
    setServerError(null);
    const res = await apiPost<RecoveryCodesResponse>(
      "/api/v1/identity/mfa/verify",
      { code },
    );
    setRecoveryCodes(res.codes);
    setStep("recovery");
  }

  async function copyRecoveryCodes(): Promise<void> {
    await navigator.clipboard.writeText(recoveryCodes.join("\n"));
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  }

  return (
    <div className="flex min-h-screen items-center justify-center p-4">
      <Card className="w-full max-w-md">
        <CardHeader className="text-center">
          <div className="mx-auto mb-2 flex h-12 w-12 items-center justify-center rounded-full bg-primary/10">
            <Shield className="h-6 w-6 text-primary" />
          </div>
          <CardTitle>Set up two-factor authentication</CardTitle>
          <CardDescription>
            Protect your account with an authenticator app.
          </CardDescription>
        </CardHeader>
        <CardContent>
          {serverError && (
            <p className="mb-4 rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive">
              {serverError}
            </p>
          )}

          <Tabs value={step} onValueChange={(v) => setStep(v as Step)}>
            <TabsList className="w-full">
              <TabsTrigger value="qr" className="flex-1">1. Scan</TabsTrigger>
              <TabsTrigger value="verify" className="flex-1">2. Verify</TabsTrigger>
              <TabsTrigger value="recovery" className="flex-1" disabled={recoveryCodes.length === 0}>
                3. Codes
              </TabsTrigger>
            </TabsList>

            <TabsContent value="qr" className="mt-4 space-y-4">
              <p className="text-sm text-muted-foreground">
                Scan this QR code with Google Authenticator, Authy, or any TOTP
                app.
              </p>
              {setup ? (
                <>
                  {/* eslint-disable-next-line @next/next/no-img-element */}
                  <img
                    src={setup.qrCodeUri}
                    alt="TOTP QR code"
                    className="mx-auto h-48 w-48 rounded-lg border"
                  />
                  <div className="rounded-md bg-muted p-3">
                    <p className="mb-1 text-xs text-muted-foreground">
                      Or enter manually:
                    </p>
                    <code className="text-sm break-all">{setup.manualKey}</code>
                  </div>
                </>
              ) : (
                <Skeleton className="mx-auto h-48 w-48" />
              )}
              <Button className="w-full" onClick={() => setStep("verify")}>
                Next: verify
              </Button>
            </TabsContent>

            <TabsContent value="verify" className="mt-4 space-y-4">
              <p className="text-sm text-muted-foreground">
                Enter the code shown in your authenticator app to confirm setup.
              </p>
              <Form {...form}>
                <form onSubmit={form.handleSubmit(onVerify)} className="space-y-4">
                  <FormField
                    control={form.control}
                    name="code"
                    render={({ field }) => (
                      <FormItem>
                        <FormLabel>6-digit code</FormLabel>
                        <FormControl>
                          <Input
                            {...field}
                            placeholder="000000"
                            maxLength={6}
                            inputMode="numeric"
                            autoComplete="one-time-code"
                            autoFocus
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
                    {form.formState.isSubmitting ? "Verifying..." : "Enable 2FA"}
                  </Button>
                </form>
              </Form>
            </TabsContent>

            <TabsContent value="recovery" className="mt-4 space-y-4">
              <p className="text-sm text-muted-foreground">
                Save these recovery codes somewhere safe. Each code can only be
                used once.
              </p>
              <div className="rounded-lg bg-muted p-4">
                <ul className="grid grid-cols-2 gap-1">
                  {recoveryCodes.map((code) => (
                    <li key={code} className="font-mono text-sm">
                      {code}
                    </li>
                  ))}
                </ul>
              </div>
              <div className="flex gap-2">
                <Button
                  variant="outline"
                  className="flex-1"
                  onClick={() => void copyRecoveryCodes()}
                >
                  {copied ? (
                    <CheckCheck className="mr-2 h-4 w-4" />
                  ) : (
                    <Copy className="mr-2 h-4 w-4" />
                  )}
                  {copied ? "Copied!" : "Copy codes"}
                </Button>
                <Button className="flex-1" onClick={() => router.push("/dashboard")}>
                  Done
                </Button>
              </div>
            </TabsContent>
          </Tabs>
        </CardContent>
      </Card>
    </div>
  );
}
