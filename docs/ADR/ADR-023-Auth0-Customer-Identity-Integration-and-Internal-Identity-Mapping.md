# ADR-023: Auth0 Customer Identity Integration and Internal Identity Mapping

## Status

Accepted

## Context

In Customer Authentication v1, Auth0 is selected as the Managed Identity Provider (IdP). To maintain architectural integrity and domain purity, the system must bridge external authentication with internal domain identity while upholding the security boundaries established in [ADR-022: Customer Order Ownership Boundary](ADR-022-Customer-Order-Ownership-Boundary.md).

ADR-022 mandates that:
1. External identity is represented by `iss + sub`, where `sub` is treated strictly as an opaque string identifier.
2. The external subject string must NEVER be parsed or coerced into a domain `CustomerId` Guid.
3. Internal domain identity is represented by a strongly-typed `CustomerId` (`Guid`).
4. WebApi consumer endpoints extract `CustomerId` exclusively from the verified claim `urn:enterprisecommerce:customer_id`.

To complete the authentication pipeline safely, a durable, idempotent, and secure mechanism is required to resolve and map an external `(issuer, subject)` pair to a canonical internal `CustomerId` without polluting the Domain layer or trusting client-supplied identity claims.

## Decision

1. **Managed IdP Selection**:
   - Auth0 is established as the authoritative Managed IdP for v1.
   - OAuth 2.0 / OpenID Connect (OIDC) is used as the standard authentication protocol.

2. **External to Internal Identity Mapping & Exact Storage**:
   - The semantic identity mapping key is `(issuer, subject) -> CustomerId`.
   - The mapping is persisted internally within the Infrastructure layer in a dedicated `CustomerIdentities` table.
   - Database uniqueness constraint `UNIQUE (Issuer, Subject)` ensures idempotent provisioning under concurrent first-login requests.
   - `Issuer` (max length 512) and `Subject` (max length 255) explicitly use ASCII storage with binary/case-sensitive collation (`ascii_bin`). This strictly enforces OIDC ASCII semantics while avoiding the 3072-byte key length constraint that would occur under utf8mb4 composite indexing.
   - Application validation enforces that `Subject` is non-empty, max length 255, and strictly ASCII.

3. **No Customer Aggregate for Authentication**:
   - A Customer aggregate/entity is NOT introduced into the Domain layer solely for authentication or identity mapping.
   - Identity mapping is strictly an Infrastructure and Presentation concern, encapsulated behind the Application layer abstraction `ICustomerIdentityStore`.

4. **Internal Identity Resolution Endpoint & M2M Protection**:
   - A dedicated internal endpoint `POST /api/v1/internal/customer-identities/resolve` is exposed for machine-to-machine identity provisioning.
   - The endpoint is protected by JWT Bearer authentication requiring the specific M2M authorization scope `identity:resolve`.
   - The request payload accepts ONLY the external `subject`.
   - The `issuer` is strictly server-controlled and normalized from the server's configured `Authentication:Authority` (`Uri.AbsoluteUri`). Callers cannot supply or override the issuer or customer ID.

5. **Token Claim Enrichment via Auth0 Action**:
   - An Auth0 Post-Login Action will call the internal identity resolver using an authenticated M2M client token.
   - Upon receiving the resolved `CustomerId` (`Guid`), the Action injects the custom namespaced claim `urn:enterprisecommerce:customer_id` into the minted Access Token.
   - Auth0 `app_metadata` or user metadata is NOT the authoritative source for domain identity mapping.

6. **Frontend Token Boundary**:
   - Next.js frontend applications will handle tokens exclusively on the server side (via Next.js Server Components / Route Handlers).
   - Raw access tokens will not be exposed to or stored in Client Components.

## Consequences

* **Security**: Enforces strict M2M authorization on identity provisioning, prevents client-side identity spoofing, and maintains fail-closed behavior across all boundaries.
* **Domain Purity**: Preserves the Domain layer without speculative Customer entities or account abstractions.
* **Idempotency & Concurrency**: Atomic persistence guarantees that concurrent initial logins for the same external identity reliably yield the exact same `CustomerId`. Concurrency recovery strictly isolates MySQL duplicate-key violations (error 1062) and re-verifies exact existence before succeeding.
* **Storage Efficiency**: ASCII column encoding with binary collation guarantees compact, exact-match indexing without exceeding InnoDB index size limits.
* **Relationship to ADR-022**: ADR-022 defines how the application consumes `urn:enterprisecommerce:customer_id` and enforces order ownership; ADR-023 defines how that internal identity is resolved, persisted, and enriched into the token.
