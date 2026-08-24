import { describe, expect, it } from "vitest";
import { createPreviewDocument } from "./LivePreviewPanel";
import type { EditorTab } from "./editorStore";

function tab(input: Partial<EditorTab> & Pick<EditorTab, "id" | "name" | "path" | "language" | "content">): EditorTab {
  return {
    savedContent: input.content,
    concurrencyToken: "token",
    status: "Saved",
    requestVersion: 0,
    acknowledgedVersion: 0,
    suppressAutoSave: false,
    cursor: { lineNumber: 1, column: 1 },
    ...input,
  };
}

describe("createPreviewDocument", () => {
  it("injects open CSS and JavaScript files into an HTML preview", () => {
    const html = tab({ id: "1", name: "index.html", path: "/index.html", language: "html", content: "<html><head></head><body><h1>Hello</h1></body></html>" });
    const css = tab({ id: "2", name: "site.css", path: "/site.css", language: "css", content: "h1 { color: tomato; }" });
    const js = tab({ id: "3", name: "app.js", path: "/app.js", language: "javascript", content: "console.log('running')" });
    const result = createPreviewDocument(html, [html, css, js]);

    expect(result).toContain("h1 { color: tomato; }");
    expect(result).toContain("console.log('running')");
    expect(result).toContain("nexacode-preview");
  });

  it("escapes closing script tags from user code", () => {
    const js = tab({ id: "1", name: "app.js", path: "/app.js", language: "javascript", content: "console.log('</script>')" });
    expect(createPreviewDocument(js, [js])).not.toContain("console.log('</script>')");
  });
});
