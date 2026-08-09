# Local Nginx Gateway

This config is for running services manually without Docker Compose.

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
