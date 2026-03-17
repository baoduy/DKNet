# How to use spec-kit

## Agent folder security

> Some agents may store credentials, auth tokens, or other identifying/private artifacts in the agent folder within your project.
>
> Consider adding `.github/` (or parts of it) to `.gitignore` to prevent accidental credential leakage.

## Next steps

1. You are already in the project directory.
2. Start using slash commands with your AI agent:
   1. `/speckit.constitution` - Establish project principles
   2. `/speckit.specify` - Create baseline specification
   3. `/speckit.plan` - Create implementation plan
   4. `/speckit.tasks` - Generate actionable tasks
   5. `/speckit.implement` - Execute implementation

## Enhancement commands

Optional commands for improving spec quality and confidence:

- `/speckit.clarify` (optional) - Ask structured questions to de-risk ambiguous areas before planning (run before `/speckit.plan` if used)
- `/speckit.analyze` (optional) - Run a cross-artifact consistency and alignment report (after `/speckit.tasks`, before `/speckit.implement`)
- `/speckit.checklist` (optional) - Generate quality checklists to validate requirements completeness, clarity, and consistency (after `/speckit.plan`)
