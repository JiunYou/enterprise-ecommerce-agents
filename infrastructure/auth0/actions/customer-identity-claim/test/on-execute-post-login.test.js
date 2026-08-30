const { describe, it, beforeEach, afterEach } = require("node:test");
const assert = require("node:assert/strict");
const { onExecutePostLogin } = require("../on-execute-post-login");

describe("Auth0 Post-Login Action: customer-identity-claim", () => {
  const defaultSecrets = {
    CUSTOMER_WEB_CLIENT_ID: "customer-web-client-id",
    API_AUDIENCE: "https://api.enterprisecommerce.test",
    AUTH0_TOKEN_URL: "https://tenant.auth0.com/oauth/token",
    M2M_CLIENT_ID: "m2m-identity-resolver-id",
    M2M_CLIENT_SECRET: "m2m-identity-resolver-secret",
    IDENTITY_RESOLVER_URL: "https://api.enterprisecommerce.test/api/v1/internal/customer-identities/resolve",
  };

  const validSubject = "auth0|64bf9b1c4f923b";
  const validCustomerId = "d3b07384-d113-4a11-8e0f-90fc36be98fe";

  let originalFetch;
  let fetchCalls;

  function createMockApi() {
    return {
      access: {
        deniedReason: null,
        deny(reason) {
          this.deniedReason = reason;
        },
      },
      accessToken: {
        customClaims: {},
        setCustomClaim(name, value) {
          this.customClaims[name] = value;
        },
      },
      idToken: {
        customClaims: {},
        setCustomClaim(name, value) {
          this.customClaims[name] = value;
        },
      },
    };
  }

  beforeEach(() => {
    fetchCalls = [];
    originalFetch = global.fetch;
  });

  afterEach(() => {
    global.fetch = originalFetch;
  });

  it("should ignore unrelated clients cleanly without denying or injecting claims", async () => {
    const event = {
      client: { client_id: "other-client-id" },
      resource_server: { identifier: defaultSecrets.API_AUDIENCE },
      user: { user_id: validSubject },
      secrets: defaultSecrets,
    };
    const api = createMockApi();

    await onExecutePostLogin(event, api);

    assert.equal(api.access.deniedReason, null);
    assert.deepEqual(api.accessToken.customClaims, {});
    assert.equal(fetchCalls.length, 0);
  });

  it("should deny access if Customer Web login targets an invalid API audience", async () => {
    const event = {
      client: { client_id: defaultSecrets.CUSTOMER_WEB_CLIENT_ID },
      resource_server: { identifier: "https://wrong.api.audience" },
      user: { user_id: validSubject },
      secrets: defaultSecrets,
    };
    const api = createMockApi();

    await onExecutePostLogin(event, api);

    assert.ok(api.access.deniedReason?.includes("Invalid API audience"));
    assert.deepEqual(api.accessToken.customClaims, {});
  });

  it("should deny access if user_id is missing or invalid", async () => {
    const event = {
      client: { client_id: defaultSecrets.CUSTOMER_WEB_CLIENT_ID },
      resource_server: { identifier: defaultSecrets.API_AUDIENCE },
      user: { user_id: "" },
      secrets: defaultSecrets,
    };
    const api = createMockApi();

    await onExecutePostLogin(event, api);

    assert.ok(api.access.deniedReason?.includes("Invalid user subject"));
  });

  it("should deny access if user_id exceeds 255 characters", async () => {
    const event = {
      client: { client_id: defaultSecrets.CUSTOMER_WEB_CLIENT_ID },
      resource_server: { identifier: defaultSecrets.API_AUDIENCE },
      user: { user_id: "a".repeat(256) },
      secrets: defaultSecrets,
    };
    const api = createMockApi();

    await onExecutePostLogin(event, api);

    assert.ok(api.access.deniedReason?.includes("Invalid user subject"));
  });

  it("should deny access if user_id contains non-ASCII characters", async () => {
    const event = {
      client: { client_id: defaultSecrets.CUSTOMER_WEB_CLIENT_ID },
      resource_server: { identifier: defaultSecrets.API_AUDIENCE },
      user: { user_id: "auth0|測試用戶" },
      secrets: defaultSecrets,
    };
    const api = createMockApi();

    await onExecutePostLogin(event, api);

    assert.ok(api.access.deniedReason?.includes("Invalid user subject"));
  });

  it("should request M2M token with client_credentials and scope identity:resolve, and handle token failure", async () => {
    global.fetch = async (url, options) => {
      fetchCalls.push({ url, options });
      return {
        ok: false,
        status: 401,
        json: async () => ({ error: "invalid_client" }),
      };
    };

    const event = {
      client: { client_id: defaultSecrets.CUSTOMER_WEB_CLIENT_ID },
      resource_server: { identifier: defaultSecrets.API_AUDIENCE },
      user: { user_id: validSubject },
      secrets: defaultSecrets,
    };
    const api = createMockApi();

    await onExecutePostLogin(event, api);

    assert.equal(fetchCalls.length, 1);
    assert.equal(fetchCalls[0].url, defaultSecrets.AUTH0_TOKEN_URL);
    const body = JSON.parse(fetchCalls[0].options.body);
    assert.equal(body.grant_type, "client_credentials");
    assert.equal(body.client_id, defaultSecrets.M2M_CLIENT_ID);
    assert.equal(body.client_secret, defaultSecrets.M2M_CLIENT_SECRET);
    assert.equal(body.audience, defaultSecrets.API_AUDIENCE);
    assert.equal(body.scope, "identity:resolve");

    assert.ok(api.access.deniedReason?.includes("Identity resolution authorization failed"));
    assert.deepEqual(api.accessToken.customClaims, {});
  });

  it("should send M2M token and subject ONLY to resolver, and handle resolver HTTP failure", async () => {
    global.fetch = async (url, options) => {
      fetchCalls.push({ url, options });
      if (url === defaultSecrets.AUTH0_TOKEN_URL) {
        return {
          ok: true,
          status: 200,
          json: async () => ({ access_token: "mock-m2m-jwt-token" }),
        };
      }
      if (url === defaultSecrets.IDENTITY_RESOLVER_URL) {
        return {
          ok: false,
          status: 500,
          json: async () => ({ error: "internal_server_error" }),
        };
      }
      throw new Error(`Unexpected url: ${url}`);
    };

    const event = {
      client: { client_id: defaultSecrets.CUSTOMER_WEB_CLIENT_ID },
      resource_server: { identifier: defaultSecrets.API_AUDIENCE },
      user: { user_id: validSubject },
      secrets: defaultSecrets,
    };
    const api = createMockApi();

    await onExecutePostLogin(event, api);

    assert.equal(fetchCalls.length, 2);
    const resolverCall = fetchCalls[1];
    assert.equal(resolverCall.url, defaultSecrets.IDENTITY_RESOLVER_URL);
    assert.equal(resolverCall.options.headers.Authorization, "Bearer mock-m2m-jwt-token");
    const resolverBody = JSON.parse(resolverCall.options.body);
    assert.deepEqual(Object.keys(resolverBody), ["subject"]);
    assert.equal(resolverBody.subject, validSubject);

    assert.ok(api.access.deniedReason?.includes("Identity resolution request failed"));
    assert.deepEqual(api.accessToken.customClaims, {});
  });

  it("should deny access if resolver returns malformed or non-Guid customerId", async () => {
    global.fetch = async (url) => {
      if (url === defaultSecrets.AUTH0_TOKEN_URL) {
        return {
          ok: true,
          status: 200,
          json: async () => ({ access_token: "mock-m2m-jwt-token" }),
        };
      }
      return {
        ok: true,
        status: 200,
        json: async () => ({ customerId: "not-a-valid-guid" }),
      };
    };

    const event = {
      client: { client_id: defaultSecrets.CUSTOMER_WEB_CLIENT_ID },
      resource_server: { identifier: defaultSecrets.API_AUDIENCE },
      user: { user_id: validSubject },
      secrets: defaultSecrets,
    };
    const api = createMockApi();

    await onExecutePostLogin(event, api);

    assert.ok(api.access.deniedReason?.includes("invalid customer ID"));
    assert.deepEqual(api.accessToken.customClaims, {});
  });

  it("should deny access if resolver returns all-zero Guid customerId", async () => {
    global.fetch = async (url) => {
      if (url === defaultSecrets.AUTH0_TOKEN_URL) {
        return {
          ok: true,
          status: 200,
          json: async () => ({ access_token: "mock-m2m-jwt-token" }),
        };
      }
      return {
        ok: true,
        status: 200,
        json: async () => ({ customerId: "00000000-0000-0000-0000-000000000000" }),
      };
    };

    const event = {
      client: { client_id: defaultSecrets.CUSTOMER_WEB_CLIENT_ID },
      resource_server: { identifier: defaultSecrets.API_AUDIENCE },
      user: { user_id: validSubject },
      secrets: defaultSecrets,
    };
    const api = createMockApi();

    await onExecutePostLogin(event, api);

    assert.ok(api.access.deniedReason?.includes("invalid customer ID"));
    assert.deepEqual(api.accessToken.customClaims, {});
  });

  it("should succeed and set custom claim on access token ONLY when all validations pass", async () => {
    global.fetch = async (url) => {
      if (url === defaultSecrets.AUTH0_TOKEN_URL) {
        return {
          ok: true,
          status: 200,
          json: async () => ({ access_token: "mock-m2m-jwt-token" }),
        };
      }
      return {
        ok: true,
        status: 200,
        json: async () => ({ customerId: validCustomerId }),
      };
    };

    const event = {
      client: { client_id: defaultSecrets.CUSTOMER_WEB_CLIENT_ID },
      resource_server: { identifier: defaultSecrets.API_AUDIENCE },
      user: { user_id: validSubject },
      secrets: defaultSecrets,
    };
    const api = createMockApi();

    await onExecutePostLogin(event, api);

    assert.equal(api.access.deniedReason, null);
    assert.equal(
      api.accessToken.customClaims["urn:enterprisecommerce:customer_id"],
      validCustomerId
    );
    assert.deepEqual(api.idToken.customClaims, {});
  });

  it("should fail closed on unexpected network exception without leaking errors", async () => {
    global.fetch = async () => {
      throw new Error("Network timeout / socket hang up");
    };

    const event = {
      client: { client_id: defaultSecrets.CUSTOMER_WEB_CLIENT_ID },
      resource_server: { identifier: defaultSecrets.API_AUDIENCE },
      user: { user_id: validSubject },
      secrets: defaultSecrets,
    };
    const api = createMockApi();

    await onExecutePostLogin(event, api);

    assert.ok(api.access.deniedReason?.includes("Unable to complete customer identity resolution"));
    assert.deepEqual(api.accessToken.customClaims, {});
  });
});
