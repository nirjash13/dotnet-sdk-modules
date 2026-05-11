"use client";

import * as React from "react";
import { CreditCard, ExternalLink } from "lucide-react";
import type { Metadata } from "next";
import { Button } from "@/components/ui/button";
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Skeleton } from "@/components/ui/skeleton";
import { apiGet, apiPost } from "@/lib/api";
import { toast } from "@/components/ui/toast";

// Note: metadata export does not work in "use client" pages.
// Move to a server wrapper if SEO metadata is required.
// export const metadata: Metadata = { title: "Billing" };

interface Subscription {
  planName: string;
  status: string;
  currentPeriodEnd: string;
  cancelAtPeriodEnd: boolean;
  seats: number;
}

export default function BillingPage(): React.JSX.Element {
  const [subscription, setSubscription] = React.useState<Subscription | null>(
    null,
  );
  const [loading, setLoading] = React.useState(true);
  const [portalLoading, setPortalLoading] = React.useState(false);

  React.useEffect(() => {
    apiGet<Subscription>("/api/v1/billing/subscription")
      .then(setSubscription)
      .catch(() => setSubscription(null))
      .finally(() => setLoading(false));
  }, []);

  async function openPortal(): Promise<void> {
    setPortalLoading(true);
    try {
      const { url } = await apiPost<{ url: string }>(
        "/api/v1/billing/portal-session",
      );
      window.location.href = url;
    } catch (err) {
      toast({
        variant: "destructive",
        title: "Portal error",
        description: err instanceof Error ? err.message : "Please try again.",
      });
    } finally {
      setPortalLoading(false);
    }
  }

  return (
    <div className="max-w-xl space-y-6">
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2">
            <CreditCard className="h-5 w-5" />
            Subscription
          </CardTitle>
          <CardDescription>Manage your plan and billing details.</CardDescription>
        </CardHeader>
        <CardContent>
          {loading ? (
            <div className="space-y-2">
              <Skeleton className="h-6 w-40" />
              <Skeleton className="h-4 w-64" />
            </div>
          ) : subscription ? (
            <dl className="space-y-3">
              <div className="flex justify-between">
                <dt className="text-sm text-muted-foreground">Plan</dt>
                <dd className="font-semibold">{subscription.planName}</dd>
              </div>
              <div className="flex justify-between">
                <dt className="text-sm text-muted-foreground">Status</dt>
                <dd>
                  <Badge
                    variant={
                      subscription.status === "active" ? "default" : "secondary"
                    }
                  >
                    {subscription.status}
                  </Badge>
                </dd>
              </div>
              <div className="flex justify-between">
                <dt className="text-sm text-muted-foreground">Seats</dt>
                <dd className="font-medium">{subscription.seats}</dd>
              </div>
              <div className="flex justify-between">
                <dt className="text-sm text-muted-foreground">
                  {subscription.cancelAtPeriodEnd ? "Cancels" : "Renews"}
                </dt>
                <dd className="font-medium">
                  {new Date(subscription.currentPeriodEnd).toLocaleDateString()}
                </dd>
              </div>
            </dl>
          ) : (
            <p className="text-sm text-muted-foreground">
              No active subscription.{" "}
              <a href="/onboarding" className="underline">
                Pick a plan
              </a>
            </p>
          )}
        </CardContent>
      </Card>

      <Button
        onClick={() => void openPortal()}
        disabled={portalLoading}
        className="w-full sm:w-auto"
      >
        {portalLoading ? (
          "Opening..."
        ) : (
          <>
            <ExternalLink className="mr-2 h-4 w-4" />
            Manage billing in Stripe
          </>
        )}
      </Button>
    </div>
  );
}
