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
- AttemptService: `http://localhost:5216`
- LLMGateway: `http://localhost:5200`
- ReviewService: `http://127.0.0.1:5214` (internal, loopback only)
- Nginx gateway: `http://localhost:8080`

Routes:

- `/api/auth/*` -> AuthService
- `/api/users/*` -> AuthService
- `/api/classroom/*` -> LLMTutorRoom
- `/api/teaching/*` -> TeachingService
- `/api/attempts` and `/api/attempts/*` -> AttemptService
- `/api/llm/*` -> LLMGateway, with `/api/llm/chat/...` rewritten to `/api/chat/...`
- `/` -> LLMTutorRoom frontend

Nginx explicitly returns `404` for `/internal` and `/internal/*`; these paths
are never passed to the frontend fallback or any backend service. The public
AttemptService routes validate the student's JWT themselves; its internal
student-overview endpoint remains reachable only through the backend network.

`ReviewService` deliberately has no nginx route. Its `/internal/*` API is called
by `LLMTutorRoom`, has no application-level authentication, and must be reachable
only through the protected backend network. Health endpoints are also not routed
through nginx.

Run with local nginx:

```bash
brew install nginx
nginx -c /Users/sabirovkamil/RiderProjects/LLMGateway/infra/nginx/nginx.local.conf -p /Users/sabirovkamil/RiderProjects/LLMGateway/infra/nginx
```

Stop with:

```bash
nginx -s stop -c /Users/sabirovkamil/RiderProjects/LLMGateway/infra/nginx/nginx.local.conf -p /Users/sabirovkamil/RiderProjects/LLMGateway/infra/nginx
```

Run with Docker without Compose:

```bash
docker run --rm --name llm-nginx \
  -p 8080:8080 \
  -v /Users/sabirovkamil/RiderProjects/LLMGateway/infra/nginx/nginx.docker.conf:/etc/nginx/nginx.conf:ro \
  nginx:1.27
```

Run nginx and RabbitMQ in Docker Compose, while keeping .NET services on the
host:

```bash
docker compose -f docker-compose.infra.yml up
```

In this mode, start `AttemptService` on `localhost:5216` and `ReviewService` on
`127.0.0.1:5214`. Nginx proxies only the public AttemptService API and does not
proxy ReviewService.

Run the full Docker stack from the repository root:

```bash
docker compose up --build
```

In the full Compose stack, `AttemptService` and `ReviewService` are reachable
inside Docker as `http://attempt-service:8080` and
`http://review-service:8080`; Compose does not publish their ports on the host.
Nginx exposes only `/api/attempts` and `/api/attempts/*` from AttemptService;
neither service's `/internal/*` API is exposed.

`LLMTutorRoom` is likewise reached through nginx in the full stack and has no
direct host port. Port `5206` above applies only when the .NET service is run
locally for the infra-only nginx configuration.

`TeachingService` is likewise not published on host port `5212` in the full
Compose stack. Nginx reaches it as `http://teaching-service:8080` and exposes
only `/api/teaching/*`; backend containers can use the same container address.
Port `5212` is used only when TeachingService itself runs on the host (local or
infra-only mode).
