import { useEffect, useMemo, useRef, useState } from "react";
import type { EditorTab } from "./editorStore";
import { executionApi } from "./executionApi";

type PreviewMessage = { source: "nexacode-preview"; level: string; values: string[] };

const browserRunnable = new Set(["html", "css", "javascript"]);

function escapeScript(value: string) {
  return value.replace(/<\/script/gi, "<\\/script");
}

function previewBridge() {
  return `<script>
(() => {
  const send = (level, values) => parent.postMessage({ source: "nexacode-preview", level, values: values.map(value => {
    try { return typeof value === "string" ? value : JSON.stringify(value); }
    catch { return String(value); }
  }) }, "*");
  for (const level of ["log", "info", "warn", "error"]) {
    const original = console[level].bind(console);
    console[level] = (...values) => { original(...values); send(level, values); };
  }
  window.addEventListener("error", event => send("error", [event.message + " (" + event.lineno + ":" + event.colno + ")"]));
  window.addEventListener("unhandledrejection", event => send("error", ["Unhandled promise rejection: " + String(event.reason)]));
  send("ready", ["Preview ready"]);
})();
</script>`;
}

export function createPreviewDocument(active: EditorTab, tabs: EditorTab[]) {
  const css = tabs.filter(tab => tab.language === "css").map(tab => `/* ${tab.path} */\n${tab.content}`).join("\n");
  const javascript = tabs.filter(tab => tab.language === "javascript" && tab.id !== active.id).map(tab => `// ${tab.path}\n${tab.content}`).join("\n");
  const bridge = previewBridge();
  const scripts = javascript ? `<script>${escapeScript(javascript)}<\/script>` : "";

  if (active.language === "html") {
    let html = active.content;
    const additions = `${bridge}<style>${css}</style>`;
    html = /<head[\s>]/i.test(html) ? html.replace(/<\/head>/i, `${additions}</head>`) : `${additions}${html}`;
    return /<\/body>/i.test(html) ? html.replace(/<\/body>/i, `${scripts}</body>`) : `${html}${scripts}`;
  }

  if (active.language === "css") {
    return `<!doctype html><html><head><meta charset="utf-8">${bridge}<style>${active.content}\n${css}</style></head><body><main class="preview-demo"><h1>Live CSS Preview</h1><p>Edit styles to see changes instantly.</p><button>Example button</button><section><strong>Sample card</strong><span>Use element, class, or ID selectors.</span></section></main>${scripts}</body></html>`;
  }

  return `<!doctype html><html><head><meta charset="utf-8">${bridge}<style>body{font:14px system-ui;padding:24px;color:#182238}code{background:#f1f3f7;padding:3px 6px;border-radius:4px}</style></head><body><h1>JavaScript Preview</h1><p>Open the Console below to inspect output.</p><script>${escapeScript(active.content)}<\/script></body></html>`;
}

export function LivePreviewPanel({ projectId, activeTab, tabs, disabled = false }: { projectId: string; activeTab?: EditorTab; tabs: EditorTab[]; disabled?: boolean }) {
  const [revision, setRevision] = useState(0);
  const [autoRun, setAutoRun] = useState(true);
  const [consoleLines, setConsoleLines] = useState<{ level: string; text: string }[]>([]);
  const [running, setRunning] = useState(false);
  const iframe = useRef<HTMLIFrameElement>(null);
  const browserSupported = Boolean(activeTab && browserRunnable.has(activeTab.language));
  const supported = browserSupported || activeTab?.language === "csharp";
  const document = useMemo(() => activeTab && browserSupported ? createPreviewDocument(activeTab, tabs) : "", [activeTab, revision, browserSupported, tabs]);

  useEffect(() => {
    const receive = (event: MessageEvent<PreviewMessage>) => {
      if (event.source !== iframe.current?.contentWindow || event.data?.source !== "nexacode-preview") return;
      setConsoleLines(lines => [...lines, { level: event.data.level, text: event.data.values.join(" ") }].slice(-100));
    };
    window.addEventListener("message", receive);
    return () => window.removeEventListener("message", receive);
  }, []);

  useEffect(() => {
    if (disabled || !autoRun || !browserSupported) return;
    const timer = window.setTimeout(() => {
      setConsoleLines([]);
      setRevision(value => value + 1);
    }, 450);
    return () => window.clearTimeout(timer);
  }, [activeTab?.content, autoRun, browserSupported, disabled]);

  const run = async () => {
    if (!supported) return;
    setConsoleLines([]);
    if (activeTab?.language !== "csharp") {
      setRevision(value => value + 1);
      return;
    }
    setRunning(true);
    try {
      const result = await executionApi.runCSharp(projectId, activeTab.content, activeTab.id);
      const lines = [
        ...result.stdout.split("\n").filter(Boolean).map(text => ({ level: "log", text })),
        ...result.stderr.split("\n").filter(Boolean).map(text => ({ level: "error", text })),
        { level: result.exitCode === 0 ? "ready" : "error", text: result.timedOut ? "Timed out" : `Process exited with code ${result.exitCode} in ${result.durationMs} ms` },
      ];
      setConsoleLines(lines);
    } catch (error) {
      setConsoleLines([{ level: "error", text: error instanceof Error ? error.message : "Execution failed." }]);
    } finally {
      setRunning(false);
    }
  };

  return <section className="live-preview-panel">
    <header>
      <div><span className="preview-status" /> <strong>Live Preview</strong><small>{activeTab?.name ?? "No file selected"}</small></div>
      <nav>
        <label><input type="checkbox" checked={autoRun} disabled={disabled || !browserSupported} onChange={event => setAutoRun(event.target.checked)} /> Auto</label>
        <button onClick={() => void run()} disabled={disabled || !supported || running}>{running ? "Running…" : "▶ Run"}</button>
        <button onClick={() => iframe.current?.contentWindow?.location.reload()} disabled={!browserSupported} title="Reload preview">↻</button>
      </nav>
    </header>
    {!activeTab ? <div className="preview-empty"><b>Open a file to run it</b><span>HTML, CSS, and JavaScript are supported.</span></div>
      : !supported ? <div className="preview-empty"><b>{activeTab.language} requires an isolated execution worker</b><span>Browser preview runs HTML, CSS, and JavaScript. The local runner currently supports C#.</span></div>
      : browserSupported ? <iframe ref={iframe} title={`Preview of ${activeTab.name}`} sandbox="allow-scripts allow-modals" srcDoc={document} />
      : <div className="preview-empty"><b>{disabled ? "Viewer access is read-only" : "C# console runner ready"}</b><span>{disabled ? "This role may inspect files but cannot execute project code." : "Press Run to compile the current file. Output and compiler errors appear below."}</span></div>}
    <div className="preview-console">
      <header><strong>CONSOLE</strong><button onClick={() => setConsoleLines([])}>Clear</button></header>
      <div>{consoleLines.length ? consoleLines.map((line, index) => <code className={line.level} key={`${index}-${line.text}`}>{line.level === "error" ? "×" : line.level === "warn" ? "!" : "›"} {line.text}</code>) : <span>Console output will appear here.</span>}</div>
    </div>
  </section>;
}
