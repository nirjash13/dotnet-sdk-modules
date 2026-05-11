import type { Metadata } from "next";
import { WebhookManager } from "@/components/webhook-manager";

export const metadata: Metadata = { title: "Webhooks" };

export default function WebhooksPage(): React.JSX.Element {
  return <WebhookManager />;
}
