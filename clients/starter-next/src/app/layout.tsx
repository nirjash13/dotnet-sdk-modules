import type { Metadata } from "next";
import { Inter } from "next/font/google";
import "./globals.css";
import { ThemeProvider } from "@/components/theme-provider";
import { Toaster } from "@/components/ui/toast";

const inter = Inter({ subsets: ["latin"], variable: "--font-sans" });

export const metadata: Metadata = {
  title: {
    default: "SaasBuilder App",
    template: "%s | SaasBuilder",
  },
  description: "SaasBuilder starter application",
};

interface BrandingResponse {
  primaryColor?: string;
  name?: string;
}

async function getBranding(): Promise<BrandingResponse> {
  const apiBase = process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5000";
  try {
    const res = await fetch(`${apiBase}/api/v1/branding`, {
      next: { revalidate: 300 },
    });
    if (!res.ok) return {};
    return (await res.json()) as BrandingResponse;
  } catch {
    return {};
  }
}

export default async function RootLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  const branding = await getBranding();

  const brandingVars =
    branding.primaryColor
      ? ({
          "--brand-primary": branding.primaryColor,
        } as React.CSSProperties)
      : undefined;

  return (
    <html lang="en" suppressHydrationWarning className={inter.variable}>
      <body style={brandingVars}>
        <ThemeProvider attribute="class" defaultTheme="system" enableSystem>
          {children}
          <Toaster />
        </ThemeProvider>
      </body>
    </html>
  );
}
