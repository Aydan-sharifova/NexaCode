export type SuggestionDiffLine = {
  kind: "context" | "removed" | "added";
  text: string;
};

export function buildSuggestionDiff(
  before: string,
  after: string,
  maximumLines = 400,
) {
  const left = before.replace(/\r\n/g, "\n").split("\n");
  const right = after.replace(/\r\n/g, "\n").split("\n");
  let prefix = 0;
  while (
    prefix < left.length &&
    prefix < right.length &&
    left[prefix] === right[prefix]
  )
    prefix++;
  let suffix = 0;
  while (
    suffix < left.length - prefix &&
    suffix < right.length - prefix &&
    left[left.length - 1 - suffix] === right[right.length - 1 - suffix]
  )
    suffix++;

  const lines: SuggestionDiffLine[] = [
    ...left
      .slice(0, prefix)
      .map((text) => ({ kind: "context" as const, text })),
    ...left
      .slice(prefix, left.length - suffix)
      .map((text) => ({ kind: "removed" as const, text })),
    ...right
      .slice(prefix, right.length - suffix)
      .map((text) => ({ kind: "added" as const, text })),
    ...(suffix
      ? left
          .slice(left.length - suffix)
          .map((text) => ({ kind: "context" as const, text }))
      : []),
  ];
  return {
    lines: lines.slice(0, maximumLines),
    truncated: lines.length > maximumLines,
  };
}
