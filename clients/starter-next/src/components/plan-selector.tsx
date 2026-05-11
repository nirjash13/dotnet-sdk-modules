"use client";

import * as React from "react";
import { Check } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardFooter, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { apiPost } from "@/lib/api";
import { redirectToCheckout } from "@/lib/stripe";
import { toast } from "@/components/ui/toast";

export interface Plan {
  id: string;
  name: string;
  description: string;
  priceMonthly: number;
  currency: string;
  features: string[];
  isCurrent?: boolean;
  isPopular?: boolean;
}

interface CheckoutSessionResponse {
  sessionId?: string;
  url?: string;
}

interface PlanSelectorProps {
  plans: Plan[];
  organizationId?: string;
}

export function PlanSelector({
  plans,
  organizationId,
}: PlanSelectorProps): React.JSX.Element {
  const [selecting, setSelecting] = React.useState<string | null>(null);

  async function selectPlan(planId: string): Promise<void> {
    setSelecting(planId);
    try {
      const data = await apiPost<CheckoutSessionResponse>(
        "/api/v1/billing/checkout-session",
        {
          planId,
          organizationId,
          successUrl: `${window.location.origin}/billing?success=1`,
          cancelUrl: window.location.href,
        },
      );

      if (data.sessionId) {
        await redirectToCheckout(data.sessionId);
      } else if (data.url) {
        window.location.href = data.url;
      }
    } catch (err) {
      toast({
        variant: "destructive",
        title: "Checkout failed",
        description: err instanceof Error ? err.message : "Please try again.",
      });
    } finally {
      setSelecting(null);
    }
  }

  if (plans.length === 0) {
    return (
      <div className="rounded-lg border border-dashed p-8 text-center text-muted-foreground">
        No plans available.
      </div>
    );
  }

  return (
    <div className="grid gap-6 sm:grid-cols-2 lg:grid-cols-3">
      {plans.map((plan) => (
        <Card
          key={plan.id}
          className={plan.isPopular ? "border-primary shadow-md" : undefined}
        >
          <CardHeader>
            <div className="flex items-center justify-between">
              <CardTitle>{plan.name}</CardTitle>
              {plan.isPopular && <Badge>Popular</Badge>}
            </div>
            <CardDescription>{plan.description}</CardDescription>
          </CardHeader>
          <CardContent>
            <p className="mb-4 text-3xl font-bold">
              {new Intl.NumberFormat("en-US", {
                style: "currency",
                currency: plan.currency,
                minimumFractionDigits: 0,
              }).format(plan.priceMonthly)}
              <span className="text-sm font-normal text-muted-foreground">/mo</span>
            </p>
            <ul className="space-y-2">
              {plan.features.map((feature) => (
                <li key={feature} className="flex items-center gap-2 text-sm">
                  <Check className="h-4 w-4 shrink-0 text-primary" />
                  {feature}
                </li>
              ))}
            </ul>
          </CardContent>
          <CardFooter>
            <Button
              className="w-full"
              variant={plan.isCurrent ? "outline" : "default"}
              disabled={plan.isCurrent || selecting === plan.id}
              onClick={() => void selectPlan(plan.id)}
            >
              {plan.isCurrent
                ? "Current plan"
                : selecting === plan.id
                  ? "Redirecting..."
                  : "Get started"}
            </Button>
          </CardFooter>
        </Card>
      ))}
    </div>
  );
}
