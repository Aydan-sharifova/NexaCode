import { zodResolver } from "@hookform/resolvers/zod";
import { useEffect } from "react";
import { useQuery } from "@tanstack/react-query";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { Dialog } from "../../components/ui/Dialog";
import type { ProjectInput } from "./types";
import { programmingLanguageApi } from "../programmingLanguages/api";

const schema = z.object({ name: z.string().trim().min(2, "Use at least 2 characters.").max(120), description: z.string().trim().max(1000).optional(), defaultLanguage: z.string().trim().min(1, "Choose a language.").max(50), isPublic: z.boolean(), deadlineAt: z.string().optional() });

export function ProjectFormDialog({ open, initial, pending, onClose, onSubmit }: { open: boolean; initial?: ProjectInput; pending: boolean; onClose: () => void; onSubmit: (value: ProjectInput) => Promise<void> }) {
  const languages = useQuery({ queryKey: ["programming-languages"], queryFn: programmingLanguageApi.list, enabled: open });
  const { register, handleSubmit, reset, formState: { errors } } = useForm<ProjectInput>({ resolver: zodResolver(schema), defaultValues: initial ?? { name: "", description: "", defaultLanguage: "TypeScript", isPublic: false } });
  useEffect(() => reset(initial ?? { name: "", description: "", defaultLanguage: "TypeScript", isPublic: false }), [initial, open, reset]);
  const options = languages.data ?? [];
  const currentMissing = initial?.defaultLanguage && !options.some((language) => language.name === initial.defaultLanguage);
  const submit = (value: ProjectInput) => onSubmit({ ...value, deadlineAt: value.deadlineAt ? new Date(value.deadlineAt).toISOString() : undefined });
  return <Dialog open={open} onClose={onClose} title={initial ? "Edit project" : "Create a project"} description="Set the workspace basics. You can change these later." footer={<><button className="ui-button ghost" onClick={onClose}>Cancel</button><button className="ui-button primary" form="project-form" disabled={pending || languages.isLoading || options.length === 0}>{pending ? "Saving…" : initial ? "Save changes" : "Create project"}</button></>}><form id="project-form" className="feature-form" onSubmit={handleSubmit(submit)}><label>Project name<input {...register("name")} autoFocus />{errors.name && <span>{errors.name.message}</span>}</label><label>Description<textarea {...register("description")} rows={3} />{errors.description && <span>{errors.description.message}</span>}</label><label>Primary language<select {...register("defaultLanguage")} disabled={languages.isLoading}>{languages.isLoading && <option>Loading languages…</option>}{currentMissing && <option value={initial.defaultLanguage}>{initial.defaultLanguage} (inactive)</option>}{options.map((language) => <option key={language.id} value={language.name}>{language.name}</option>)}</select>{languages.isError && <span>Languages could not be loaded. Please try again.</span>}{!languages.isLoading && !languages.isError && options.length === 0 && <span>No active language is configured. Contact an administrator.</span>}</label>{!initial && <label>Deadline (optional)<input type="datetime-local" min={new Date().toISOString().slice(0, 16)} {...register("deadlineAt")} /><small>Developers become read-only when this deadline expires.</small></label>}<label className="check-row"><input type="checkbox" {...register("isPublic")} /><span><strong>Public project</strong><small>Visible to everyone, editable only by members.</small></span></label></form></Dialog>;
}
