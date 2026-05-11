"use client";

import * as React from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card";
import {
  Form,
  FormControl,
  FormDescription,
  FormField,
  FormItem,
  FormLabel,
  FormMessage,
} from "@/components/ui/form";
import { Skeleton } from "@/components/ui/skeleton";
import { apiGet, apiPut } from "@/lib/api";
import { toast } from "@/components/ui/toast";

const schema = z.object({
  name: z.string().min(1, "Name is required"),
  primaryColor: z
    .string()
    .regex(/^#[0-9a-fA-F]{6}$/, "Enter a valid hex color (e.g. #6366f1)"),
  logoUrl: z.string().url("Enter a valid URL").or(z.literal("")).optional(),
});

type FormData = z.infer<typeof schema>;

interface BrandingResponse {
  name: string;
  primaryColor: string;
  logoUrl?: string;
}

export default function BrandingSettingsPage(): React.JSX.Element {
  const [loading, setLoading] = React.useState(true);
  const [preview, setPreview] = React.useState("#6366f1");

  const form = useForm<FormData>({
    resolver: zodResolver(schema),
    defaultValues: { name: "", primaryColor: "#6366f1", logoUrl: "" },
  });

  const primaryColor = form.watch("primaryColor");
  React.useEffect(() => {
    if (/^#[0-9a-fA-F]{6}$/.test(primaryColor)) {
      setPreview(primaryColor);
    }
  }, [primaryColor]);

  React.useEffect(() => {
    apiGet<BrandingResponse>("/api/v1/branding")
      .then((b) => {
        form.reset({
          name: b.name,
          primaryColor: b.primaryColor,
          logoUrl: b.logoUrl ?? "",
        });
        setPreview(b.primaryColor);
      })
      .catch(() =>
        toast({ variant: "destructive", title: "Failed to load branding" }),
      )
      .finally(() => setLoading(false));
  }, [form]);

  async function onSubmit(data: FormData): Promise<void> {
    await apiPut("/api/v1/branding", data);
    toast({ title: "Branding updated" });
    // Force layout refresh to pick up new CSS vars
    document.documentElement.style.setProperty("--brand-primary", data.primaryColor);
  }

  return (
    <div className="max-w-lg space-y-6">
      {/* Live preview swatch */}
      <div
        className="h-2 w-full rounded-full"
        style={{ background: preview }}
        aria-hidden
      />

      <Card>
        <CardHeader>
          <CardTitle>Branding</CardTitle>
          <CardDescription>
            Customize your organization&apos;s appearance.
          </CardDescription>
        </CardHeader>
        <CardContent>
          {loading ? (
            <div className="space-y-3">
              <Skeleton className="h-10 w-full" />
              <Skeleton className="h-10 w-full" />
            </div>
          ) : (
            <Form {...form}>
              <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-4">
                <FormField
                  control={form.control}
                  name="name"
                  render={({ field }) => (
                    <FormItem>
                      <FormLabel>Organization display name</FormLabel>
                      <FormControl>
                        <Input {...field} placeholder="Acme Corp" />
                      </FormControl>
                      <FormMessage />
                    </FormItem>
                  )}
                />
                <FormField
                  control={form.control}
                  name="primaryColor"
                  render={({ field }) => (
                    <FormItem>
                      <FormLabel>Brand color</FormLabel>
                      <FormControl>
                        <div className="flex gap-2">
                          <input
                            type="color"
                            value={field.value}
                            onChange={(e) => field.onChange(e.target.value)}
                            className="h-10 w-10 cursor-pointer rounded-md border"
                            aria-label="Pick a color"
                          />
                          <Input {...field} placeholder="#6366f1" />
                        </div>
                      </FormControl>
                      <FormDescription>Hex color code</FormDescription>
                      <FormMessage />
                    </FormItem>
                  )}
                />
                <FormField
                  control={form.control}
                  name="logoUrl"
                  render={({ field }) => (
                    <FormItem>
                      <FormLabel>Logo URL</FormLabel>
                      <FormControl>
                        <Input
                          {...field}
                          type="url"
                          placeholder="https://example.com/logo.png"
                        />
                      </FormControl>
                      <FormDescription>
                        Public URL to your logo image (PNG or SVG recommended)
                      </FormDescription>
                      <FormMessage />
                    </FormItem>
                  )}
                />
                <Button type="submit" disabled={form.formState.isSubmitting}>
                  {form.formState.isSubmitting ? "Saving..." : "Save branding"}
                </Button>
              </form>
            </Form>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
