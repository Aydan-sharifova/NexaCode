import { Component, type ErrorInfo, type ReactNode } from "react";

interface Props { children: ReactNode; }
interface State { error?: Error; }

export class RouteErrorBoundary extends Component<Props, State> {
  state: State = {};
  static getDerivedStateFromError(error: Error): State { return { error }; }
  componentDidCatch(error: Error, info: ErrorInfo) { console.error("Route rendering failed", error, info); }
  private retry = () => {
    const message = this.state.error?.message ?? "";
    if (/dynamically imported module|loading chunk|importing a module/i.test(message)) {
      window.location.reload();
      return;
    }
    this.setState({ error: undefined });
  };
  render() {
    if (!this.state.error) return this.props.children;
    return <main className="route-error" role="alert"><div className="brand-mark" aria-hidden="true">C</div><h1>This workspace could not be displayed</h1><p>{this.state.error.message || "An unexpected interface error occurred."}</p><div><button className="ui-button primary" onClick={this.retry}>Try again</button><button className="ui-button ghost" onClick={() => window.location.reload()}>Reload workspace</button><button className="ui-button ghost" onClick={() => { window.location.href = "/projects"; }}>Back to projects</button></div></main>;
  }
}
