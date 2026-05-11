import { cookies } from "next/headers";
import { redirect } from "next/navigation";
import type { Metadata } from "next";
import Link from "next/link";
import {
  Users,
  CreditCard,
  Files,
  Bell,
  Webhook,
  Settings,
} from "lucide-react";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { apiGet } from "@/lib/api";

export const metadata: Metadata = { title: "Dashboard" };

interface Subscription {
  planName: string;
  status: string;
  currentPeriodEnd: string;
}

const quickLinks = [
  { href: "/members", label: "Team Members", icon: Users },
  { href: "/billing", label: "Billing", icon: CreditCard },
  { href: "/files", label: "Files", icon: Files },
  { href: "/notifications", label: "Notifications", icon: Bell },
  { href: "/webhooks", label: "Webhooks", icon: Webhook },
  { href: "/settings/profile", label: "Settings", icon: Settings },
];

export default async function DashboardPage(): Promise<React.JSX.Element> {
  const jar = await cookies();
  const token = jar.get("sb_token")?.value;
  if (!token) redirect("/login");

  const subscription = await apiGet<Subscription>(
    "/api/v1/billing/subscription",
    token,
  ).catch(() => null);

  return (
    <div className="space-y-6">
      {/* Subscription status */}
      {subscription && (
        <Card>
          <CardHeader>
            <CardTitle className="text-base">Subscription</CardTitle>
          </CardHeader>
          <CardContent className="flex items-center gap-4">
            <span className="font-semibold">{subscription.planName}</span>
            <Badge
              variant={subscription.status === "active" ? "default" : "secondary"}
            >
              {subscription.status}
            </Badge>
            <span className="ml-auto text-sm text-muted-foreground">
              Renews {new Date(subscription.currentPeriodEnd).toLocaleDateString()}
            </span>
          </CardContent>
        </Card>
      )}

      {/* Quick links */}
      <div>
        <h2 className="mb-4 text-sm font-semibold uppercase tracking-wide text-muted-foreground">
          Quick access
        </h2>
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {quickLinks.map(({ href, label, icon: Icon }) => (
            <Link
              key={href}
              href={href}
              className="flex items-center gap-3 rounded-lg border bg-card p-4 font-medium transition-colors hover:bg-accent hover:text-accent-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
            >
              <Icon className="h-5 w-5 text-muted-foreground" />
              {label}
            </Link>
          ))}
        </div>
      </div>
    </div>
  );
}
