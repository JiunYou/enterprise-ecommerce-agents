# Agent Execution Policy

## Allowed Autonomous Actions
Agents are allowed to perform the following without explicit approval:
- Read files and explore repositories
- Analyze code and configurations
- Run unit and integration tests
- Generate reports and architecture blueprints
- Update documentation and workflow state

## Require Approval
Agents MUST request explicit user approval before performing:
- Architecture changes (modifying ADRs or foundations)
- Domain changes (modifying Aggregates, Value Objects, Domain Events)
- Database migrations or schema modifications
- Production deployments
- Security policy changes
