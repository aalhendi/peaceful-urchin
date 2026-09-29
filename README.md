# Peaceful Urchin

A loan and credit microservices task using .NET and PostgreSQL. The [task](task.md) defines the required behavior and deliverables. The [architecture guide](docs/architecture.md) shows the services and the domain terms they use.

## Run locally

Install Docker with Compose. No local .NET SDK is needed. From the repository root, start the three APIs and PostgreSQL:

```sh
docker compose up --build --detach --wait
```

One PostgreSQL container hosts separate `access_db`, `lending_db`, and `credit_db` databases. The Compose stack runs the APIs in Development so their OpenAPI documents are available:

| Service | OpenAPI document | Liveness |
| --- | --- | --- |
| Access | http://127.0.0.1:8081/openapi/v1.json | http://127.0.0.1:8081/health/live |
| Lending | http://127.0.0.1:8082/openapi/v1.json | http://127.0.0.1:8082/health/live |
| Credit | http://127.0.0.1:8083/openapi/v1.json | http://127.0.0.1:8083/health/live |

The OpenAPI documents are JSON, with no separate UI. The APIs expose them only in Development.

## Demo users

| Institution | Username | Password |
| --- | --- | --- |
| Bank A | `bank@example.test` | `bank-local` |
| CINET | `cinet@example.test` | `cinet-local` |

Log in through Access's `POST /auth/login`, then send its access token as a Bearer token to protected routes. The seeded customer Civil ID `296051500019` can be used for reads and the demo flows.

## Tests

Run the integration tests in a container, using the same PostgreSQL service:

```sh
docker compose run --build --rm tests
```

`docker compose down` stops the containers and keeps PostgreSQL's data. `docker compose down --volumes` removes that local database so the next `up` starts fresh.

## AI Use Disclosure

OpenAI GPT 6-Sol (Proprietary) and local Google Gemma-4-E4B (Apache 2.0) used as research and learning aids.
- Find and compare online sources such as https://learn.microsoft.com, and my personal Obsidian vault.
- Translate concepts I know from Rust / Java into C# and .NET.
- Code generation for test cases. Used to validate invariants that I expect to be held up.
- Commit message generation
- Final Sanity checks

In short, I treat it as a **glorified search engine and concept translator**, with a human in the loop for decisions, verification and implementation.

The disclosure will be updated accordingly in case any changes occur.
