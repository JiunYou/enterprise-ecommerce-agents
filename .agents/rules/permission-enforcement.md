# Permission Enforcement Rule

## JIT Enforcement Policy
Task starts -> AGENTS.md + routing metadata -> select primary Agent -> before write/execute action, consult relevant Agent permission row -> if high-risk, consult approval-gates.md -> load additional governance only when task characteristics require it

## Canonical Matrix
The canonical least-privilege matrix is located at:
`.agents/permissions/agent-permission-matrix.md`

## Invariants
Global invariants are summarized by AGENTS.md and do not require reloading large governance contexts:
- security by default
- no gate bypass
- smallest safe change
- max 3 correction cycles
- evidence over claims
