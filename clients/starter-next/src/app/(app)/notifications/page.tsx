import { cookies } from "next/headers";
import { redirect } from "next/navigation";
import type { Metadata } from "next";
import { NotificationFeed } from "@/components/notification-feed";

export const metadata: Metadata = { title: "Notifications" };

export default async function NotificationsPage(): Promise<React.JSX.Element> {
  const jar = await cookies();
  const token = jar.get("sb_token")?.value;
  if (!token) redirect("/login");

  return <NotificationFeed accessToken={token} />;
}
