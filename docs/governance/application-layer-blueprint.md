# Application Layer Blueprint

## 1. Application Layer Responsibility

**Application Layer owns:**
- **Use Case orchestration:** Coordinating the steps required to fulfill a specific business request.
- **Transaction coordination:** Ensuring atomicity by managing the Unit of Work around aggregate changes.
- **Authorization checks:** Validating that the current user/identity is permitted to execute the use case.
- **Domain invocation:** Loading domain objects, invoking their behaviors, and persisting changes.
- **Domain Event dispatch coordination:** Triggering the evaluation and subsequent integration publishing of domain events generated during the transaction.

**Application Layer does NOT own:**
- **Business rules:** All core invariants and rules live in the Domain Layer.
- **Entity state mutation logic:** Mutation happens strictly through Domain Aggregate root methods.
- **Database implementation:** Repositories are defined here as interfaces but implemented in Infrastructure.
- **Message broker implementation:** Event publishing details belong to Infrastructure.

## 2. Use Case Architecture

The Application layer is organized by vertical feature slices (Use Cases) aligned with our core domains. We will adopt a Command Query Responsibility Segregation (CQRS) directory structure.

```text
EnterpriseCommerce.Application/
├── Orders/
│   ├── Commands/
│   │   └── CreateOrder/
│   │       ├── CreateOrderCommand.cs
│   │       ├── CreateOrderCommandHandler.cs
│   │       └── CreateOrderCommandValidator.cs
│   ├── Queries/
│   │   └── GetOrderById/
│   │       ├── GetOrderByIdQuery.cs
│   │       └── GetOrderByIdQueryHandler.cs
│   └── Dtos/
├── Inventory/
│   ├── Commands/
│   ├── Queries/
│   └── Dtos/
└── Common/
    ├── Behaviors/
    │   ├── ValidationBehavior.cs
    │   ├── TransactionBehavior.cs
    │   └── AuthorizationBehavior.cs
    ├── Interfaces/
    │   ├── IUnitOfWork.cs
    │   ├── IOrderRepository.cs
    │   └── IInventoryRepository.cs
    └── Exceptions/
```

## 3. Command Query Strategy

We adopt **CQRS using MediatR** to strictly segregate read and write operations.

- **Command responsibilities:**
  - Intent to change system state.
  - Handled by exactly one handler.
  - Loads an Aggregate, invokes behavior, saves state, and commits transaction.
  - Triggers Domain Events collection and dispatch.

- **Query responsibilities:**
  - Read-only operations.
  - Avoids modifying Domain state completely.
  - Bypasses rich Domain models if possible, projecting directly from the database to DTOs for performance.

**Decision:** CQRS allows us to apply specific cross-cutting concerns (e.g., transactions to Commands only) and optimize read/write paths independently as the platform scales.

## 4. Transaction Boundary Design

Transactions are scoped to a single Use Case (Command).

**Execution Flow:**
1. Request arrives (Command).
2. Application Use Case begins.
3. **Transaction Begins** (Managed by `IUnitOfWork` via `TransactionBehavior` pipeline).
4. `IRepository` retrieves Domain Aggregate.
5. Application invokes Aggregate method.
6. `IRepository` saves Aggregate state.
7. **Transaction Commits** (Unit of Work saves changes to DB).
8. **Domain Events Dispatched** (Events collected from Aggregate are dispatched locally/externally).

*Note: Infrastructure will implement the actual `DbContext` and EF Core transaction.*

## 5. Domain Event Flow Design

**Current Setup:**
Domain Event -> Application Dispatcher -> Integration Event Mapper -> Infrastructure Publisher

**Future Responsibilities:**
- **Domain:** Responsible solely for defining and registering `DomainEvent` instances within Aggregates during state mutation.
- **Application:** Iterates through collected Domain Events before/after `SaveChanges`. Uses MediatR `IPublisher` to dispatch events to local handlers (In-Process) and utilizes `IntegrationEventMapper` to translate domain events into `EventEnvelope` for out-of-process messaging.
- **Infrastructure:** Serializes `EventEnvelope` and publishes to external message broker (e.g., RabbitMQ).

## 6. Authorization Boundary

Security is enforced at the Use Case entry point, prior to invoking domain logic.

**Flow:**
API -> Authentication (Filter) -> Application Use Case (AuthorizationBehavior) -> Domain

**Plan:**
- Implement `IAuthorizationService` or MediatR `IPipelineBehavior` to perform Role-Based Access Control (RBAC) or Resource-Based checks.
- Handlers define required permissions (e.g., `[Authorize(Policy = "ManageOrders")]`).
- Application layer handles access denied by throwing `UnauthorizedAccessException` before querying the database.

## 7. Validation Strategy

Validation logic is categorized by responsibility:

- **Unit Testing:**
  - **Handler Behavior:** Mock `IRepository` and verify handler logic orchestrates correctly (calls repository, commits).
  - **Use Case Orchestration:** Ensure validators and authorization rules trigger appropriately.
- **Integration Testing:**
  - **Database Interaction:** Real database tests executing commands and verifying persistence via Repositories.
- **Architecture Testing:**
  - **Dependency Validation:** NetArchTest to enforce Application does not reference WebApi or Infrastructure.

*Note: Command input validation (e.g., FluentValidation) happens in Application Layer. Domain invariants are validated inside the Domain Layer.*

## 8. Application Layer Risks & Mitigation

| Risk | Description | Mitigation |
|------|-------------|------------|
| Application becoming business logic container | Handlers contain logic like `if (stock > 0)` instead of the domain. | Strict code reviews forcing mutation logic into Aggregates. Handlers must act strictly as orchestrators. |
| Over-engineered CQRS | Complex plumbing for simple CRUD operations. | Use simple queries without complex read models initially. Stick to straightforward MediatR handlers. |
| Transaction leakage | Transactions spanning multiple aggregates or failing to rollback correctly. | Use `IUnitOfWork` pattern explicitly. Enforce single aggregate mutation per transaction rule (where possible). |
| Event coupling | Too many use cases chained locally via Domain Events leading to spaghetti code. | Prefer out-of-process Integration Events for cross-domain communication instead of local synchronous event handlers. |
