---
workflow: Bug Fix
version: 1.1
owner: Orchestrator Agent
risk: Medium
required_agents: [Relevant Developer Agent, QA Agent, Security Agent, Validation Agent]
approval_required: false
max_iterations: 3
---
# Bug Fix Workflow

## Purpose
Controlled bug fixing with mandatory limits.

## Flow
1. Issue
2. Diagnosis
3. Root Cause Analysis
4. Fix Plan
5. Implementation
6. Testing
7. Security Check
8. Completion

## Restrictions
- **Maximum Attempts:** 3
- After three failed attempts: **STOP**
- Must generate:
  - Error summary
  - Root cause hypothesis
  - Recommended human action
