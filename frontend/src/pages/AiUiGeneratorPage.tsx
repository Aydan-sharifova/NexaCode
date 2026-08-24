import { useEffect, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useParams } from "react-router-dom";
import {
  uiGeneratorApi,
  uiGeneratorKeys,
  type UiFile,
  type UiGeneration,
} from "../features/ui-generator/api";
import { useToast } from "../contexts/ToastContext";
import "./AiUiGeneratorPage.css";
function Review({
  draft,
  busy,
  apply,
}: {
  draft?: UiGeneration;
  busy: boolean;
  apply: () => void;
}) {
  const [tab, setTab] = useState<"preview" | "code" | "diff">("preview"),
    [index, setIndex] = useState(0);
  useEffect(() => setIndex(0), [draft?.id]);
  if (!draft)
    return (
      <section className="uig-empty">
        <b>Describe an interface to generate</b>
        <p>
          A multi-file architecture, live preview and review diff will appear
          here.
        </p>
      </section>
    );
  const file = draft.files[index];
  return (
    <section className="uig-review">
      <header>
        <div>
          <span>
            {draft.status} ·{" "}
            {draft.includeSampleData
              ? "sample data approved"
              : "no invented sample data"}
          </span>
          <h2>{draft.prompt}</h2>
          <small>
            {draft.modelProvider} · {draft.modelName}
          </small>
        </div>
        {draft.status === "Draft" && (
          <button className="ui-button primary" disabled={busy} onClick={apply}>
            {busy ? "Applying…" : "Approve all files"}
          </button>
        )}
      </header>
      <article className="uig-analysis">
        <b>Architecture & visual rationale</b>
        <p>{draft.analysis}</p>
      </article>
      <nav>
        {(["preview", "code", "diff"] as const).map((x) => (
          <button
            className={tab === x ? "active" : ""}
            key={x}
            onClick={() => setTab(x)}
          >
            {x}
          </button>
        ))}
      </nav>
      {tab !== "preview" && (
        <div className="uig-files">
          {draft.files.map((x, i) => (
            <button
              className={i === index ? "active" : ""}
              onClick={() => setIndex(i)}
              key={x.path}
            >
              {x.path}
              <small>{x.existingNodeId ? "replace" : "new"}</small>
            </button>
          ))}
        </div>
      )}
      {tab === "preview" ? (
        <iframe
          title="Generated UI preview"
          sandbox="allow-scripts"
          srcDoc={draft.previewHtml}
        />
      ) : tab === "code" ? (
        <pre className="uig-code">{file.generatedContent}</pre>
      ) : (
        <pre className="uig-diff">
          {file.existingContent ? (
            <span className="old">
              {file.existingContent
                .split("\n")
                .map((x) => `- ${x}`)
                .join("\n")}
              {"\n"}
            </span>
          ) : (
            <span className="neutral">New file{"\n"}</span>
          )}
          <span className="new">
            {file.generatedContent
              .split("\n")
              .map((x) => `+ ${x}`)
              .join("\n")}
          </span>
        </pre>
      )}
      <footer>
        All target versions are rechecked before the atomic database write. Git
        is synchronized after commit.
      </footer>
    </section>
  );
}
export function AiUiGeneratorPage() {
  const { projectId = "" } = useParams(),
    client = useQueryClient(),
    { show } = useToast();
  const [prompt, setPrompt] = useState(
      "Create a premium SaaS dashboard with clear navigation, accessible empty states, and a responsive information hierarchy.",
    ),
    [sample, setSample] = useState(false),
    [selected, setSelected] = useState<string>();
  const list = useQuery({
    queryKey: uiGeneratorKeys.list(projectId),
    queryFn: () => uiGeneratorApi.list(projectId),
  });
  useEffect(() => {
    if (!selected && list.data?.[0]) setSelected(list.data[0].id);
  }, [selected, list.data]);
  const detail = useQuery({
    queryKey: uiGeneratorKeys.detail(projectId, selected ?? ""),
    queryFn: () => uiGeneratorApi.get(projectId, selected!),
    enabled: !!selected,
  });
  const sync = (x: UiGeneration) => {
    setSelected(x.id);
    client.setQueryData(uiGeneratorKeys.detail(projectId, x.id), x);
    void client.invalidateQueries({
      queryKey: uiGeneratorKeys.list(projectId),
    });
  };
  const generate = useMutation({
    mutationFn: () => uiGeneratorApi.generate(projectId, prompt, sample),
    onSuccess: (x) => {
      sync(x);
      show("Multi-file UI draft generated for review.");
    },
    onError: (e) => show(e.message, "error"),
  });
  const apply = useMutation({
    mutationFn: () => uiGeneratorApi.apply(projectId, selected!),
    onSuccess: (x) => {
      sync(x);
      show("Approved UI files were versioned and synchronized to Git.");
    },
    onError: (e) => show(e.message, "error"),
  });
  return (
    <main className="uig-page">
      <header className="uig-hero">
        <div>
          <span>TEXT → ARCHITECTURE → UI</span>
          <h1>AI UI Generator</h1>
          <p>
            Generate a component layer, page, routing, responsive visual system
            and isolated preview with local Ollama.
          </p>
        </div>
        <b>{list.data?.length ?? 0} drafts</b>
      </header>
      <section className="uig-compose">
        <textarea
          maxLength={2000}
          value={prompt}
          onChange={(e) => setPrompt(e.target.value)}
          placeholder="Create a premium SaaS dashboard…"
        />
        <label>
          <input
            type="checkbox"
            checked={sample}
            onChange={(e) => setSample(e.target.checked)}
          />
          <span>
            <b>Include non-production sample data</b>
            <small>
              Off by default. When off, Ollama must generate honest
              empty/loading-ready states.
            </small>
          </span>
        </label>
        <button
          className="ui-button primary"
          disabled={prompt.trim().length < 10 || generate.isPending}
          onClick={() => generate.mutate()}
        >
          {generate.isPending
            ? "Ollama is designing…"
            : "Generate review draft"}
        </button>
      </section>
      <div className="uig-layout">
        <aside>
          {list.isLoading && <p role="status">Loading UI drafts…</p>}
          {list.isError && (
            <p role="alert">
              {list.error.message}{" "}
              <button onClick={() => void list.refetch()}>Retry</button>
            </p>
          )}
          {list.data?.map((x) => (
            <button
              key={x.id}
              className={x.id === selected ? "active" : ""}
              onClick={() => setSelected(x.id)}
            >
              <b>{x.prompt}</b>
              <span>
                {x.status} · {new Date(x.generatedAt).toLocaleDateString()}
              </span>
            </button>
          ))}
          {!list.isLoading && !list.data?.length && <p>No UI drafts yet.</p>}
        </aside>
        {detail.isLoading ? (
          <section className="uig-empty" role="status">
            Loading draft…
          </section>
        ) : detail.isError ? (
          <section className="uig-empty" role="alert">
            <b>Draft unavailable</b>
            <p>{detail.error.message}</p>
            <button onClick={() => void detail.refetch()}>Retry</button>
          </section>
        ) : (
          <Review
            draft={detail.data}
            busy={apply.isPending}
            apply={() => {
              if (
                confirm(
                  "Apply all four generated UI files? Existing hashes and concurrency tokens will be verified.",
                )
              )
                apply.mutate();
            }}
          />
        )}
      </div>
    </main>
  );
}
