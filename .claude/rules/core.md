# Core Rules (Always On)

## Planning and Execution
- Use a plan for multi-file or ambiguous tasks.
- Prefer small, testable increments.
- For behavior changes, add or update tests.

## Memory and Documentation
- Keep `.claude/memory/active.yaml` concise and current (always-loaded memory).
- Record durable decisions in `.claude/memory/decisions.md`.
- Record root-cause bug fixes in `.claude/memory/bugs.md`.
- Record reusable implementation patterns in `.claude/memory/patterns.md`.
- Use `.claude/memory/context.md` for expanded session context when needed.

## Security and Safety
- Never expose or request secrets from `.env`, `appsettings*.json`, or credentials folders.
- Follow repository security and architecture constraints in `.claude/constitution.md`.
- Never commit secrets, API keys, or credentials to source control.

## Token Discipline
- Do not paste long logs or generated files into chat.
- Read large outputs from files on demand.
- Prefer concise summaries and focused context loading.
- Keep always-loaded memory small; store detail in archive files.
