# Execution worker security

No code-execution worker, queue consumer or Docker sandbox implementation was detected. The public API image has no Docker socket and must never receive one. Production code execution therefore remains disabled/not implemented.

The required future design is: API submits a signed, bounded job to an authenticated queue; a separate execution VPS consumes it and creates an ephemeral sandbox. The worker must run non-root, apply CPU/RAM/PID/time/output limits, disable network by default, mount no host or production secrets, use an ephemeral filesystem, drop capabilities, avoid privileged/host networking, and always clean up containers. The execution host must have no production database access. Threat modeling and abuse tests are required before launch.
