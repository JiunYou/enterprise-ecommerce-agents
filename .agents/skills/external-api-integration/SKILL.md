---
name: external-api-integration
description: Use when integrating third-party APIs (payments, webhooks), handling retries, idempotency, and timeouts.
---
# External API Integration Skill

## Guidance
- Understand and document the external provider contract.
- Implement proper timeout configurations.
- Use retry logic with exponential backoff.
- Handle idempotency for non-safe operations.
- Validate and securely process external webhooks.
- Gracefully map failure and error responses.
- Maintain trust boundary awareness and API version considerations.

## Boundaries / Validation
Does not duplicate detailed authentication/security content already owned by api-security or ecommerce-security.
