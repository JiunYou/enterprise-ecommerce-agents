# Enterprise E-Commerce Agents Architecture

This repository uses a progressively disclosed AI engineering architecture that works with Google Antigravity/Gemini and OpenAI Codex.

## Core Principles
1. **Project Identity**: This is an enterprise e-commerce platform.
2. **Architecture**: Preserve existing architecture and accepted/enforced governance.
3. **Action Hierarchy**: Reuse → Configure → Extend → Refactor → Create.
4. **Change Scope**: Always make the smallest safe change.
5. **Simplicity**: KISS / YAGNI. No speculative abstractions.
6. **Security**: Security by default. Never expose secrets, tokens, passwords, connection strings, private keys, or PII.
7. **Compliance**: Do not bypass authorization or approval gates.
8. **Evidence**: Programmatic verification first. Evidence over AI completion claims. Machine-verifiable acceptance criteria require executable evidence (see .agents/rules/execution-boundary.md).
9. **Execution**: Maximum 3 correction cycles. Stop after acceptance criteria pass.
10. **Roles**: One primary implementation Agent by default. Reviewers are conditional, not automatically all invoked.
11. **Git Integration**: Before branch, rebase, push, PR, or merge operations, verify worktree state, current HEAD/upstream, merge-base/divergence, and existing PR/merge state. Never perform Git integration based on assumed branch state.

## Routing and Memory
- **Agents**: Use `.agents/routing/agents.json`.
- **Skills**: Use `.agents/routing/skills.json`.
- **Memory**: Use `.agents/memory/catalog.json` for JIT memory lookup. Never preload the whole memory directory or all Skills.
- **Search**: Use repository search/grep before broad file loading.
- **Transience**: Transient task traces are not durable project memory. Large logs/tool results must be summarized or filtered when possible.

## Files
- `.agents/agents/`: On-demand role definitions.
- `.agents/skills/`: Progressive-disclosure capabilities.
- `.agents/rules/`: Mandatory/conditional constraints.
- `.agents/workflows/`: Reusable execution sequences.
- `.agents/permissions/`: Least-privilege boundaries.
