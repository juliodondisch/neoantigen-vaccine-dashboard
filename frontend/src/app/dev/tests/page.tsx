import { notFound } from "next/navigation";
import { ENABLE_DEV_TOOLS } from "@/lib/constants/config";
import { TopBar } from "@/components/layout/TopBar";
import { TestHarness } from "@/components/dev/TestHarness";

export default function DevTestsPage() {
  if (!ENABLE_DEV_TOOLS) {
    notFound();
  }

  return (
    <>
      <TopBar showBackLink />
      <main className="mx-auto w-full max-w-[1400px] flex-1 px-8 py-8">
        <TestHarness />
      </main>
    </>
  );
}
