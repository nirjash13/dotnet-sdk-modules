"use client";

import { useEffect, useState } from "react";
import { api } from "@/lib/api";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";

type JobState = "Scheduled" | "Running" | "Succeeded" | "Failed";

interface Job {
  id: string;
  name: string;
  queue: string;
  state: JobState;
  scheduledAt: string;
  startedAt: string | null;
  finishedAt: string | null;
  errorMessage: string | null;
}

interface JobsResponse {
  items: Job[];
  total: number;
}

const STATE_TABS: JobState[] = ["Scheduled", "Running", "Succeeded", "Failed"];

function stateBadgeVariant(
  state: JobState,
): "default" | "success" | "destructive" | "warning" {
  switch (state) {
    case "Succeeded":
      return "success";
    case "Failed":
      return "destructive";
    case "Running":
      return "warning";
    default:
      return "default";
  }
}

export default function JobsPage() {
  const [activeTab, setActiveTab] = useState<JobState>("Scheduled");
  const [jobs, setJobs] = useState<Job[]>([]);
  const [total, setTotal] = useState(0);
  const [loading, setLoading] = useState(false);
  const [actionId, setActionId] = useState<string | null>(null);

  useEffect(() => {
    async function load() {
      setLoading(true);
      try {
        const result = await api.get<JobsResponse>(
          `/api/v1/admin/jobs?state=${activeTab}&pageSize=50`,
        );
        setJobs(result.items);
        setTotal(result.total);
      } catch {
        setJobs([]);
        setTotal(0);
      } finally {
        setLoading(false);
      }
    }
    void load();
  }, [activeTab]);

  async function rerun(jobId: string) {
    setActionId(jobId);
    try {
      await api.post(`/api/v1/admin/jobs/${jobId}/rerun`, {});
      setJobs((prev) =>
        prev.map((j) => (j.id === jobId ? { ...j, state: "Scheduled" } : j)),
      );
    } finally {
      setActionId(null);
    }
  }

  async function cancel(jobId: string) {
    setActionId(jobId);
    try {
      await api.post(`/api/v1/admin/jobs/${jobId}/cancel`, {});
      setJobs((prev) => prev.filter((j) => j.id !== jobId));
    } finally {
      setActionId(null);
    }
  }

  return (
    <div>
      <div className="mb-6">
        <h1 className="text-2xl font-bold">Background Jobs</h1>
        <p className="text-sm text-muted-foreground">
          Monitor and manage the job queue
        </p>
      </div>

      <Tabs value={activeTab} onValueChange={(v) => setActiveTab(v as JobState)}>
        <TabsList className="mb-4">
          {STATE_TABS.map((s) => (
            <TabsTrigger key={s} value={s}>
              {s}
            </TabsTrigger>
          ))}
        </TabsList>

        {STATE_TABS.map((s) => (
          <TabsContent key={s} value={s}>
            {loading ? (
              <p className="py-8 text-center text-muted-foreground">Loading...</p>
            ) : jobs.length === 0 ? (
              <div className="rounded-lg border border-dashed py-16 text-center">
                <p className="text-muted-foreground">
                  No {s.toLowerCase()} jobs — backend may not be connected yet.
                </p>
              </div>
            ) : (
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Job</TableHead>
                    <TableHead>Queue</TableHead>
                    <TableHead>State</TableHead>
                    <TableHead>Scheduled</TableHead>
                    <TableHead>Duration</TableHead>
                    <TableHead></TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {jobs.map((job) => {
                    const duration =
                      job.startedAt && job.finishedAt
                        ? `${Math.round((new Date(job.finishedAt).getTime() - new Date(job.startedAt).getTime()) / 1000)}s`
                        : job.startedAt
                          ? "running"
                          : "—";

                    return (
                      <TableRow key={job.id}>
                        <TableCell>
                          <div>
                            <p className="font-medium">{job.name}</p>
                            {job.errorMessage && (
                              <p className="text-xs text-destructive truncate max-w-[280px]">
                                {job.errorMessage}
                              </p>
                            )}
                          </div>
                        </TableCell>
                        <TableCell className="text-muted-foreground">
                          {job.queue}
                        </TableCell>
                        <TableCell>
                          <Badge variant={stateBadgeVariant(job.state)}>
                            {job.state}
                          </Badge>
                        </TableCell>
                        <TableCell className="text-muted-foreground">
                          {new Date(job.scheduledAt).toLocaleString()}
                        </TableCell>
                        <TableCell className="text-muted-foreground">
                          {duration}
                        </TableCell>
                        <TableCell>
                          <div className="flex gap-2">
                            {(job.state === "Failed" || job.state === "Succeeded") && (
                              <Button
                                size="sm"
                                variant="outline"
                                onClick={() => rerun(job.id)}
                                disabled={actionId === job.id}
                              >
                                Re-run
                              </Button>
                            )}
                            {(job.state === "Scheduled" || job.state === "Running") && (
                              <Button
                                size="sm"
                                variant="ghost"
                                onClick={() => cancel(job.id)}
                                disabled={actionId === job.id}
                              >
                                Cancel
                              </Button>
                            )}
                          </div>
                        </TableCell>
                      </TableRow>
                    );
                  })}
                </TableBody>
              </Table>
            )}
          </TabsContent>
        ))}
      </Tabs>
    </div>
  );
}
