# Development Rules

- Always read `.agent` files before making changes.
- Always update `.agent` files when project direction, architecture, roadmap, or important implementation details change.
- Always update `.agent/session-log.md` after meaningful work.
- Always update `README.md` when public project information changes.
- Keep `README.md` in English.
- Keep code comments in English.
- Do not write comments inside code unless they are genuinely useful.
- Do not add comments in Russian inside code.
- Keep localization keys synchronized between `en.json` and `ru.json`.
- Never add visible UI text without adding it to both localization JSON files.
- Keep business logic out of MAUI pages.
- Keep view models thin and delegate use cases to application services.
- Use async and await for I/O.
- Avoid committing secrets, Firebase credentials, local databases, build outputs, or user-specific IDE files.
- Use `develop` as the integration branch and create feature branches from it.
- Use clear English commit messages.
- Add or update tests for domain and application behavior changes.
