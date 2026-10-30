---
name: commit-message
description: Write a commit message for the current changes using the Conventional Commits standard. Use when the user asks for a commit message, or asks to describe, summarize or word their staged or uncommitted changes for a commit.
---

# Commit message

Write a commit message that follows [Conventional Commits 1.0.0](https://www.conventionalcommits.org/en/v1.0.0/). Base it on the actual diff, not on what the conversation says changed.

This skill only writes the message. Don't stage files or run `git commit` unless the user asks for that as well.

## Steps

1. See what changed:

   ```bash
   git status --short
   git diff --staged
   ```

2. If nothing is staged, use the unstaged changes instead, and include untracked files, which `git diff` doesn't show:

   ```bash
   git diff
   ```

   Read any untracked file that `git status` lists. Tell the user the message covers unstaged work.

3. If there are no changes at all, say so and stop.

4. If the changes are unrelated to each other, say so and suggest splitting them, with one message per commit. A commit should do one thing.

5. Write the message and give it to the user in a fenced code block so it's easy to copy.

## Format

```text
<type>(<scope>): <description>

[optional body]

[optional footers]
```

### Type

Use the type that matches the main purpose of the change:

| Type | Use for |
|---|---|
| `feat` | A new feature for the user |
| `fix` | A bug fix |
| `docs` | Documentation only |
| `style` | Formatting with no change in behaviour |
| `refactor` | Code changes that neither fix a bug nor add a feature |
| `perf` | A performance improvement |
| `test` | Adding or correcting tests |
| `build` | Dependencies or the build system |
| `ci` | CI configuration |
| `chore` | Other maintenance that doesn't touch source or tests |
| `revert` | Reverting an earlier commit |

Tests and docs that ship with a feature or fix belong to that `feat` or `fix` commit, not a separate type.

### Scope

The scope is optional. Use it when the change sits clearly in one area, and leave it out when it spans several.

| Scope | Covers |
|---|---|
| `core` | `Weather.Core`, except forecast sources |
| `sources` | `Weather.Core/Sources` |
| `server` | `Weather.Server` |
| `client` | `weather.client` |
| `tests` | `Weather.Core.Tests` |
| `claude` | `.claude` |

Add a new scope only if none of these fit.

### Description

- Use the imperative mood: "add", not "added" or "adds".
- Start with a lowercase letter and don't end with a period.
- Say what changed, not which files changed.
- Keep the whole first line to 72 characters or fewer, and aim for 50.

### Body

Add a body only when the reason for the change isn't obvious from the description. Leave a blank line after the description, wrap lines at 72 characters, and explain why rather than how.

### Footers

Add footers after a blank line, such as `Refs: #123`. Only reference issues the user or the diff mentions; never invent an issue number.

### Breaking changes

Mark a breaking change with `!` before the colon and a `BREAKING CHANGE:` footer that says what callers must do. In this repo, that means a change to the `/api/forecast` response, a removed or renamed setting, or a change to a public `Weather.Core` type.

## Before answering

- Don't put API keys, tokens or other secrets in the message, even if they appear in the diff. If a diff contains one, warn the user before they commit it.
- Don't mention anything that isn't in the diff.

## Examples

A feature:

```text
feat(sources): add Pirate Weather forecast column
```

A fix that needs an explanation:

```text
fix(sources): roll NewsCenter Maine dates into the next year

The page shows dates with no year, so forecasts spanning New Year were
placed eleven months in the past and never matched a table row.
```

A breaking change:

```text
feat(server)!: return temperatures in a nested object

BREAKING CHANGE: `highF` is now `high.fahrenheit` in `/api/forecast`
responses, so API clients must read the new field.
```

Documentation:

```text
docs: add setup steps and settings to README
```
