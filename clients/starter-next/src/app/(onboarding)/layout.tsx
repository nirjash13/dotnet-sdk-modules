import * as React from "react";

export default function OnboardingLayout({
  children,
}: {
  children: React.ReactNode;
}): React.JSX.Element {
  return (
    <div className="flex min-h-screen flex-col bg-background">
      <header className="border-b px-6 py-4">
        <span className="text-sm font-semibold tracking-tight">SaasBuilder</span>
      </header>
      <main className="flex flex-1 items-start justify-center px-4 py-12">
        <div className="w-full max-w-2xl">{children}</div>
      </main>
    </div>
  );
}
