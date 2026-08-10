import type { Monaco } from "@monaco-editor/react";
import type { editor } from "monaco-editor";
import { useCallback, useEffect } from "react";
import { useEditorStore } from "../editorStore";

const models = new Map<string, editor.ITextModel>();
let configured = false;

export function getOrCreateModel(
  monaco: Monaco,
  id: string,
  path: string,
  content: string,
  language: string,
) {
  const existing = models.get(id);
  if (existing && !existing.isDisposed()) return existing;

  const uri = monaco.Uri.parse(`file://${path.startsWith("/") ? path : `/${path}`}`);
  const model =
    monaco.editor.getModel(uri) ??
    monaco.editor.createModel(content, language, uri);

  models.set(id, model);
  return model;
}

export function useMonacoConfiguration() {
  const openTabIds = useEditorStore((state) => state.openTabIds);

  useEffect(() => {
    for (const [id, model] of models) {
      if (!openTabIds.includes(id)) {
        model.dispose();
        models.delete(id);
      }
    }
  }, [openTabIds]);

  useEffect(
    () => () => {
      models.forEach((model) => model.dispose());
      models.clear();
    },
    [],
  );

  const configure = useCallback((monaco: Monaco) => {
    if (configured) return;

    monaco.editor.defineTheme("coding-dark", {
      base: "vs-dark",
      inherit: true,
      rules: [],
      colors: {
        "editor.background": "#0e1118",
        "editorLineNumber.foreground": "#4f5666",
        "editorCursor.foreground": "#8b7cf4",
      },
    });

    monaco.editor.defineTheme("coding-light", {
      base: "vs",
      inherit: true,
      rules: [],
      colors: {
        "editor.background": "#ffffff",
        "editorCursor.foreground": "#6253df",
      },
    });

    // Monaco's TypeScript contribution is loaded separately from the editor
    // core. Some Vite dependency-cache states briefly expose the editor before
    // that contribution is attached, so configuration must never assume it is
    // already available.
    const typescript = monaco.languages.typescript;
    if (typescript?.typescriptDefaults) {
      typescript.typescriptDefaults.setCompilerOptions({
        allowNonTsExtensions: true,
        jsx: typescript.JsxEmit.ReactJSX,
        target: typescript.ScriptTarget.ESNext,
      });
    }

    configured = true;
  }, []);

  return { configure };
}
