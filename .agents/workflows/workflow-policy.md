---
workflow: Workflow Engine Policy
version: 1.1
owner: AI Engineering Organization Architect
risk: Dynamic
required_agents: Orchestrator Agent
approval_required: conditional
max_iterations: 3
---
# Workflow Engine Policy

## Workflow Lifecycle
1. Human Intent
2. Orchestrator Risk Assessment
3. Automatic Workflow Selection
4. Relevant Agent Collaboration
5. Implementation
6. Automated Tests
7. Architecture / Security / QA Validation as applicable
8. Autonomous Correction (Max 3 cycles)
9. Final Report
10. STOP

## Mandatory Rules
- **Selection**: Every task starts from Orchestrator assessing risk and intent (natural language routing supported).
- **Approval**: Routine work proceeds automatically without stopping for plan approval. High-risk decisions require human approval.
- **Agent Usage**: Select only relevant agents for the task. Do not mechanically invoke all agents.
- **Documentation**: Minimize governance noise. Prefer one concise final report for routine features instead of plan + task + walkthrough + report.
- **Retry Enforcement**: Failed tasks cannot exceed maximum retry cycles (3). If unresolved, STOP and report BLOCKER.
