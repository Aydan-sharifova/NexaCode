import { useEffect, useState } from "react";
import { useInfiniteQuery } from "@tanstack/react-query";
import { searchApi } from "./api";
import type { SearchResultType } from "./types";

export function useGlobalSearch(query: string, type?: SearchResultType, projectId?: string) {
  const [debounced, setDebounced] = useState("");
  useEffect(() => {
    const timer = window.setTimeout(() => setDebounced(query.trim()), 300);
    return () => window.clearTimeout(timer);
  }, [query]);
  return useInfiniteQuery({
    queryKey: ["global-search", debounced, type, projectId],
    queryFn: ({ signal, pageParam }) => searchApi.search({ query: debounced, type, projectId, page: pageParam }, signal),
    initialPageParam: 1,
    getNextPageParam: (lastPage) => lastPage.groups.some((group) => group.hasMore) ? lastPage.page + 1 : undefined,
    enabled: debounced.length >= 2,
    staleTime: 15_000,
  });
}
