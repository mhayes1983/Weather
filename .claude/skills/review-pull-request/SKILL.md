---
name: review-pull-request
description: Review a branch or pull request for merge readiness against this repo's rules, covering requirements, correctness, security, compatibility, maintainability and missing tests. Use when asked to review a PR, branch diff or patch, or whether a branch is ready to merge.
context: fork
agent: general-purpose
---

# Pull Request Review

## Objective

Review the current branch against `master` without changing the code.

Requirements and extra context: $ARGUMENTS

## Required Context

This review runs without the conversation that started it, so gather the context here:

- **Requirements:** use the arguments above first, then the branch's commit messages. If neither states them, say so in the report and review the change on its own terms. Don't guess at requirements.
- **Pull request description:** use it if the arguments include it. This repo has no issues to read, and `gh` may not be installed.
- **Complete branch diff:** `git diff master...HEAD`, plus `git log master..HEAD` and `git status --short` for uncommitted or untracked work.
- **Repository instructions:** `.claude/CLAUDE.md`.
- **Surrounding code:** the implementation and tests around each change.

## Process

1. Compare the change with the requirements.
2. Inspect the complete diff, including generated and configuration files.
3. Read surrounding code and trace affected callers.
4. Review correctness, error behaviour and concurrency.
5. Check input handling and sensitive data:
   - No keys in the repo, including `appsettings.json` and `deploy/`.
   - No URLs logged from the `weather` HTTP client.
   - `/api/station` exposes no keys, MAC address, street address, coordinates or neighbourhood, and accepts only configured station IDs.
6. Check this repo's rules:
   - `Weather.Core` stays free of ASP.NET.
   - Temperatures stay in °F in Core and the API, and highlighting is decided in Core.
   - One bad source can't break the table.
   - Sources use their own timeout, not the request's cancellation token.
   - All caching stays in memory.
   - The home station stays on Ambient Weather.
7. Check compatibility and rollout:
   - Changes to the `/api/forecast` response, settings or public `Weather.Core` types.
   - New environment variables in `deploy/truenas-compose.yaml`. Merging to `master` publishes a new image.
8. Review test coverage for success and failure cases.
9. Run the checks below, then report only evidence-based findings.

## Checks

```bash
dotnet build Weather.slnx
dotnet test Weather.Core.Tests/Weather.Core.Tests.csproj
npm --prefix weather.client run build
npm --prefix weather.client run lint
```

If the build reports locked files, another `Weather.Server` process is running. That isn't a code error, so report the check as not completed.

## Output

Rank findings most severe first, using these levels:

- **Blocker:** wrong behaviour, a security or data exposure problem, or a broken repo rule. Must be fixed before merging.
- **Major:** a real defect with limited reach, or a missing test for risky behaviour. Should be fixed before merging.
- **Minor:** a small defect or maintainability problem that can wait.

For each finding:

- Severity
- File and location
- Evidence
- Failure scenario
- Impact
- Recommended correction
- Verification method

Finish with requirement coverage, the checks you ran and their results, and one verdict: **Ready**, **Ready with follow-ups** or **Not ready**.

## Constraints

- Do not modify source files, commit or push. Running the checks above is allowed, even though they write build output.
- Separate confirmed defects from concerns that need validation.
- Do not report style preferences as blockers.
- Do not claim tests passed unless they were run.
