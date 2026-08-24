import type { MarketplaceCategory, MarketplaceItem } from "./types";

const publishedAt = "2026-08-20T10:00:00.000Z";

function item(
  slug: string,
  title: string,
  description: string,
  category: MarketplaceCategory,
  author: string,
  tags: string[],
  downloads: number,
  likes: number,
  version = "1.0.0",
): MarketplaceItem {
  return {
    id: `sample-marketplace-${slug}`,
    slug,
    title,
    description,
    category,
    status: "Published",
    author: { id: `sample-author-${author}`, publicId: author, userName: author, fullName: author },
    tags,
    downloads,
    likes,
    isLiked: false,
    isSaved: false,
    latestVersion: {
      id: `sample-version-${slug}`,
      version,
      permissions: ["project.read"],
      checksum: `sample-${slug}`,
      isPublished: true,
      createdAt: publishedAt,
      publishedAt,
    },
    updatedAt: publishedAt,
  };
}

export const sampleMarketplaceItems: MarketplaceItem[] = [
  item("react-dashboard-kit", "React Dashboard Kit", "Responsive dashboard starter with charts, tables, authentication screens, and dark mode.", "ProjectTemplate", "nexalabs", ["react", "typescript", "dashboard"], 2840, 318, "2.4.1"),
  item("midnight-aurora", "Midnight Aurora", "A polished deep-space theme with accessible contrast and carefully tuned editor colors.", "Theme", "aylin", ["dark", "editor", "accessible"], 1975, 246, "1.8.0"),
  item("api-compass", "API Compass", "Explore OpenAPI endpoints, generate typed clients, and keep contracts in sync.", "Plugin", "devtools", ["openapi", "api", "generator"], 1642, 189, "3.1.0"),
  item("review-pilot", "Review Pilot", "An AI review agent that spots risky changes and produces concise, actionable feedback.", "AiAgent", "codecraft", ["ai", "review", "quality"], 3510, 472, "1.6.2"),
  item("test-forge", "Test Forge", "Creates focused unit-test plans from changed files and your existing test conventions.", "AiAgent", "shipfast", ["ai", "testing", "automation"], 2214, 301, "2.0.3"),
  item("fetch-retry", "Typed Fetch Retry", "A compact TypeScript fetch helper with backoff, cancellation, and typed errors.", "Snippet", "mira", ["typescript", "fetch", "utility"], 986, 124, "1.2.0"),
  item("command-palette", "Command Palette", "Keyboard-first React command palette with search, groups, and recent actions.", "Component", "pixelworks", ["react", "ui", "keyboard"], 1436, 207, "2.2.0"),
  item("saas-launchpad", "SaaS Launchpad", "Production-minded SaaS foundation with billing, teams, onboarding, and settings.", "ProjectTemplate", "northstar", ["saas", "fullstack", "starter"], 4120, 536, "4.0.0"),
  item("database-lens", "Database Lens", "Inspect schemas, preview safe queries, and generate migration summaries in your workspace.", "Plugin", "orbit", ["database", "sql", "schema"], 1188, 156, "1.4.3"),
];

export function filterSampleMarketplace(category?: MarketplaceCategory, search?: string) {
  const query = search?.trim().toLocaleLowerCase();
  return sampleMarketplaceItems.filter(entry =>
    (!category || entry.category === category) &&
    (!query || [entry.title, entry.description, entry.author.userName, ...entry.tags]
      .some(value => value.toLocaleLowerCase().includes(query))),
  );
}
