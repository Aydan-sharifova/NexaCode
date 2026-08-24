import { describe, expect, it } from "vitest";
import { queryKeys } from "./queryKeys";

describe("queryKeys", () => {
  it("keeps every repository view under one invalidation prefix", () => {
    const root = queryKeys.repository.all("project-1");
    expect(queryKeys.repository.status("project-1").slice(0, root.length)).toEqual(root);
    expect(queryKeys.repository.history("project-1").slice(0, root.length)).toEqual(root);
    expect(queryKeys.repository.branches("project-1").slice(0, root.length)).toEqual(root);
    expect(queryKeys.repository.diff("project-1", false).slice(0, root.length)).toEqual(root);
    expect(queryKeys.repository.commitDiff("project-1", "sha").slice(0, root.length)).toEqual(root);
  });

  it("defines stable shared keys for user deletion dependants", () => {
    expect(queryKeys.teamDirectory).toEqual(["team-directory"]);
    expect(queryKeys.projects).toEqual(["projects"]);
    expect(queryKeys.dashboard).toEqual(["dashboard"]);
  });

  it("keeps saved views under one invalidation prefix", () => {
    expect(queryKeys.saved.list("Projects").slice(0, 1)).toEqual(queryKeys.saved.all);
    expect(queryKeys.saved.project("project-1").slice(0, 1)).toEqual(queryKeys.saved.all);
  });
});
