# ADR-022: Customer Order Ownership Boundary

## Status

Accepted

## Context

To ensure the security and privacy of customer data within the Enterprise E-Commerce platform, a robust boundary must be established around Order ownership. The system must guarantee that authenticated customers can only view, modify, and submit their own orders. Without this boundary, horizontal privilege escalation (IDOR - Insecure Direct Object Reference) could occur, allowing a malicious actor to manipulate orders belonging to other customers simply by guessing or iterating through `OrderId` values.

This security boundary is a prerequisite before frontend development for Customer Cart and Order management can safely proceed.

## Decision

1. **Identity Resolution**: We extract the customer identity exclusively from the `urn:enterprisecommerce:customer_id` JWT claim in the `ApiControllerBase`. If this claim is missing, empty, or cannot be parsed as a valid `Guid`, the system will "fail closed" by immediately returning a `403 Forbidden` response.
2. **Application Layer Enforcement**: The ownership validation must occur in the Application layer (e.g., inside MediatR Command/Query Handlers), not just the API Controller layer. The `CustomerId` is passed explicitly from the Controller into the Command/Query constructors.
3. **Fail Closed & Anti-Enumeration**: If an order exists but the requesting `CustomerId` does not match the order's `CustomerId`, the application must return an `OrderErrors.NotFound` error (yielding a `404 Not Found` response). This prevents resource enumeration attacks, as an attacker cannot distinguish between a non-existent order and an order belonging to someone else.
4. **Command/Query Updates**: All customer-facing order operations (`GetOrderByIdQuery`, `AddOrderItemCommand`, `RemoveOrderItemCommand`, `SubmitOrderCommand`, `CancelOrderCommand`) have been updated to require `CustomerId`.

## Consequences

* **Security**: Strong mitigation against IDOR and resource enumeration vulnerabilities on customer orders.
* **Architecture**: The Application layer now acts as the authoritative enforcer of business security rules regarding ownership, rather than relying solely on the Web API layer.
* **Testing**: Unit and integration tests must now explicitly mock or provide the appropriate `CustomerId` to execute successfully. This requires slightly more setup in test fixtures.
* **Admin Endpoints**: Existing Admin-only endpoints (e.g., `PayOrder`, `ShipOrder`) remain untouched as they rely on Role-based access (`[Authorize(Roles = "Admin")]`) rather than specific customer ownership.
