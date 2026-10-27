---
description: Weekly documentation upkeep for the C# API, frontend and Azure deployment.
on:
  schedule: weekly
  workflow_dispatch:
permissions:
  contents: read
  issues: read
  pull-requests: read
engine: copilot
timeout-minutes: 20
tools:
  github:
    toolsets: [repos, pull_requests]
  edit:
  bash: ["ls", "cat", "grep", "find", "git log", "git diff", "git status"]
safe-outputs:
  create-pull-request:
    title-prefix: "[docs-upkeep] "
    labels: [documentation, automation]
    draft: true
    max: 1
    allowed-files:
      - "Readme.md"
      - "src/README.md"
      - "src/docs/**"
      - "infra/README.md"
  noop:
---

# Documentation upkeep

You keep the OctoCAT Supply documentation accurate. The repository now contains only the C# API (`src/api-cs`), the React frontend (`src/frontend`), SQLite (`src/database`) and Azure infrastructure (`infra/`, `.github/workflows/deploy.yml`). The TypeScript, Python and Java APIs were removed.

## Task

1. Read the documentation files: `Readme.md`, `src/README.md`, `src/docs/*.md` and `infra/README.md`.
2. Compare them with the code. Look for statements that are now wrong, such as references to removed APIs (`api-ts`, `api-py`, `api-java`), `make` targets that no longer exist (check `src/Makefile`), outdated commands, ports, environment variables or endpoints (check `src/api-cs/Program.cs` and the controllers).
3. Fix the inaccuracies with the smallest edits that make the docs correct. Remove sections that only describe removed components rather than rewriting them.
4. Open one draft pull request listing each change and the evidence from the code that justified it.

## Rules

- Only edit Markdown files under `src/README.md`, `src/docs/`, `Readme.md` and `infra/README.md`. Never edit source code, workflows, Terraform, `challenges/`, `walkthrough/` or `.github/`.
- Do not invent facts. If you cannot confirm something from the repository, leave it unchanged and mention it in the pull request body.
- Do not add new documents.
- If the documentation is already accurate, call `noop` with a short explanation instead of opening a pull request.
