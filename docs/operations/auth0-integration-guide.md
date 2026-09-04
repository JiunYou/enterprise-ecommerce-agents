# Auth0 Integration & Operational Configuration Guide

This document outlines the operational configuration contract and deployment requirements for **Customer Authentication v1** across Auth0, Next.js Customer Web, and the EnterpriseCommerce backend.

---

## 1. Auth0 Application Configurations

### 1.1 Customer Web (Regular Web Application)
- **Application Type**: Regular Web Application
- **Token Endpoint Auth Method**: `Basic` / `Post` (Client Secret) or `None` with PKCE depending on environment.
- **Allowed Callback URLs**: `<APP_BASE_URL>/auth/callback` (e.g., `http://localhost:3000/auth/callback` for dev).
- **Allowed Logout URLs**: `<APP_BASE_URL>` (e.g., `http://localhost:3000` for dev).
- **Allowed Web Origins**: `<APP_BASE_URL>`
- **API Authorization**:
  - Authorize for the EnterpriseCommerce API.
  - **Do NOT grant** the `identity:resolve` scope to Customer Web.
- **Refresh Token Behavior**:
  - Enable Refresh Token Rotation and offline access to support Next.js server-side session rolling.

---

## 2. Auth0 API Configuration

### 2.1 EnterpriseCommerce API (Resource Server)
- **Identifier / Audience**: Must match `Authentication:Audience` (Backend) and `AUTH0_AUDIENCE` (Frontend).
- **Signing Algorithm**: RS256
- **RBAC Settings**: Enable RBAC and include permissions in access tokens if applicable.
- **Permissions (Scopes)**:
  - `identity:resolve`: Machine-to-Machine permission for resolving customer identity.

---

## 3. M2M Identity Resolver Client

### 3.1 Machine-to-Machine Application
- **Application Type**: Machine to Machine
- **API Authorization**: EnterpriseCommerce API
- **Authorized Scopes**: `identity:resolve` ONLY.
- **Configuration Bindings**:
  - `Client ID` -> Configured in backend as `Authentication:IdentityResolverClientId` (env: `Authentication__IdentityResolverClientId`).
  - `Client Secret` -> Stored exclusively as an Auth0 Action Secret (`M2M_CLIENT_SECRET`).

---

## 4. Auth0 Post-Login Action

### 4.1 Action Details
- **Trigger**: Post-Login (`Login / Post-Login`)
- **Runtime**: Node 22 (using built-in global `fetch`)
- **Source Code**: [on-execute-post-login.js](../../infrastructure/auth0/actions/customer-identity-claim/on-execute-post-login.js)

### 4.2 Required Action Secrets (Configuration Names Only)
| Secret Name | Description |
|---|---|
| `CUSTOMER_WEB_CLIENT_ID` | Client ID of the Customer Web application |
| `API_AUDIENCE` | Identifier of the EnterpriseCommerce API |
| `AUTH0_TOKEN_URL` | Auth0 token endpoint (`https://<tenant-domain>/oauth/token`) |
| `M2M_CLIENT_ID` | Client ID of the M2M Identity Resolver client |
| `M2M_CLIENT_SECRET` | Client Secret of the M2M Identity Resolver client |
| `IDENTITY_RESOLVER_URL` | Backend HTTPS identity resolution endpoint (`https://<api-host>/api/v1/internal/customer-identities/resolve`) |

---

## 5. Token Claim Contract

Upon successful login and identity resolution:
- **Claim Name**: `urn:enterprisecommerce:customer_id`
- **Location**: Access Token ONLY (`api.accessToken.setCustomClaim`)
- **Format**: Textual Guid string (`^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$`)
- **Fail-Closed**: If resolution fails, the Action denies login to prevent issuance of unmapped tokens.
