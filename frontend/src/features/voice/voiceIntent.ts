export type VoiceIntent =
  | { kind: "openFile"; fileName: string; risky: false; summary: string }
  | { kind: "explain"; risky: false; summary: string }
  | { kind: "runTests"; risky: true; summary: string }
  | { kind: "createBranch"; branchName: string; risky: true; summary: string }
  | { kind: "fixError"; risky: false; summary: string }
  | { kind: "unknown"; risky: false; summary: string };

export function parseVoiceIntent(raw: string): VoiceIntent {
  const command = raw.trim();
  const open = command.match(/^open\s+(.+?)[.!]?$/i);
  if (open?.[1]) return { kind: "openFile", fileName: open[1].trim(), risky: false, summary: `Open ${open[1].trim()}` };
  if (/^explain\s+(this|the)\s+(function|file|code)[.!]?$/i.test(command) || /^explain[.!]?$/i.test(command)) return { kind: "explain", risky: false, summary: "Explain the current selection or file with AI" };
  if (/^run\s+(the\s+)?tests?[.!]?$/i.test(command)) return { kind: "runTests", risky: true, summary: "Start a bounded autonomous test run for the current file" };
  const branch = command.match(/^create\s+(a\s+)?branch\s+(called|named)\s+([a-z0-9._\/-]+)[.!]?$/i);
  if (branch?.[3]) { const branchName = branch[3].replace(/[.!]+$/, ""); return { kind: "createBranch", branchName, risky: true, summary: `Create branch ${branchName}` }; }
  if (/^(ask\s+ai\s+to\s+)?fix\s+(this|the)\s+error[.!]?$/i.test(command)) return { kind: "fixError", risky: false, summary: "Ask AI for a fix suggestion without applying it" };
  return { kind: "unknown", risky: false, summary: "Command not recognized" };
}
