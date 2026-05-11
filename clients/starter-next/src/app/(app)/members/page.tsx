import { cookies } from "next/headers";
import { redirect } from "next/navigation";
import type { Metadata } from "next";
import { apiGet } from "@/lib/api";
import { MembersTable, type Member } from "@/components/members-table";
import { InviteModal } from "@/components/invite-modal";

export const metadata: Metadata = { title: "Members" };

interface Organization {
  id: string;
  name: string;
}

interface MembersResponse {
  items: Member[];
}

interface Me {
  id: string;
}

export default async function MembersPage(): Promise<React.JSX.Element> {
  const jar = await cookies();
  const token = jar.get("sb_token")?.value;
  if (!token) redirect("/login");

  const [org, me] = await Promise.all([
    apiGet<Organization>("/api/v1/organizations/current", token).catch(
      () => null,
    ),
    apiGet<Me>("/api/v1/identity/me", token).catch(() => null),
  ]);

  const members = org
    ? await apiGet<MembersResponse>(
        `/api/v1/organizations/${org.id}/members`,
        token,
      ).catch(() => ({ items: [] }))
    : { items: [] };

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h2 className="text-lg font-semibold">Team members</h2>
        {org && <InviteModal organizationId={org.id} />}
      </div>
      <MembersTable
        members={members.items}
        organizationId={org?.id ?? ""}
        currentUserId={me?.id}
      />
    </div>
  );
}
