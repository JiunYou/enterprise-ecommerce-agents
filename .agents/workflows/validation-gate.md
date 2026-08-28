---
workflow: Validation Gate
version: 1.0
owner: Architecture Validation Agent
risk: High
required_agents: [Architecture Validation Agent, Security Agent, QA Agent]
approval_required: true
max_iterations: 3
---
# Validation Gate Workflow

## Purpose
Every major phase must pass validation before the next phase can begin.

## Flow
1. Implementation Complete
2. Architecture Validation Agent (Verify design adherence and constraints)
3. Security Agent (Verify security requirements)
4. QA Agent (Verify test coverage and execution)
5. Final Report (Summarize validation findings)
6. STOP
