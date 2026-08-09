# Nginx Gateway

There are three nginx configs:

- `nginx.local.conf` - local nginx on the host, services also on the host.
- `nginx.docker.conf` - nginx in Docker, services on the host.
- `nginx.compose.conf` - nginx and services in the same Docker Compose network.

Expected local service ports:

- AuthService: `http://localhost:5210`
- LLMTutorRoom: `http://localhost:5206`
- TeachingService: `http://localhost:5212`
- LLMGateway: `http://localhost:5200`
- Nginx gateway: `http://localhost:8080`

Routes:

- `/api/auth/*` -> AuthService
- `/api/users/*` -> AuthService
- `/api/classroom/*` -> LLMTutorRoom
- `/api/teaching/*` -> TeachingService
- `/api/llm/*` -> LLMGateway, with `/api/llm/chat/...` rewritten to `/api/chat/...`
- `/` -> LLMTutorRoom frontend

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

Run the full Docker stack from the repository root:

```bash
docker compose up --build
```
