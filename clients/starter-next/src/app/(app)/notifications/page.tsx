// C-28 FIX: The server component no longer reads the sb_token cookie and passes it
// as a prop to the client component. Doing so serialised the access token into the
// RSC hydration JSON, defeating the HttpOnly-cookie design (any XSS on the page
// could read it from the SSR payload).
//
// Instead, the client component fetches a short-lived hub-scoped token from
// /api/signalr-token (a BFF route that reads the HttpOnly cookie server-side and
// exchanges it for a narrow-scoped hub token). The long-lived access token never
// reaches the client JS heap.
import { redirect } from "next/navigation";
import { cookies } from "next/headers";
import type { Metadata } from "next";
import { NotificationFeed } from "@/components/notification-feed";

export const metadata: Metadata = { title: "Notifications" };

export default async function NotificationsPage(): Promise<React.JSX.Element> {
  // Gate: ensure the user is authenticated (presence check — see middleware.ts C-27 note).
  // The actual token is NOT passed to the client component.
  const jar = await cookies();
  const isAuthenticated = Boolean(jar.get("sb_token")?.value);
  if (!isAuthenticated) redirect("/login");

  // NotificationFeed fetches /api/signalr-token itself using the HttpOnly cookie.
  return <NotificationFeed />;
}
