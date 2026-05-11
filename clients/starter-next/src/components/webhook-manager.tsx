"use client";

import * as React from "react";
import { Plus, Trash2, RefreshCw } from "lucide-react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Badge } from "@/components/ui/badge";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from "@/components/ui/dialog";
import {
  Form,
  FormControl,
  FormField,
  FormItem,
  FormLabel,
  FormMessage,
} from "@/components/ui/form";
import { Skeleton } from "@/components/ui/skeleton";
import { apiGet, apiPost, apiDelete } from "@/lib/api";
import { toast } from "@/components/ui/toast";

const AVAILABLE_EVENTS = [
  "member.invited",
  "member.joined",
  "member.removed",
  "billing.subscription.updated",
  "billing.invoice.paid",
  "billing.invoice.failed",
  "file.uploaded",
  "organization.updated",
];

interface WebhookEndpoint {
  id: string;
  url: string;
  events: string[];
  isEnabled: boolean;
  createdAt: string;
}

const addSchema = z.object({
  url: z.string().url("Enter a valid HTTPS URL"),
  events: z.array(z.string()).min(1, "Select at least one event"),
});

type AddFormData = z.infer<typeof addSchema>;

interface WebhookListResponse {
  items: WebhookEndpoint[];
}

export function WebhookManager(): React.JSX.Element {
  const [endpoints, setEndpoints] = React.useState<WebhookEndpoint[]>([]);
  const [loading, setLoading] = React.useState(true);
  const [dialogOpen, setDialogOpen] = React.useState(false);

  const form = useForm<AddFormData>({
    resolver: zodResolver(addSchema),
    defaultValues: { url: "", events: [] },
  });

  React.useEffect(() => {
    loadEndpoints();
  }, []);

  function loadEndpoints(): void {
    setLoading(true);
    apiGet<WebhookListResponse>("/api/v1/webhooks")
      .then((data) => setEndpoints(data.items))
      .catch(() => setEndpoints([]))
      .finally(() => setLoading(false));
  }

  async function addEndpoint(data: AddFormData): Promise<void> {
    const created = await apiPost<WebhookEndpoint>("/api/v1/webhooks", data);
    setEndpoints((prev) => [...prev, created]);
    toast({ title: "Webhook added", description: data.url });
    form.reset();
    setDialogOpen(false);
  }

  async function deleteEndpoint(id: string): Promise<void> {
    await apiDelete(`/api/v1/webhooks/${id}`);
    setEndpoints((prev) => prev.filter((e) => e.id !== id));
    toast({ title: "Webhook removed" });
  }

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h2 className="text-lg font-semibold">Webhook Endpoints</h2>
        <div className="flex gap-2">
          <Button variant="outline" size="sm" onClick={loadEndpoints}>
            <RefreshCw className="mr-1 h-4 w-4" />
            Refresh
          </Button>
          <Dialog open={dialogOpen} onOpenChange={setDialogOpen}>
            <DialogTrigger asChild>
              <Button size="sm">
                <Plus className="mr-1 h-4 w-4" />
                Add endpoint
              </Button>
            </DialogTrigger>
            <DialogContent>
              <DialogHeader>
                <DialogTitle>Add webhook endpoint</DialogTitle>
              </DialogHeader>
              <Form {...form}>
                <form
                  onSubmit={form.handleSubmit(addEndpoint)}
                  className="space-y-4"
                >
                  <FormField
                    control={form.control}
                    name="url"
                    render={({ field }) => (
                      <FormItem>
                        <FormLabel>Endpoint URL</FormLabel>
                        <FormControl>
                          <Input
                            {...field}
                            type="url"
                            placeholder="https://example.com/webhook"
                          />
                        </FormControl>
                        <FormMessage />
                      </FormItem>
                    )}
                  />
                  <FormField
                    control={form.control}
                    name="events"
                    render={({ field }) => (
                      <FormItem>
                        <FormLabel>Events to subscribe</FormLabel>
                        <FormControl>
                          <div className="grid grid-cols-2 gap-2">
                            {AVAILABLE_EVENTS.map((event) => (
                              <label
                                key={event}
                                className="flex cursor-pointer items-center gap-2 text-sm"
                              >
                                <input
                                  type="checkbox"
                                  checked={field.value.includes(event)}
                                  onChange={(e) => {
                                    const next = e.target.checked
                                      ? [...field.value, event]
                                      : field.value.filter((v) => v !== event);
                                    field.onChange(next);
                                  }}
                                  className="rounded"
                                />
                                <code className="text-xs">{event}</code>
                              </label>
                            ))}
                          </div>
                        </FormControl>
                        <FormMessage />
                      </FormItem>
                    )}
                  />
                  <div className="flex justify-end gap-2">
                    <Button
                      type="button"
                      variant="outline"
                      onClick={() => setDialogOpen(false)}
                    >
                      Cancel
                    </Button>
                    <Button
                      type="submit"
                      disabled={form.formState.isSubmitting}
                    >
                      {form.formState.isSubmitting ? "Saving..." : "Save"}
                    </Button>
                  </div>
                </form>
              </Form>
            </DialogContent>
          </Dialog>
        </div>
      </div>

      {loading ? (
        <div className="space-y-2">
          {Array.from({ length: 3 }).map((_, i) => (
            <Skeleton key={i} className="h-12 w-full" />
          ))}
        </div>
      ) : endpoints.length === 0 ? (
        <div className="rounded-lg border border-dashed p-8 text-center text-muted-foreground">
          No webhook endpoints configured.
        </div>
      ) : (
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>URL</TableHead>
              <TableHead>Events</TableHead>
              <TableHead>Status</TableHead>
              <TableHead className="w-10" />
            </TableRow>
          </TableHeader>
          <TableBody>
            {endpoints.map((ep) => (
              <TableRow key={ep.id}>
                <TableCell className="max-w-xs truncate font-mono text-sm">
                  {ep.url}
                </TableCell>
                <TableCell>
                  <div className="flex flex-wrap gap-1">
                    {ep.events.slice(0, 3).map((e) => (
                      <Badge key={e} variant="secondary" className="text-xs">
                        {e}
                      </Badge>
                    ))}
                    {ep.events.length > 3 && (
                      <Badge variant="outline" className="text-xs">
                        +{ep.events.length - 3}
                      </Badge>
                    )}
                  </div>
                </TableCell>
                <TableCell>
                  <Badge variant={ep.isEnabled ? "default" : "secondary"}>
                    {ep.isEnabled ? "Active" : "Disabled"}
                  </Badge>
                </TableCell>
                <TableCell>
                  <Button
                    variant="ghost"
                    size="icon"
                    aria-label={`Delete webhook ${ep.url}`}
                    className="text-destructive hover:text-destructive"
                    onClick={() => void deleteEndpoint(ep.id)}
                  >
                    <Trash2 className="h-4 w-4" />
                  </Button>
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      )}
    </div>
  );
}
