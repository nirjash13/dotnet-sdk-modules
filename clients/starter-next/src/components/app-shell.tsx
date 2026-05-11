"use client";

import * as React from "react";
import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
import {
  LayoutDashboard,
  Users,
  CreditCard,
  Files,
  Bell,
  Webhook,
  Settings,
  LogOut,
  ChevronRight,
  AlertTriangle,
} from "lucide-react";
import { cn } from "@/lib/cn";
import { Button } from "@/components/ui/button";

interface NavItem {
  href: string;
  label: string;
  icon: React.ComponentType<{ className?: string }>;
}

const NAV_ITEMS: NavItem[] = [
  { href: "/dashboard", label: "Dashboard", icon: LayoutDashboard },
  { href: "/members", label: "Members", icon: Users },
  { href: "/billing", label: "Billing", icon: CreditCard },
  { href: "/files", label: "Files", icon: Files },
  { href: "/notifications", label: "Notifications", icon: Bell },
  { href: "/webhooks", label: "Webhooks", icon: Webhook },
  { href: "/settings/profile", label: "Settings", icon: Settings },
];

interface AppShellProps {
  children: React.ReactNode;
  userName?: string;
  userEmail?: string;
  tenantName?: string;
  logoUrl?: string | null;
  /** When set, shows an impersonation banner at the top. */
  impersonating?: string;
}

export function AppShell({
  children,
  userName,
  userEmail,
  tenantName,
  logoUrl,
  impersonating,
}: AppShellProps): React.JSX.Element {
  const pathname = usePathname();
  const router = useRouter();

  async function handleLogout(): Promise<void> {
    await fetch("/api/auth/logout", { method: "POST" });
    router.push("/login");
  }

  return (
    <div className="flex h-screen overflow-hidden bg-background">
      {/* Sidebar */}
      <aside className="flex w-64 flex-col border-r bg-card">
        {/* Tenant header */}
        <div className="flex h-16 items-center gap-3 border-b px-4">
          {logoUrl && (
            // eslint-disable-next-line @next/next/no-img-element
            <img src={logoUrl} alt={tenantName ?? "Logo"} className="h-8 w-8 rounded object-cover" />
          )}
          <span className="truncate font-semibold">{tenantName ?? "My App"}</span>
        </div>

        {/* Navigation */}
        <nav className="flex-1 overflow-y-auto p-2" aria-label="Main navigation">
          <ul className="space-y-1">
            {NAV_ITEMS.map(({ href, label, icon: Icon }) => {
              const active = pathname.startsWith(href);
              return (
                <li key={href}>
                  <Link
                    href={href}
                    className={cn(
                      "flex items-center gap-3 rounded-md px-3 py-2 text-sm font-medium transition-colors",
                      active
                        ? "bg-primary text-primary-foreground"
                        : "text-muted-foreground hover:bg-accent hover:text-accent-foreground",
                    )}
                    aria-current={active ? "page" : undefined}
                  >
                    <Icon className="h-4 w-4 shrink-0" />
                    {label}
                    {active && <ChevronRight className="ml-auto h-3 w-3" />}
                  </Link>
                </li>
              );
            })}
          </ul>
        </nav>

        {/* User footer */}
        <div className="border-t p-4">
          {userName && (
            <div className="mb-3">
              <p className="truncate text-sm font-medium">{userName}</p>
              <p className="truncate text-xs text-muted-foreground">{userEmail}</p>
            </div>
          )}
          <Button
            variant="ghost"
            size="sm"
            className="w-full justify-start"
            onClick={() => void handleLogout()}
          >
            <LogOut className="mr-2 h-4 w-4" />
            Sign out
          </Button>
        </div>
      </aside>

      {/* Main area */}
      <div className="flex flex-1 flex-col overflow-hidden">
        {/* Impersonation banner */}
        {impersonating && (
          <div
            className="flex items-center gap-2 bg-amber-500 px-4 py-2 text-sm font-medium text-white"
            role="alert"
          >
            <AlertTriangle className="h-4 w-4" />
            You are viewing as <strong>{impersonating}</strong>. Actions will affect their account.
          </div>
        )}

        {/* Topbar */}
        <header className="flex h-14 items-center border-b px-6">
          <h1 className="text-base font-semibold capitalize">
            {pathname.split("/").at(-1)?.replace(/-/g, " ") ?? ""}
          </h1>
        </header>

        {/* Page content */}
        <main className="flex-1 overflow-y-auto p-6">{children}</main>
      </div>
    </div>
  );
}
