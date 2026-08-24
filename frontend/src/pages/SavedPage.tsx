import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Link } from "react-router-dom";
import { EmptyState, ErrorState, LoadingState } from "../components/AsyncState";
import {
  savedApi,
  type SavedPackage,
  type SavedType,
} from "../features/saved/api";
import "./SavedPage.css";
import { queryKeys } from "../services/queryKeys";
const types: SavedType[] = [
  "All",
  "Posts",
  "Projects",
  "Snippets",
  "Templates",
  "Agents",
];
function Packages({ items }: { items: SavedPackage[] }) {
  return (
    <div className="saved-grid">
      {items.map((x) => (
        <article className="saved-card" key={x.id}>
          <small>{x.category}</small>
          <h3>{x.title}</h3>
          <p>{x.description}</p>
          <footer>
            {x.tags.join(" · ")}
            <Link to="/marketplace">Marketplace</Link>
          </footer>
        </article>
      ))}
    </div>
  );
}
export function SavedPage() {
  const [type, setType] = useState<SavedType>("All");
  const [draft, setDraft] = useState("");
  const [search, setSearch] = useState("");
  const query = useQuery({
    queryKey: queryKeys.saved.list(type, search),
    queryFn: () => savedApi.list(type, search),
  });
  const data = query.data;
  const count = data
    ? data.posts.length +
      data.projects.length +
      data.snippets.length +
      data.templates.length +
      data.agents.length
    : 0;
  return (
    <main className="saved-page">
      <header>
        <small>YOUR LIBRARY</small>
        <h1>Saved</h1>
        <p>Posts, projects, snippets, templates and AI agents in one place.</p>
      </header>
      <form
        onSubmit={(e) => {
          e.preventDefault();
          setSearch(draft.trim());
        }}
      >
        <input
          value={draft}
          onChange={(e) => setDraft(e.target.value)}
          placeholder="Search saved content…"
        />
        <button>Search</button>
      </form>
      <nav>
        {types.map((x) => (
          <button
            className={type === x ? "active" : ""}
            onClick={() => setType(x)}
            key={x}
          >
            {x}
          </button>
        ))}
      </nav>
      {query.isPending ? (
        <LoadingState label="Loading saved content…" />
      ) : query.isError ? (
        <ErrorState
          message={query.error.message}
          retry={() => void query.refetch()}
        />
      ) : count === 0 ? (
        <EmptyState
          title="Nothing saved here"
          description="Save useful community content to build your library."
        />
      ) : (
        <div className="saved-sections">
          {data!.posts.length > 0 && (
            <section>
              <h2>Posts</h2>
              <div className="saved-grid">
                {data!.posts.map((x) => (
                  <article className="saved-card" key={x.id}>
                    <p>{x.content}</p>
                    <footer>
                      <Link to={`/users/${x.author.publicId}`}>
                        @{x.author.userName}
                      </Link>
                    </footer>
                  </article>
                ))}
              </div>
            </section>
          )}
          {data!.projects.length > 0 && (
            <section>
              <h2>Projects</h2>
              <div className="saved-grid">
                {data!.projects.map((x) => (
                  <Link
                    className="saved-card"
                    to={`/public/projects/${x.id}`}
                    key={x.id}
                  >
                    <small>{x.language}</small>
                    <h3>{x.name}</h3>
                    <p>{x.description || "Public project"}</p>
                  </Link>
                ))}
              </div>
            </section>
          )}
          {data!.snippets.length > 0 && (
            <section>
              <h2>Snippets</h2>
              <div className="saved-grid">
                {data!.snippets.map((x) => (
                  <article className="saved-card" key={x.id}>
                    <small>{x.language}</small>
                    <pre>{x.content}</pre>
                    <footer>
                      <Link to={`/users/${x.author.publicId}`}>
                        @{x.author.userName}
                      </Link>
                    </footer>
                  </article>
                ))}
              </div>
            </section>
          )}
          {data!.templates.length > 0 && (
            <section>
              <h2>Templates</h2>
              <Packages items={data!.templates} />
            </section>
          )}
          {data!.agents.length > 0 && (
            <section>
              <h2>AI agents</h2>
              <Packages items={data!.agents} />
            </section>
          )}
        </div>
      )}
    </main>
  );
}
