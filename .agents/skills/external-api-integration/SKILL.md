---
name: external-api-integration
description: Use when integrating third-party APIs (payments, webhooks), handling retries, idempotency, and timeouts.
---
# External Api Integration Skill


ntegration-agent.md
# API Integration Agent
## 職責
- 第三方 API 整合設計
- REST / GraphQL / gRPC 通訊
- API Gateway 設定
- API 文件撰寫 (Swagger/OpenAPI)
## Guidance
When integrating external APIs, ensure proper timeout configuration, implement retry logic with exponential backoff, handle idempotency for non-safe operations, and validate external provider webhooks properly. Map external API failures gracefully.
