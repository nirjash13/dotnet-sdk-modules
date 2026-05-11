"use client";

import * as React from "react";
import { MoreHorizontal } from "lucide-react";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import { Badge } from "@/components/ui/badge";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";
import { apiDelete } from "@/lib/api";
import { toast } from "@/components/ui/toast";

export interface Member {
  id: string;
  name: string;
  email: string;
  role: string;
  joinedAt: string;
}

interface MembersTableProps {
  members: Member[];
  organizationId: string;
  loading?: boolean;
  onMemberRemoved?: (memberId: string) => void;
  currentUserId?: string;
}

export function MembersTable({
  members,
  organizationId,
  loading = false,
  onMemberRemoved,
  currentUserId,
}: MembersTableProps): React.JSX.Element {
  async function removeMember(memberId: string, memberName: string): Promise<void> {
    await apiDelete(`/api/v1/organizations/${organizationId}/members/${memberId}`);
    toast({ title: "Member removed", description: `${memberName} has been removed.` });
    onMemberRemoved?.(memberId);
  }

  if (loading) {
    return (
      <div className="space-y-2">
        {Array.from({ length: 4 }).map((_, i) => (
          <Skeleton key={i} className="h-12 w-full" />
        ))}
      </div>
    );
  }

  if (members.length === 0) {
    return (
      <div className="rounded-lg border border-dashed p-8 text-center text-muted-foreground">
        No members yet. Invite someone to get started.
      </div>
    );
  }

  return (
    <Table>
      <TableHeader>
        <TableRow>
          <TableHead>Name</TableHead>
          <TableHead>Email</TableHead>
          <TableHead>Role</TableHead>
          <TableHead>Joined</TableHead>
          <TableHead className="w-10" />
        </TableRow>
      </TableHeader>
      <TableBody>
        {members.map((member) => (
          <TableRow key={member.id}>
            <TableCell className="font-medium">{member.name}</TableCell>
            <TableCell className="text-muted-foreground">{member.email}</TableCell>
            <TableCell>
              <Badge variant={member.role === "Admin" ? "default" : "secondary"}>
                {member.role}
              </Badge>
            </TableCell>
            <TableCell className="text-muted-foreground text-sm">
              {new Date(member.joinedAt).toLocaleDateString()}
            </TableCell>
            <TableCell>
              {member.id !== currentUserId && (
                <DropdownMenu>
                  <DropdownMenuTrigger asChild>
                    <Button variant="ghost" size="icon" aria-label="Member actions">
                      <MoreHorizontal className="h-4 w-4" />
                    </Button>
                  </DropdownMenuTrigger>
                  <DropdownMenuContent align="end">
                    <DropdownMenuItem
                      className="text-destructive focus:text-destructive"
                      onClick={() => void removeMember(member.id, member.name)}
                    >
                      Remove member
                    </DropdownMenuItem>
                  </DropdownMenuContent>
                </DropdownMenu>
              )}
            </TableCell>
          </TableRow>
        ))}
      </TableBody>
    </Table>
  );
}
