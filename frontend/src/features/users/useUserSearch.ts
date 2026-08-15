import { useEffect, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { userKeys, usersApi } from "./api";

export function useUserSearch(query: string) {
  const [debounced, setDebounced] = useState("");
  useEffect(() => {
    const timer = window.setTimeout(() => setDebounced(query.trim()), 350);
    return () => window.clearTimeout(timer);
  }, [query]);
  return useQuery({
    queryKey: userKeys.search(debounced),
    queryFn: ({ signal }) => usersApi.search(debounced, signal),
    enabled: debounced.length >= 2,
    staleTime: 15_000,
  });
}
