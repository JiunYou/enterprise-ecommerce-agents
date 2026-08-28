# API Layer Blueprint

## 1. API Responsibilities

**API Layer owns:**
- **HTTP Transport:** Receiving HTTP requests and returning appropriate HTTP responses.
- **Routing & Versioning:** Mapping URIs and HTTP methods to appropriate handlers, and managing API versioning (e.g., `/api/v1/orders`).
- **Security Boundary:** Enforcing Authentication (OIDC/JWT), Authorization (RBAC/ABAC), Rate Limiting, and CORS policies.
- **Payload Translation:** Deserializing JSON payloads into MediatR Commands/Queries.
- **Error Formatting:** Catching exceptions and application `Result` failures, mapping them to standard HTTP Status Codes, and formatting them as RFC 7807 Problem Details.
- **Observability Entrypoint:** Generating Correlation IDs, logging request/response metadata without leaking sensitive data or PII.

**API Layer does NOT own:**
- **Business Logic:** No domain rules or invariants.
- **Data Validation:** Core validation is pushed to the Application layer (FluentValidation pipeline).
- **Direct Infrastructure Calls:** The API layer does not interact with the database or external services directly.
- **State Mutation:** All state mutation is handled exclusively via Application Commands.

## 2. API Project/Module Structure

```text
EnterpriseCommerce.WebApi/
├── Controllers/
│   ├── v1/
│   │   ├── OrdersController.cs
│   │   └── InventoryController.cs
├── Middleware/
│   ├── ExceptionHandlingMiddleware.cs
│   └── CorrelationIdMiddleware.cs
├── Extensions/
│   ├── ServiceCollectionExtensions.cs
│   └── ApplicationBuilderExtensions.cs
├── OpenApi/
│   └── ConfigureSwaggerOptions.cs
├── Program.cs
└── appsettings.json
```

## 3. Request Lifecycle

1. **Client Request:** HTTP Request arrives at WebApi.
2. **Security & Pipeline Middleware:**
   - CORS evaluation.
   - Rate Limiting check.
   - Correlation ID generation/attachment.
   - Authentication (JWT Bearer Token validation).
3. **Endpoint Routing & Authorization:**
   - Map to Controller based on path and version.
   - Enforce Authorization Policies (`[Authorize(Policy = "...")]`).
4. **Adapter (Controller):**
   - Model binding (JSON to Command/Query).
   - Dispatch to `ISender` (MediatR).
5. **Application Layer (Pipeline):**
   - Validation (FluentValidation).
   - Command Execution (Domain Mutation + DB Commit).
   - Return `Result<T>`.
6. **Response Mapping:**
   - Map `Result<T>` to HTTP Status Code.
   - Return structured response.
7. **Exception Handling:**
   - If unhandled exception or Failure Result occurs, `IExceptionHandler` intercepts and formats into RFC 7807 Problem Details.

## 4. CQRS Integration Flow

The API strictly implements the adapter pattern for CQRS:
- **POST/PUT/PATCH/DELETE** requests map exclusively to **Commands**. They return 201 Created, 200 OK, or 204 No Content upon success.
- **GET** requests map exclusively to **Queries**. They return 200 OK with DTOs.
- Controllers inject only `ISender` (MediatR), maintaining total ignorance of Application and Domain internals.

## 5. Security Architecture

### Authentication Flow
- Rely on an external OIDC Identity Provider (e.g., Keycloak, Auth0, Google Identity).
- API configured with `AddJwtBearer` to validate signature, issuer, audience, and expiration.
- Stateless authentication—tokens contain the necessary claims.

### Authorization Flow
- **Separation of Concerns:** Authentication verifies *who* the user is; Authorization verifies *what* they can do.
- **RBAC/ABAC:** Implemented using ASP.NET Core Policies. Controllers/endpoints define required policies (e.g., `RequireAdminRole`, `CanCreateOrder`).
- **Least Privilege:** Endpoints demand the minimum necessary scope.

### Security Controls
- **CORS:** Restrict allowed origins to recognized frontends. No `AllowAnyOrigin` in production.
- **Rate Limiting:** IP-based or Client-based rate limiting to prevent DDoS or brute-force attacks.
- **Input Validation Injection Risks:** Application layer FluentValidation prevents SQLi/XSS at the domain boundary.
- **Sensitive Data & Error Leakage:** Exception middleware never exposes stack traces in production. PII is redacted in logging via Serilog Destructuring policies.

## 6. Error-Response Contract

All API errors adhere to the **RFC 7807 Problem Details** standard.
- **Domain Errors:** Mapped to 400 (Bad Request), 404 (Not Found), or 409 (Conflict).
- **Validation Errors:** Mapped to 400 (Bad Request) with a customized Problem Details extension containing the validation property failures.
- **Unauthorized/Forbidden:** 401 (Unauthorized) / 403 (Forbidden).
- **Internal Errors:** 500 (Internal Server Error) with masked details.

## 7. Observability Boundary

- **Request Correlation:** A unique `X-Correlation-ID` is extracted from incoming headers or generated if missing. It is pushed into the logging context (`LogContext.PushProperty`).
- **Health/Readiness:** Expose `/health/live` and `/health/ready` endpoints using ASP.NET Core Health Checks (checking DB connectivity for readiness).
- **Audit Integration:** High-value actions (e.g., `CreateOrder`) are logged at the Application layer, but the API layer adds HTTP-specific context (IP address, User Agent) safely.

## 8. API Versioning

- **Strategy:** URI Path Versioning (e.g., `/api/v1/orders`).
- **Rationale:** Explicit versioning prevents accidental breaking changes for clients. Swagger will display versioned documents distinctively.

## 9. Testing Strategy

- **Integration Testing:** Utilize `WebApplicationFactory` to spin up the API in-memory.
- **Authentication Mocking:** Inject test authentication handlers for integration tests.
- **Scope:** Test endpoint routing, authorization requirements, status code mapping, and Problem Details formatting.
- **Exclusion:** Do not re-test deep business logic inside API tests; trust the Application/Domain layer unit tests.

## 10. Implementation Sequence

1. Bootstrap ASP.NET Core Web API project and configure dependencies (`DependencyInjection.cs`).
2. Implement Global Exception Handling (RFC 7807 Problem Details).
3. Configure JWT Authentication, Authorization Policies, and CORS.
4. Implement API Versioning and Swagger/OpenAPI setup.
5. Create BaseApiController and initial feature Controllers (e.g., `OrdersController`).
6. Configure Logging (Correlation ID, Health Checks).
7. Write API Integration Tests.

## 11. Acceptance Criteria

- API project strictly references Application, not Infrastructure or Domain (enforced by NetArchTest).
- All failed MediatR commands return valid RFC 7807 JSON.
- Swagger UI is fully operational and correctly documents versions.
- Endpoints without valid JWT tokens correctly return 401 Unauthorized.

## 12. Known Risks

| Risk | Mitigation |
|------|------------|
| Logic Leakage | Strict code reviews and architecture tests to ensure Controllers remain extremely thin (under ~10 lines per action). |
| Token Validation Overhead | Use token caching or strict local signature validation via JWKS endpoint caching. |
| Excessive API surface | Only expose endpoints for Commands/Queries that are actually required by the frontend/consumers. |
