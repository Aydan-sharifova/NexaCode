export async function shareUrl(
  title: string,
  url: string,
): Promise<"shared" | "copied" | "cancelled"> {
  if (navigator.share) {
    try {
      await navigator.share({ title, url });
      return "shared";
    } catch (error) {
      if (error instanceof DOMException && error.name === "AbortError")
        return "cancelled";
    }
  }
  await navigator.clipboard.writeText(url);
  return "copied";
}
