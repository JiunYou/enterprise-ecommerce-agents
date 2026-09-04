/**
 * Auth0 Post-Login Action: Customer Identity Claim Enrichment
 *
 * Flow & Security Rules:
 * 1. Filter: Executes only for the targeted Customer Web client (event.client.client_id === secrets.CUSTOMER_WEB_CLIENT_ID).
 *    Ignores other clients cleanly without altering token.
 * 2. Filter: Ensures the login transaction targets the expected EnterpriseCommerce API (event.resource_server.identifier === secrets.API_AUDIENCE).
 *    Denies access if a Customer Web login targets an unexpected audience.
 * 3. Validate user_id: Must be a non-empty ASCII string up to 255 characters.
 * 4. M2M Token Exchange: Requests client_credentials token from secrets.AUTH0_TOKEN_URL with scope "identity:resolve".
 * 5. Resolve CustomerId: POST to secrets.IDENTITY_RESOLVER_URL with { subject: event.user.user_id }.
 * 6. Validate CustomerId: Must be a valid non-empty, non-zero GUID.
 * 7. Inject Claim: Sets "urn:enterprisecommerce:customer_id" on access token ONLY.
 * 8. Fail-Closed: On any failure, denies login without exposing secrets, tokens, or PII.
 */

const GUID_REGEX = /^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/;
const EMPTY_GUID = "00000000-0000-0000-0000-000000000000";
const CLAIM_NAME = "urn:enterprisecommerce:customer_id";

function isAscii(str) {
  for (let i = 0; i < str.length; i++) {
    if (str.charCodeAt(i) > 127) {
      return false;
    }
  }
  return true;
}

/**
 * Handler that will be called during the execution of a PostLogin flow.
 *
 * @param {object} event - Details about the user and the context in which they are logging in.
 * @param {object} api - Interface whose methods can be used to change the behavior of the login.
 */
exports.onExecutePostLogin = async (event, api) => {
  try {
    const secrets = event.secrets || {};
    const expectedWebClientId = secrets.CUSTOMER_WEB_CLIENT_ID;
    const expectedApiAudience = secrets.API_AUDIENCE;
    const auth0TokenUrl = secrets.AUTH0_TOKEN_URL;
    const m2mClientId = secrets.M2M_CLIENT_ID;
    const m2mClientSecret = secrets.M2M_CLIENT_SECRET;
    const identityResolverUrl = secrets.IDENTITY_RESOLVER_URL;

    // 1. Ignore unrelated clients
    if (event.client && event.client.client_id !== expectedWebClientId) {
      return;
    }

    // 2. Validate API Audience
    const requestedAudience = event.resource_server ? event.resource_server.identifier : null;
    if (!requestedAudience || requestedAudience !== expectedApiAudience) {
      api.access.deny("Access denied: Invalid API audience.");
      return;
    }

    // 3. Validate user subject
    const subject = event.user ? event.user.user_id : null;
    if (!subject || typeof subject !== "string" || subject.trim().length === 0 || subject.length > 255 || !isAscii(subject)) {
      api.access.deny("Access denied: Invalid user subject identifier.");
      return;
    }

    // 4. Request M2M Access Token from Auth0
    const tokenResponse = await fetch(auth0TokenUrl, {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
      },
      body: JSON.stringify({
        grant_type: "client_credentials",
        client_id: m2mClientId,
        client_secret: m2mClientSecret,
        audience: expectedApiAudience,
        scope: "identity:resolve",
      }),
    });

    if (!tokenResponse.ok) {
      console.log(`M2M_TOKEN_REQUEST_FAILED status=${tokenResponse.status}`);
      api.access.deny("Access denied: Identity resolution authorization failed.");
      return;
    }

    const tokenData = await tokenResponse.json();
    const m2mAccessToken = tokenData.access_token;
    if (!m2mAccessToken || typeof m2mAccessToken !== "string") {
      console.log("M2M_TOKEN_INVALID_RESPONSE");
      api.access.deny("Access denied: Invalid identity resolution authorization token.");
      return;
    }

    // 5. Call Backend Identity Resolver Endpoint
    const resolverResponse = await fetch(identityResolverUrl, {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        "Authorization": `Bearer ${m2mAccessToken}`,
      },
      body: JSON.stringify({
        subject: subject,
      }),
    });

    if (!resolverResponse.ok) {
      console.log(`RESOLVER_REQUEST_FAILED status=${resolverResponse.status}`);
      api.access.deny("Access denied: Identity resolution request failed.");
      return;
    }

    const resolverData = await resolverResponse.json();
    const customerId = resolverData ? resolverData.customerId : null;

    // 6. Validate CustomerId Guid
    if (
      !customerId ||
      typeof customerId !== "string" ||
      !GUID_REGEX.test(customerId) ||
      customerId === EMPTY_GUID
    ) {
      console.log("RESOLVER_INVALID_RESPONSE");
      api.access.deny("Access denied: Identity resolution returned an invalid customer ID.");
      return;
    }

    // 7. Inject custom claim into Access Token ONLY
    if (!api.accessToken || typeof api.accessToken.setCustomClaim !== "function") {
      console.log("CUSTOM_CLAIM_INJECTION_FAILED");
      api.access.deny("Access denied: Access token claim injection unsupported.");
      return;
    }

    api.accessToken.setCustomClaim(CLAIM_NAME, customerId);
  } catch (error) {
    // Fail closed without exposing internal errors, tokens or secrets
    const errorClass = error && error.name ? error.name : "UnknownError";
    console.log(`CUSTOMER_IDENTITY_ACTION_EXCEPTION error_class=${errorClass}`);
    if (api && api.access && typeof api.access.deny === "function") {
      api.access.deny("Access denied: Unable to complete customer identity resolution.");
    }
  }
};
