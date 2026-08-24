import { useEffect, useMemo, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useParams } from "react-router-dom";
import {
  screenshotCodeApi,
  screenshotCodeKeys,
  type ScreenshotCodeFile,
  type ScreenshotGeneration,
} from "../features/screenshot-code/api";
import { useToast } from "../contexts/ToastContext";
import "./ScreenshotToCodePage.css";
function Diff({ file }: { file: ScreenshotCodeFile }) {
  const before = (file.existingContent ?? "").split("\n"),
    after = file.generatedContent.split("\n");
  return (
    <pre className="vision-diff">
      {before.length > 1 || before[0] ? (
        before.map((l, i) => (
          <span className="removed" key={`b${i}`}>
            - {l}
            {"\n"}
          </span>
        ))
      ) : (
        <span className="muted">New file{"\n"}</span>
      )}
      {after.map((l, i) => (
        <span className="added" key={`a${i}`}>
          + {l}
          {"\n"}
        </span>
      ))}
    </pre>
  );
}
function Draft({
  value,
  applying,
  apply,
}: {
  value?: ScreenshotGeneration;
  applying: boolean;
  apply: () => void;
}) {
  const [index, setIndex] = useState(0);
  const [view, setView] = useState<"preview" | "code" | "diff">("preview");
  useEffect(() => setIndex(0), [value?.id]);
  if (!value)
    return (
      <section className="vision-empty">
        <b>Upload a design reference</b>
        <p>Analysis, React code, isolated preview and diff will appear here.</p>
      </section>
    );
  const file = value.files[index];
  return (
    <section className="vision-result">
      <header>
        <div>
          <span>{value.status}</span>
          <h2>{value.imageFileName}</h2>
          <small>
            {value.modelProvider} · {value.modelName} ·{" "}
            {new Date(value.generatedAt).toLocaleString()}
          </small>
        </div>
        {value.status === "Draft" && (
          <button
            className="ui-button primary"
            disabled={applying}
            onClick={apply}
          >
            {applying ? "Applying…" : "Approve & apply"}
          </button>
        )}
      </header>
      <div className="vision-analysis">
        <b>Visual analysis</b>
        <p>{value.analysis}</p>
      </div>
      <nav>
        {(["preview", "code", "diff"] as const).map((x) => (
          <button
            className={view === x ? "active" : ""}
            onClick={() => setView(x)}
            key={x}
          >
            {x}
          </button>
        ))}
      </nav>
      {view !== "preview" && (
        <div className="vision-files">
          {value.files.map((x, i) => (
            <button
              className={i === index ? "active" : ""}
              onClick={() => setIndex(i)}
              key={x.path}
            >
              {x.path}
              {x.existingNodeId ? " · overwrite" : " · new"}
            </button>
          ))}
        </div>
      )}
      {view === "preview" ? (
        <iframe
          title="Generated design preview"
          sandbox="allow-scripts"
          srcDoc={value.previewHtml}
        />
      ) : view === "code" ? (
        <pre className="vision-code">
          <code>{file.generatedContent}</code>
        </pre>
      ) : (
        <Diff file={file} />
      )}
      <footer>
        Image SHA-256: <code>{value.imageHash.slice(0, 16)}…</code> · Files
        change only after approval.
      </footer>
    </section>
  );
}
export function ScreenshotToCodePage() {
  const { projectId = "" } = useParams(),
    client = useQueryClient(),
    { show } = useToast();
  const [prompt, setPrompt] = useState(
      "Recreate this interface as an accessible responsive React page while preserving the visible visual hierarchy.",
    ),
    [image, setImage] = useState<File>(),
    [selected, setSelected] = useState<string>();
  const list = useQuery({
    queryKey: screenshotCodeKeys.list(projectId),
    queryFn: () => screenshotCodeApi.list(projectId),
  });
  useEffect(() => {
    if (!selected && list.data?.[0]) setSelected(list.data[0].id);
  }, [selected, list.data]);
  const detail = useQuery({
    queryKey: screenshotCodeKeys.detail(projectId, selected ?? ""),
    queryFn: () => screenshotCodeApi.get(projectId, selected!),
    enabled: Boolean(selected),
  });
  const sync = (v: ScreenshotGeneration) => {
    setSelected(v.id);
    client.setQueryData(screenshotCodeKeys.detail(projectId, v.id), v);
    void client.invalidateQueries({
      queryKey: screenshotCodeKeys.list(projectId),
    });
  };
  const generate = useMutation({
    mutationFn: () => screenshotCodeApi.generate(projectId, prompt, image!),
    onSuccess: (v) => {
      sync(v);
      show("Screenshot draft generated. Review preview and diff.");
    },
    onError: (e) => show(e.message, "error"),
  });
  const apply = useMutation({
    mutationFn: () => screenshotCodeApi.apply(projectId, selected!),
    onSuccess: (v) => {
      sync(v);
      show("Approved files were written with version history.");
    },
    onError: (e) => show(e.message, "error"),
  });
  const url = useMemo(
    () => (image ? URL.createObjectURL(image) : undefined),
    [image],
  );
  useEffect(
    () => () => {
      if (url) URL.revokeObjectURL(url);
    },
    [url],
  );
  return (
    <main className="vision-page">
      <header className="vision-hero">
        <div>
          <span>VISION → REVIEWABLE CODE</span>
          <h1>Screenshot to Code</h1>
          <p>
            Ollama produces React, TypeScript, CSS, a sandboxed preview, and an
            explicit diff.
          </p>
        </div>
        <b>{list.data?.length ?? 0} drafts</b>
      </header>
      <section className="vision-compose">
        <label className="vision-upload">
          {url ? (
            <img src={url} alt="Selected design reference" />
          ) : (
            <span>
              Choose PNG, JPEG, or WebP
              <br />
              <small>Maximum 5 MB</small>
            </span>
          )}
          <input
            type="file"
            accept="image/png,image/jpeg,image/webp"
            onChange={(e) => setImage(e.target.files?.[0])}
          />
        </label>
        <div>
          <label>
            Implementation intent
            <textarea
              value={prompt}
              maxLength={2000}
              onChange={(e) => setPrompt(e.target.value)}
            />
          </label>
          <button
            className="ui-button primary"
            disabled={!image || prompt.trim().length < 10 || generate.isPending}
            onClick={() => generate.mutate()}
          >
            {generate.isPending
              ? "Ollama vision is generating…"
              : "Analyze & generate draft"}
          </button>
          <small>
            No image binary is persisted; only its hash and the draft are
            stored.
          </small>
        </div>
      </section>
      <div className="vision-layout">
        <aside>
          {list.isLoading && <p role="status">Loading screenshot drafts…</p>}
          {list.isError && (
            <p role="alert">
              {list.error.message}{" "}
              <button onClick={() => void list.refetch()}>Retry</button>
            </p>
          )}
          {list.data?.map((x) => (
            <button
              key={x.id}
              className={selected === x.id ? "active" : ""}
              onClick={() => setSelected(x.id)}
            >
              <b>{x.imageFileName}</b>
              <span>
                {x.status} · {new Date(x.generatedAt).toLocaleDateString()}
              </span>
            </button>
          ))}
          {!list.isLoading && !list.data?.length && (
            <p>No screenshot drafts yet.</p>
          )}
        </aside>
        {detail.isLoading ? (
          <section className="vision-empty" role="status">
            Loading draft…
          </section>
        ) : detail.isError ? (
          <section className="vision-empty" role="alert">
            <b>Draft unavailable</b>
            <p>{detail.error.message}</p>
            <button onClick={() => void detail.refetch()}>Retry</button>
          </section>
        ) : (
          <Draft
            value={detail.data}
            applying={apply.isPending}
            apply={() => {
              if (
                confirm(
                  "Apply App.tsx and styles.css? Current versions will be verified first.",
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
