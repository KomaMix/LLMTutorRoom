# Nginx Gateway

There are three nginx configs:

- `nginx.local.conf` - local nginx on the host, services also on the host.
- `nginx.docker.conf` - nginx in Docker, services on the host.
- `nginx.compose.conf` - nginx and services in the same Docker Compose network.

Expected service ports when .NET services run locally (directly or with the
infra-only Compose file):

- AuthService: `http://localhost:5210`
- LLMTutorRoom: `http://localhost:5206`
- TeachingService: `http://localhost:5212`
- GroupService: `http://localhost:5218`
- AttemptService: `http://localhost:5216`
- LLMGateway: `http://localhost:5200`
- ReviewService: `http://127.0.0.1:5214`
- Nginx gateway: `http://localhost:8080`

Routes:

- `/api/auth/*` -> AuthService
- `/api/users/*` -> AuthService
- `/api/classroom/*` -> LLMTutorRoom
- `/api/teaching/*` -> TeachingService
- `/api/groups` and `/api/groups/*` -> GroupService
- `/api/attempts` and `/api/attempts/*` -> AttemptService
- `/api/reviews/*` -> ReviewService
- `/api/model-access/*` -> ReviewService
- `/api/llm/models*` -> административный API LLMGateway с JWT и ролью Admin; служебные вызовы `/internal/chat/{modelId}` выполняются напрямую без аутентификации
- `/` -> LLMTutorRoom frontend

Nginx explicitly returns `404` for `/internal` and `/internal/*`; these paths
are never passed to the frontend fallback or any backend service. Public routes
of `GroupService`, `AttemptService` and `ReviewService` validate JWT and roles themselves.
Their internal overview/history endpoints remain reachable only by backend
services, while health endpoints are not routed through nginx.

Run with local nginx from the repository root:

```bash
brew install nginx
nginx -c "$PWD/infra/nginx/nginx.local.conf" -p "$PWD/infra/nginx"
```

Stop with:

```bash
nginx -s stop -c "$PWD/infra/nginx/nginx.local.conf" -p "$PWD/infra/nginx"
```

Run with Docker without Compose:

```bash
docker run --rm --name llm-nginx \
  -p 8080:8080 \
  -v "$PWD/infra/nginx/nginx.docker.conf:/etc/nginx/nginx.conf:ro" \
  nginx:1.27
```

Run nginx and RabbitMQ in Docker Compose, while keeping .NET services on the
host:

```bash
docker compose -f docker-compose.infra.yml up
```

In this mode, start `GroupService` on `localhost:5218`, `AttemptService` on
`localhost:5216` and `ReviewService` on
`127.0.0.1:5214`. Nginx proxies their public API routes and continues to reject
every `/internal/*` request.

Run the full Docker stack from the repository root:

```bash
docker compose up --build
```

In the full Compose stack, `AttemptService` and `ReviewService` are reachable
inside Docker as `http://attempt-service:8080` and
`http://review-service:8080`; Compose does not publish their ports on the host.
Nginx exposes only `/api/attempts` and `/api/attempts/*` from AttemptService;
from ReviewService it exposes `/api/reviews/*` and `/api/model-access/*`.
Neither service's `/internal/*` API is exposed.

`LLMTutorRoom` is likewise reached through nginx in the full stack and has no
direct host port. Port `5206` above applies only when the .NET service is run
locally for the infra-only nginx configuration.

`TeachingService` is likewise not published on host port `5212` in the full
Compose stack. Nginx reaches it as `http://teaching-service:8080` and exposes
only `/api/teaching/*`; backend containers can use the same container address.
Port `5212` is used only when TeachingService itself runs on the host (local or
infra-only mode).

`GroupService` owns group membership and teacher selection. It is reachable as
`http://group-service:8080` inside Compose and has no published host port there.
Nginx exposes its `/api/groups` routes; its `/internal/*` API remains available
only to backend services without authentication. Test assignments stay in
`TeachingService` and use the group's current membership.
