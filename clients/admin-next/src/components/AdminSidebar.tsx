"use client";

import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
import {
  Users,
  Briefcase,
  Webhook,
  Flag,
  LogOut,
  Shield,
} from "lucide-react";
import { cn } from "@/lib/utils";
import { clearToken } from "@/lib/api";

const navItems = [
  { href: "/admin/tenants", label: "Tenants", icon: Users },
  { href: "/admin/jobs", label: "Background Jobs", icon: Briefcase },
  { href: "/admin/webhooks", label: "Webhooks", icon: Webhook },
  { href: "/admin/feature-flags", label: "Feature Flags", icon: Flag },
];

export function AdminSidebar() {
  const pathname = usePathname();
  const router = useRouter();

  function handleSignOut() {
    clearToken();
    document.cookie =
      "admin_access_token=; path=/; expires=Thu, 01 Jan 1970 00:00:00 GMT";
    router.push("/auth/login");
  }

  return (
    <aside className="flex h-full w-64 flex-col bg-sidebar text-sidebar-foreground">
      <div className="flex items-center gap-2 px-6 py-5 border-b border-sidebar-border">
        <Shield className="h-6 w-6 text-sidebar-primary" />
        <span className="text-lg font-semibold text-sidebar-primary">
          SaasBuilder Admin
        </span>
      </div>

      <nav className="flex-1 overflow-y-auto px-3 py-4 space-y-1" aria-label="Admin navigation">
        {navItems.map(({ href, label, icon: Icon }) => {
          const active = pathname.startsWith(href);
          return (
            <Link
              key={href}
              href={href}
              aria-current={active ? "page" : undefined}
              className={cn(
                "flex items-center gap-3 rounded-md px-3 py-2 text-sm font-medium transition-colors",
                active
                  ? "bg-sidebar-accent text-sidebar-accent-foreground"
                  : "text-sidebar-foreground hover:bg-sidebar-accent hover:text-sidebar-accent-foreground",
              )}
            >
              <Icon className="h-4 w-4 shrink-0" />
              {label}
            </Link>
          );
        })}
      </nav>

      <div className="border-t border-sidebar-border p-3">
        <button
          onClick={handleSignOut}
          className="flex w-full items-center gap-3 rounded-md px-3 py-2 text-sm font-medium text-sidebar-foreground hover:bg-sidebar-accent transition-colors"
        >
          <LogOut className="h-4 w-4" />
          Sign Out
        </button>
      </div>
    </aside>
  );
}
