# Copilot instructions

This repository keeps its AI assistant guidance in the `.claude` folder. Treat those files as the source of truth, and don't copy their contents here, so there is only one version to maintain.

## Project guidance

Before making changes, read [.claude/CLAUDE.md](../.claude/CLAUDE.md). It covers the project layout, build and test commands, code rules, how to add a forecast source, and each source's quirks. Follow it as if its rules were written in this file.

## Skills

Skills are task instructions stored in `.claude/skills/<skill-name>/SKILL.md`. The `description` at the top of each file says when it applies.

Before starting a task, check whether a skill in `.claude/skills` covers it. If one does, read its `SKILL.md` and follow those steps instead of your default approach.

Current skills:

- [commit-message](../.claude/skills/commit-message/SKILL.md): write a commit message using the Conventional Commits standard.

New skills added to `.claude/skills` apply even if they aren't listed here.
