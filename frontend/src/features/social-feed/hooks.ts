import { useInfiniteQuery, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { feedKeys, socialFeedApi, type CreatePostInput, type FeedTab } from "./api";

export function useFeed(tab: FeedTab, enabled = true) {
  return useInfiniteQuery({ queryKey: feedKeys.list(tab), queryFn: ({ pageParam }) => socialFeedApi.feed(tab, pageParam), initialPageParam: undefined as string | undefined, getNextPageParam: page => page.nextCursor, enabled });
}
export function useSavedFeed(enabled = true) {
  return useInfiniteQuery({ queryKey: feedKeys.saved, queryFn: ({ pageParam }) => socialFeedApi.saved(pageParam), initialPageParam: undefined as string | undefined, getNextPageParam: page => page.nextCursor, enabled });
}
export function useSocialDiscover(){return useQuery({queryKey:feedKeys.discover,queryFn:()=>socialFeedApi.discover()});}
function useRefreshFeed() { const client = useQueryClient(); return () => client.invalidateQueries({ queryKey: feedKeys.all }); }
export function useCreateFeedPost() { const refresh = useRefreshFeed(); return useMutation({ mutationFn: (input: CreatePostInput) => socialFeedApi.create(input), onSuccess: refresh }); }
export function useDeleteFeedPost() { const refresh = useRefreshFeed(); return useMutation({ mutationFn: socialFeedApi.remove, onSuccess: refresh }); }
export function useToggleLike() { const refresh = useRefreshFeed(); return useMutation({ mutationFn: socialFeedApi.like, onSuccess: refresh }); }
export function useToggleSave() { const refresh = useRefreshFeed(); return useMutation({ mutationFn: socialFeedApi.save, onSuccess: refresh }); }
export function useSharePost() { const refresh = useRefreshFeed(); return useMutation({ mutationFn: socialFeedApi.share, onSuccess: refresh }); }
export function useComments(postId: string, enabled: boolean) { return useInfiniteQuery({ queryKey: feedKeys.comments(postId), queryFn: ({ pageParam }) => socialFeedApi.comments(postId, pageParam), initialPageParam: undefined as string | undefined, getNextPageParam: page => page.nextCursor, enabled }); }
export function useAddComment(postId: string) { const client = useQueryClient(); const refresh = useRefreshFeed(); return useMutation({ mutationFn: ({content,parentCommentId}:{content:string;parentCommentId?:string}) => socialFeedApi.comment(postId, content,parentCommentId), onSuccess: () => { client.invalidateQueries({ queryKey: feedKeys.comments(postId) }); refresh(); } }); }
export function useDeleteComment(postId: string) { const client = useQueryClient(); const refresh = useRefreshFeed(); return useMutation({ mutationFn: socialFeedApi.removeComment, onSuccess: () => { client.invalidateQueries({ queryKey: feedKeys.comments(postId) }); refresh(); } }); }
