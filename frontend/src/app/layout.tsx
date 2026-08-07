import type { Metadata } from "next";
import type { ReactNode } from "react";
import "./globals.css";
import { ToastContainer } from "@/components/common/ToastContainer";

export const metadata: Metadata = {
  title: "Neoantigen Pipeline",
  description:
    "Design a personalized mRNA neoantigen vaccine from tumor and normal sequencing data.",
};

export default function RootLayout({ children }: { children: ReactNode }) {
  return (
    <html lang="en" className="h-full">
      <body className="flex h-full min-h-screen flex-col bg-paper font-sans text-ink antialiased">
        {children}
        <ToastContainer />
      </body>
    </html>
  );
}
