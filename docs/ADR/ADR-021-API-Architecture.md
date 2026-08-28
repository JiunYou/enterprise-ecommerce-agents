# ADR-021: API Layer Architecture

## Status
Proposed

## Context
Following the completion of the Application Layer (CQRS + MediatR) and the Infrastructure Layer (EF Core + Outbox), the system requires an API Layer (Presentation) to expose these capabilities to external clients (e.g., web frontend, mobile apps, integrations). This layer must strictly adhere to Clean Architecture boundaries, depending solely on the Application Layer.

## Decision

We will implement the API Layer using ASP.NET Core Web API with the following architectural patterns and security standards:

### 1. Presentation Boundary
- **Role**: Thin adapter layer.
- **Dependency Rule**: The API project (`EnterpriseCommerce.WebApi`) will reference **only** `EnterpriseCommerce.Application`. It will reference `EnterpriseCommerce.Infrastructure` strictly for Dependency Injection wire-up during `Program.cs` startup, but no API code (Controllers) will directly use Infrastructure types.

### 2. Request Handling (CQRS & MediatR)
- **Controller/Endpoint Pattern**: Controllers will be kept extremely thin. Their sole responsibility is to receive HTTP requests, map them to MediatR Commands/Queries, and dispatch them via `ISender`.
- **Validation**: Will not be performed in the API layer. The API relies entirely on the Application layer's `ValidationBehavior` pipeline (FluentValidation). 

### 3. Error Handling Contract (RFC 7807)
- The API will use ASP.NET Core's Global Exception Handling (`IExceptionHandler`).
- Domain `Result` objects returned by MediatR will be uniformly mapped to standard HTTP status codes:
  - `Result.Success` -> 200 OK / 201 Created / 204 No Content
  - `Result.Failure` -> 400 Bad Request / 404 Not Found / 409 Conflict (depending on the `Error` type).
- All error responses will strictly follow the **RFC 7807 Problem Details** format.

### 4. API Security Boundary
- **Authentication**: JWT Bearer tokens will be used. The API will validate tokens issued by the central Identity Provider (IdP).
- **Authorization**: Policy-based authorization will be enforced at the endpoint level (`[Authorize]`).
- **Rate Limiting**: ASP.NET Core rate limiting middleware will be configured to prevent abuse.
- **CORS**: Strict Cross-Origin Resource Sharing policies will be applied, allowing only authorized client origins.

### 5. Versioning Strategy
- **URI Path Versioning**: APIs will be versioned within the URI (e.g., `/api/v1/orders`).
- This ensures explicit backward compatibility and clear contract boundaries for external consumers.

### 6. OpenAPI Documentation
- **Swagger/OpenAPI 3.0+**: Configured via Swashbuckle or NSwag to provide interactive API documentation, ensuring that all models, security requirements, and HTTP responses are accurately described.

## Consequences

**Positive:**
- Complete decoupling of HTTP concerns from business logic.
- Standardized error formats improve client developer experience.
- Strict security boundaries are enforced before requests reach the Application layer.

**Negative:**
- Minor boilerplate required to map HTTP requests to MediatR commands.

## Compliance
- Aligns with `.agents/skills/rest-api-design/SKILL.md` (RFC 7807, OpenAPI, Versioning).
- Aligns with `.agents/skills/api-security/SKILL.md` (JWT, Rate Limiting, Input Validation).
