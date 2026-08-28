---
workflow: Feature Development
version: 1.2
owner: Orchestrator Agent
risk: Dynamic
required_agents: [Orchestrator determines based on risk]
approval_required: conditional
max_iterations: 3
---
# Feature Development Workflow

## Purpose
Risk-based feature development. Routine engineering executes autonomously. High-risk features require human approval.

## Flow
1. Orchestrator Risk Assessment
2. Relevant Agent Delegation
3. Implementation (Autonomous for routine work)
4. Automated Tests
5. Validation (Architecture, Security, QA as applicable)
6. Autonomous Correction (Max 3 cycles)
7. Final Report
8. STOP

## Restrictions
- High-risk changes (Architecture, Aggregate behavior / lifecycle state transitions / Domain invariants, Security / Privileged authorization policies, Schema migrations, Public contracts) MUST stop for human approval.
  - **CRITICAL ENFORCEMENT**: If a High-Risk Approval Gate is triggered and explicit human approval has not been received, execution must STOP immediately. You must not edit production code, create migrations, modify Domain models, continue implementation, or silently treat the remaining work as Routine. Only a decision proposal/report may be produced until human approval is received.
- Routine changes (existing patterns, read queries, use cases on existing aggregate methods, bug fixes, lint cleanups) execute autonomously.
- Do not generate implementation_plan, task, or walkthrough artifacts for routine features unless they provide durable value.
- Max 3 correction cycles. If unresolved, STOP with BLOCKER.
