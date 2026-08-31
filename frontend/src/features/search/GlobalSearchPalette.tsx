import { useEffect, useMemo, useRef, useState } from "react";
import { useNavigate } from "react-router-dom";
import { Icon } from "../../components/Icon";
import { useGlobalSearch } from "./useGlobalSearch";
import type { SearchResult, SearchResultType } from "./types";

const recentKey = "coding.recent-searches";
const readRecent = () => {
  try { return JSON.parse(localStorage.getItem(recentKey) ?? "[]") as string[]; } catch { return []; }
};

export function GlobalSearchPalette({ open, onOpenChange }: { open: boolean; onOpenChange: (open: boolean) => void }) {
  const [query, setQuery] = useState("");
  const [activeIndex, setActiveIndex] = useState(0);
  const [recent, setRecent] = useState<string[]>(readRecent);
  const [type, setType] = useState<SearchResultType>();
  const input = useRef<HTMLInputElement>(null);
  const navigate = useNavigate();
  const search = useGlobalSearch(query, type);
  const groups = useMemo(() => {
    const combined = new Map<SearchResultType, SearchResult[]>();
    search.data?.pages.forEach((page) => page.groups.forEach((group) => {
      const current = combined.get(group.type) ?? [];
      const seen = new Set(current.map((item) => item.id));
      combined.set(group.type, [...current, ...group.items.filter((item) => !seen.has(item.id))]);
    }));
    return [...combined].map(([groupType, items]) => ({ type: groupType, items }));
  }, [search.data]);
  const results = useMemo(() => groups.flatMap((group) => group.items), [groups]);

  useEffect(() => { if (open) window.setTimeout(() => input.current?.focus(), 0); }, [open]);
  useEffect(() => setActiveIndex(0), [search.data]);
  if (!open) return null;

  const select = (result: SearchResult) => {
    const normalized = query.trim();
    if (normalized) {
      const next = [normalized, ...recent.filter((item) => item !== normalized)].slice(0, 6);
      setRecent(next); localStorage.setItem(recentKey, JSON.stringify(next));
    }
    onOpenChange(false); setQuery(""); navigate(result.navigationUrl);
  };
  const keyDown = (event: React.KeyboardEvent) => {
    if (event.key === "Escape") onOpenChange(false);
    if (event.key === "ArrowDown") { event.preventDefault(); setActiveIndex((value) => Math.min(results.length - 1, value + 1)); }
    if (event.key === "ArrowUp") { event.preventDefault(); setActiveIndex((value) => Math.max(0, value - 1)); }
    if (event.key === "Enter" && results[activeIndex]) { event.preventDefault(); select(results[activeIndex]); }
  };

  let cursor = -1;
  return <div className="search-palette-backdrop" onMouseDown={() => onOpenChange(false)}>
    <section className="search-palette" role="dialog" aria-modal="true" aria-label="Global search" onMouseDown={(event) => event.stopPropagation()} onKeyDown={keyDown}>
      <label htmlFor="global-search"><Icon name="search" /><input id="global-search" ref={input} value={query} onChange={(event) => setQuery(event.target.value)} placeholder="Search projects, files, users and tasks…" autoComplete="off" /><kbd>ESC</kbd></label>
      <nav className="search-type-filters" aria-label="Search result types">
        {([undefined, "Project", "File", "User", "Task"] as Array<SearchResultType | undefined>).map((value) => <button key={value ?? "All"} className={type === value ? "active" : ""} onClick={() => setType(value)}>{value ? `${value}s` : "All"}</button>)}
      </nav>
      <div className="search-palette-body">
        {query.trim().length < 2 && <div className="recent-searches"><strong>RECENT SEARCHES</strong>{recent.length ? recent.map((item) => <button key={item} onClick={() => setQuery(item)}><Icon name="search" />{item}</button>) : <p>Type at least 2 characters to search.</p>}</div>}
        {search.isFetching && <div className="search-skeleton">{[1, 2, 3].map((item) => <span key={item} />)}</div>}
        {!search.isFetching && query.trim().length >= 2 && !results.length && <div className="search-empty"><strong>No results</strong><p>Try another term or check the spelling.</p></div>}
        {!search.isLoading && groups.map((group) => group.items.length > 0 && <div className="search-result-group" key={group.type}><header><strong>{group.type}s</strong></header>{group.items.map((result) => {
          cursor += 1; const index = cursor;
          return <button className={index === activeIndex ? "active" : ""} key={`${result.type}-${result.id}`} onMouseEnter={() => setActiveIndex(index)} onClick={() => select(result)}><span className="search-result-icon">{result.type.slice(0, 1)}</span><span><b>{result.title}</b><small>{result.subtitle}</small></span><em>{result.matchedText}</em></button>;
        })}</div>)}
        {search.hasNextPage && <button className="search-load-more" disabled={search.isFetchingNextPage} onClick={() => void search.fetchNextPage()}>{search.isFetchingNextPage ? "Loading…" : "Load more"}</button>}
      </div>
      <footer><span>↑↓ Navigate</span><span>↵ Open</span><span>Esc Close</span></footer>
    </section>
  </div>;
}
