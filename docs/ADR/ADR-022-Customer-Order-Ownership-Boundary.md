# ADR-022: Customer Order Ownership Boundary

## Status

Accepted

## Context

To ensure the security and privacy of customer data within the Enterprise E-Commerce platform, a robust boundary must be established around Order ownership. The system must guarantee that authenticated customers can only view, modify, and submit their own orders. Without this boundary, horizontal privilege escalation (IDOR - Insecure Direct Object Reference) could occur, allowing a malicious actor to manipulate orders belonging to other customers simply by guessing or iterating through `OrderId` values.

Authentication is delegated to a managed Identity Provider (IdP). In OpenID Connect (OIDC), the external identity is identified by `iss + sub`, where the subject claim (`sub` / `ClaimTypes.NameIdentifier`) is treated as an opaque external identifier. This external subject is strictly NOT the Domain `CustomerId`. The internal Domain `CustomerId` remains a strongly-typed `Guid`.

This security boundary is a prerequisite before frontend development for Customer Cart and Order management can safely proceed.

## Decision

1. **Authentication & External Identity Delegation**:
   - Authentication is delegated to a managed external Identity Provider.
   - External identity is defined by `iss + sub`, where the `sub` (subject) is an opaque string identifier.
   - The external identity (`sub`) is NOT the Domain `CustomerId`, and the system must never attempt to parse the external subject string directly into a domain `CustomerId` Guid.

2. **Internal Identity Resolution & Claim Propagation**:
   - The WebApi receives and resolves internal customer identity exclusively through the custom claim `urn:enterprisecommerce:customer_id` (defined centrally as `CustomerClaimTypes.CustomerId`).
   - If the `urn:enterprisecommerce:customer_id` claim is missing, malformed (not a valid Guid), or evaluates to `Guid.Empty`, the WebApi fails closed immediately and returns `403 Forbidden`.

3. **Client Request Contracts**:
   - Customer-facing request payloads, such as `CreateOrderRequest`, do NOT accept `CustomerId` from the client request body or query string.
   - The WebApi controller extracts the authenticated `CustomerId` from the verified token claim and explicitly passes it into Application layer commands and queries (`CreateOrderCommand`, `GetOrderByIdQuery`, `AddOrderItemCommand`, `RemoveOrderItemCommand`, `CancelOrderCommand`, `SubmitOrderCommand`, `InitiatePaymentCommand`).

4. **Authoritative Application Layer Ownership Enforcement**:
   - The Application layer (MediatR Command and Query Handlers) acts as the authoritative ownership enforcement boundary.
   - Handlers verify that the target `Order.CustomerId` matches the `request.CustomerId`.

5. **Fail-Closed & Anti-Enumeration (IDOR Prevention)**:
   - When an order does not exist or when `order.CustomerId != request.CustomerId`, the application handler returns identical failure: `OrderErrors.NotFound` (resulting in `404 Not Found`).
   - This prevents resource enumeration and horizontal IDOR disclosure by ensuring an attacker cannot infer the existence of another customer's order.
   - Any state mutations (such as querying products, reserving inventory, or saving changes) are aborted immediately before side-effects occur.

6. **Scope Boundaries**:
   - IdP tenant configuration, Auth0 Actions / rule hooks, user provisioning workflows, external-subject to internal-CustomerId mapping persistence, login UI, token acquisition, and customer registration flows are explicitly **OUT OF SCOPE** for this implementation slice.

## Consequences

* **Security**: Comprehensive protection against horizontal IDOR, tampering, and resource enumeration on customer orders and payment initiation.
* **Architecture**: Clean separation between opaque external authentication (`iss + sub`) and internal domain identity (`CustomerId: Guid`), with authoritative business rule enforcement at the Application layer.
* **Contract Integrity**: API contracts prevent client-side identity spoofing by sourcing customer identity solely from verified claims.
* **Testing**: Integration tests simulate the IdP token with `urn:enterprisecommerce:customer_id` claims, and unit tests verify cross-customer boundary isolation without side-effects.
* **Admin Operations**: Administrative endpoints (e.g., `PayOrder`, `ShipOrder`) remain role-based (`[Authorize(Roles = "Admin")]`) and do not perform customer ownership checks.
