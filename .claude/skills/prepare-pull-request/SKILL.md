---
name: prepare-pull-request
description: Inspect a branch, validate its changes, identify unrelated edits, and prepare a structured pull request summary. Use before opening or updating a pull request.
disable-model-invocation: true
allowed-tools:
  - "Bash(git status *)"
  - "Bash(git diff *)"
  - "Bash(git log *)"
  - "Bash(dotnet build *)"
  - "Bash(dotnet test *)"
  - "Bash(npm --prefix weather.client run build)"
  - "Bash(npm --prefix weather.client run lint)"
---

# Pull Request Preparation

Original task: $ARGUMENTS

## Process

1. Inspect the branch against `master`, including uncommitted and untracked files:

   ```bash
   git status --short
   git log master..HEAD
   git diff master...HEAD
   ```

2. Compare the change with the original task. Take the task from the arguments above, then this conversation, then the commit messages. If it's still unclear, ask.
3. Identify unrelated, generated, or accidental modifications.
4. Scan the diff for API keys, tokens and other secrets. If you find one, warn the user before anything else.
5. Run the required checks and record each result:

   ```bash
   dotnet build Weather.slnx
   dotnet test Weather.Core.Tests/Weather.Core.Tests.csproj
   npm --prefix weather.client run build
   npm --prefix weather.client run lint
   ```

   If the build reports locked files, another `Weather.Server` process is running. Report that check as not completed.

6. Identify user-visible behavior and compatibility impact. Treat a change as breaking if it changes the `/api/forecast` response, removes or renames a setting, or changes a public `Weather.Core` type.
7. Identify rollout needs. Merging to `master` publishes a new image, so check whether the NAS needs new environment variables or a redeploy.
8. Draft a concise title and pull request description. The title follows Conventional Commits, using the types and scopes in the `commit-message` skill.

## Output

Include every section, and write "None" when one doesn't apply.

- Proposed title
- Problem
- Solution
- Important implementation details
- User-visible changes
- Tests and validation
- Migration or rollout notes
- Risks and follow-up work
- Unrelated changes to remove

## Constraints

- Do not push, commit, or open the pull request without approval.
- Do not claim a check passed unless it was run.
