# Approval Gates

## Automatic Execution Allowed (Routine Engineering)
- Existing-pattern API features and read queries within approved domain models
- Application use cases implementing existing approved aggregate methods
- Unit and integration tests
- Bug fixes
- Non-breaking refactors and code quality/lint cleanups
- Documentation updates
- Repetitive mappings and boilerplate
- Implementation of already-approved ADR decisions

## Human Approval Required (High-Risk Decisions)
- Architecture changes
- Aggregate behavior changes, new domain invariants, lifecycle state transitions, or domain event semantics
- Bounded context modifications or cross-aggregate coordination
- Security, privileged operations, or authorization policy changes (e.g., payment confirmation, fulfillment, RBAC)
- Database schema migrations or destructive data operations
- Breaking public contracts
- Production deployment
- Irreversible external actions
- Unresolved ambiguity with significant architectural impact

