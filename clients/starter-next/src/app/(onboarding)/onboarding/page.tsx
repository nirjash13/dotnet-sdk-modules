"use client";

import * as React from "react";
import { useRouter } from "next/navigation";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { Building2, UserPlus, CreditCard } from "lucide-react";
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
import { PlanSelector, type Plan } from "@/components/plan-selector";
import { apiGet, apiPost } from "@/lib/api";

const orgSchema = z.object({
  name: z.string().min(2, "Organization name is required"),
});

type OrgFormData = z.infer<typeof orgSchema>;

interface Organization {
  id: string;
  name: string;
}

type Step = "org" | "invite" | "plan";

export default function OnboardingPage(): React.JSX.Element {
  const router = useRouter();
  const [step, setStep] = React.useState<Step>("org");
  const [org, setOrg] = React.useState<Organization | null>(null);
  const [plans, setPlans] = React.useState<Plan[]>([]);
  const [inviteEmail, setInviteEmail] = React.useState("");
  const [inviteError, setInviteError] = React.useState<string | null>(null);
  const [inviteSent, setInviteSent] = React.useState(false);

  const orgForm = useForm<OrgFormData>({
    resolver: zodResolver(orgSchema),
    defaultValues: { name: "" },
  });

  async function createOrg({ name }: OrgFormData): Promise<void> {
    const created = await apiPost<Organization>("/api/v1/organizations", { name });
    setOrg(created);
    setStep("invite");
  }

  async function sendInvite(): Promise<void> {
    if (!org || !inviteEmail) return;
    setInviteError(null);
    await apiPost(`/api/v1/organizations/${org.id}/invitations`, {
      email: inviteEmail,
      role: "Member",
    });
    setInviteSent(true);
    setInviteEmail("");
  }

  async function proceedToPlan(): Promise<void> {
    const data = await apiGet<{ items: Plan[] }>("/api/v1/billing/plans").catch(
      () => ({ items: [] }),
    );
    setPlans(data.items);
    setStep("plan");
  }

  const steps = [
    { key: "org", icon: Building2, label: "Create organization" },
    { key: "invite", icon: UserPlus, label: "Invite team" },
    { key: "plan", icon: CreditCard, label: "Pick a plan" },
  ] as const;

  return (
    <div className="flex min-h-screen flex-col items-center justify-center p-4">
      {/* Step indicator */}
      <nav aria-label="Onboarding steps" className="mb-8">
        <ol className="flex items-center gap-2">
          {steps.map(({ key, icon: Icon, label }, i) => {
            const stepIndex = steps.findIndex((s) => s.key === step);
            const isCurrent = key === step;
            const isDone = steps.findIndex((s) => s.key === key) < stepIndex;
            return (
              <React.Fragment key={key}>
                <li className="flex items-center gap-2">
                  <div
                    className={`flex h-8 w-8 items-center justify-center rounded-full text-sm font-medium ${
                      isCurrent
                        ? "bg-primary text-primary-foreground"
                        : isDone
                          ? "bg-primary/20 text-primary"
                          : "bg-muted text-muted-foreground"
                    }`}
                    aria-current={isCurrent ? "step" : undefined}
                  >
                    <Icon className="h-4 w-4" />
                  </div>
                  <span
                    className={`hidden text-sm sm:block ${isCurrent ? "font-semibold" : "text-muted-foreground"}`}
                  >
                    {label}
                  </span>
                </li>
                {i < steps.length - 1 && (
                  <li aria-hidden className="h-px w-8 bg-border" />
                )}
              </React.Fragment>
            );
          })}
        </ol>
      </nav>

      {/* Step content */}
      {step === "org" && (
        <Card className="w-full max-w-sm">
          <CardHeader>
            <CardTitle>Name your organization</CardTitle>
            <CardDescription>
              This is how your team will be identified across the platform.
            </CardDescription>
          </CardHeader>
          <CardContent>
            <Form {...orgForm}>
              <form onSubmit={orgForm.handleSubmit(createOrg)} className="space-y-4">
                <FormField
                  control={orgForm.control}
                  name="name"
                  render={({ field }) => (
                    <FormItem>
                      <FormLabel>Organization name</FormLabel>
                      <FormControl>
                        <Input {...field} placeholder="Acme Corp" autoFocus />
                      </FormControl>
                      <FormMessage />
                    </FormItem>
                  )}
                />
                <Button
                  type="submit"
                  className="w-full"
                  disabled={orgForm.formState.isSubmitting}
                >
                  {orgForm.formState.isSubmitting ? "Creating..." : "Create & continue"}
                </Button>
              </form>
            </Form>
          </CardContent>
        </Card>
      )}

      {step === "invite" && org && (
        <Card className="w-full max-w-sm">
          <CardHeader>
            <CardTitle>Invite your team</CardTitle>
            <CardDescription>
              Add colleagues to <strong>{org.name}</strong>. You can always do
              this later.
            </CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            {inviteError && (
              <p className="text-sm text-destructive">{inviteError}</p>
            )}
            {inviteSent && (
              <p className="text-sm text-green-600">Invitation sent!</p>
            )}
            <div className="flex gap-2">
              <Input
                type="email"
                placeholder="colleague@example.com"
                value={inviteEmail}
                onChange={(e) => setInviteEmail(e.target.value)}
                onKeyDown={(e) => {
                  if (e.key === "Enter") { e.preventDefault(); void sendInvite(); }
                }}
              />
              <Button
                variant="outline"
                onClick={() => void sendInvite()}
                disabled={!inviteEmail}
              >
                Invite
              </Button>
            </div>
            <Button className="w-full" onClick={() => void proceedToPlan()}>
              Continue to plans
            </Button>
          </CardContent>
        </Card>
      )}

      {step === "plan" && org && (
        <div className="w-full max-w-4xl">
          <div className="mb-6 text-center">
            <h2 className="text-2xl font-bold">Choose a plan</h2>
            <p className="text-muted-foreground">
              You can upgrade or downgrade at any time.
            </p>
          </div>
          <PlanSelector plans={plans} organizationId={org.id} />
          <div className="mt-6 text-center">
            <Button
              variant="ghost"
              onClick={() => router.push("/dashboard")}
            >
              Skip for now
            </Button>
          </div>
        </div>
      )}
    </div>
  );
}
