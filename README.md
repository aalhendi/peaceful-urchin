# Peaceful Urchin

A loan and credit microservices task using .NET and PostgreSQL. The [task](task.md) defines the required behavior and deliverables.

## Run locally

Install Docker with Compose, then start the services from the repository root:

```sh
docker compose up --build --detach --wait
```

One PostgreSQL container hosts separate `access_db`, `lending_db`, and `credit_db` databases.

Run the database integration tests in a container, using the same PostgreSQL service:

```sh
docker compose run --build --rm tests
```

`docker compose down` stops the containers and keeps PostgreSQL's data. `docker compose down --volumes` also removes the development and test databases.

## AI Use Disclosure

OpenAI GPT 6-Sol (Proprietary) and local Google Gemma-4-E4B (Apache 2.0) used as research and learning aids.
- Find and compare online sources such as https://learn.microsoft.com, and my personal Obsidian vault.
- Translate concepts I know from Rust / Java into C# and .NET.
- Code generation for test cases. Used to validate invariants that I expect to be held up.
- Commit message generation

In short, I treat it as a **glorified search engine and concept translator**, with a human in the loop for decisions, verification and implementation.

The disclosure will be updated accordingly in case any changes occur.
