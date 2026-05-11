import type { Metadata } from "next";
import "./globals.css";

export const metadata: Metadata = {
  title: "SaasBuilder Admin",
  description: "Platform operator console for SaasBuilder SDK",
};

export default function RootLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  return (
    <html lang="en">
      <body className="antialiased">{children}</body>
    </html>
  );
}
