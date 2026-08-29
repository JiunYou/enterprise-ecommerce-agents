---
name: git-workflow
description: Use when executing Git operations, branching strategies, and repository management.
---
# Git Workflow Skill

## Preflight
Verify worktree state before branch, rebase, push, PR, or merge:
- `git status --short` (clean worktree)
- `git fetch --prune origin`
- Current branch, HEAD SHA, upstream tracking
- `origin/main` SHA and `git merge-base HEAD origin/main`
- Ahead/behind divergence counts (`git rev-list --left-right --count origin/main...HEAD`)
- Existing PR state (via `gh pr view` or API)

## Topology Classification
- **ahead-only**: Normal PR candidate ready for integration.
- **behind-only**: Stale branch; update or rebase before integration.
- **0/0**: Already synchronized with upstream.
- **diverged**: Do not blindly merge; analyze divergence cause.
- **post-merge new commits on old branch**: Create a new integration branch from current `origin/main` and cherry-pick necessary commits.

## Pre-Merge Gate
Before merging any PR or integrating changes, verify:
- Target base branch is correct (e.g. `main`)
- Expected head commit SHA matches PR head
- Changed-file scope adheres to boundary rules
- Test and validation evidence passed
- No unexpected or unreviewed commits included
- Clean worktree without stray untracked/dirty files
- PR approval and CI state satisfied

## Post-Merge Verification
After PR merge or integration completes, verify:
- New `origin/main` HEAD commit SHA
- Expected commit ancestry and merge result
- Remaining branch-only commits (ensure none left behind unintended)
- Confirm whether branch deletion is safe
