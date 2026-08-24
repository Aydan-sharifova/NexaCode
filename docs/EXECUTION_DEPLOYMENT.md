# Execution security

The application has a development C# runner behind the authenticated project-member endpoint `POST /api/projects/{projectId}/execution/csharp`. It never runs submitted source directly on the API host. `ContainerRuntimeProvider` writes source to a server-created temporary directory and invokes a fixed Docker command with:

- no network;
- 256 MB memory, 0.75 CPU and 64 PID limits;
- a 15-second hard maximum and bounded output;
- all Linux capabilities dropped and `no-new-privileges`;
- read-only container root plus an ephemeral `/tmp`;
- only the unique execution directory mounted into `/workspace`;
- unconditional best-effort cleanup.

Local development requires Docker Desktop and the configured .NET SDK image. `appsettings.Development.json` enables the runner for direct local API development. Compose keeps it disabled unless `EXECUTION_ENABLED=true` is explicitly supplied.

## Production boundary

Production configuration hard-disables execution. Do not mount the Docker socket into the public API container and do not enable this in `docker-compose.prod.yml`.

The production design remains a separate execution service: the API submits a signed, bounded job to an authenticated queue; a dedicated execution worker creates the ephemeral sandbox and streams status/output back. That host must run non-root, have no production database or application secrets, enforce per-user quotas and concurrency, keep networking disabled by default, validate fixed runtime images, and clean up every container. Threat modelling and abuse tests are required before production launch.
