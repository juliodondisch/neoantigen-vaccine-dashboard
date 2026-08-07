import { formatBytes } from "@/lib/utils/format";
import { Button } from "@/components/common/Button";
import type { DiskStatus, ToolStatus } from "@/types/api";

interface ToolStatusPanelProps {
  statuses: ToolStatus[];
  diskStatus: DiskStatus | null;
  onRefresh: () => void;
}

export function ToolStatusPanel({ statuses, diskStatus, onRefresh }: ToolStatusPanelProps) {
  return (
    <div className="flex flex-col gap-4 rounded-md border border-rule bg-surface p-6">
      <div className="flex items-center justify-between">
        <h3 className="text-h2 text-ink">Tool availability</h3>
        <Button variant="secondary" size="sm" onClick={onRefresh}>
          Refresh
        </Button>
      </div>

      {diskStatus && (
        <p className="text-ui text-slate">
          Disk: <span className="font-mono text-ink">{formatBytes(diskStatus.availableBytes)}</span> free,{" "}
          <span className="font-mono text-ink">{formatBytes(diskStatus.dataUsedBytes)}</span> used by patient data
        </p>
      )}

      {statuses.length === 0 ? (
        <p className="text-body text-slate">No tool status yet.</p>
      ) : (
        <table className="w-full text-ui">
          <thead>
            <tr className="text-left text-micro text-slate">
              <th className="py-2">Tool</th>
              <th className="py-2">Status</th>
              <th className="py-2">Version</th>
              <th className="py-2">Used by</th>
            </tr>
          </thead>
          <tbody>
            {statuses.map((tool) => (
              <tr key={tool.toolName} className="border-t border-rule">
                <td className="py-2 font-mono">{tool.toolName}</td>
                <td className="py-2">
                  <span className={tool.isAvailable ? "text-state-complete" : "text-state-skipped"}>
                    {tool.isAvailable ? "Available" : "Missing"}
                  </span>
                </td>
                <td className="py-2 font-mono text-slate">{tool.version ?? "—"}</td>
                <td className="py-2 text-small text-slate">{tool.usedBySteps.join(", ")}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </div>
  );
}
