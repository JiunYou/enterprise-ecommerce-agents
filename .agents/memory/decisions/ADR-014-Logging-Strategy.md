---
Document ID: ADR-014
Version: 1.0
Owner Agent: Enterprise Platform Engineer Agent
Created Date: 2024-05-20
Status: Approved
Related ADR: ADR-008
---

# ADR-014: Logging Strategy Decision

## Status
Approved

## Context
跨越多個 Node.js 服務與 .NET 單體的混合架構中，必須確保 Log 具備統一格式，才能在日後的 ELK / Datadog 等集中式監控系統中順暢進行日誌關聯 (Correlation)。

## Decision
- **格式規範:** 全面採用 Structured Logging (JSON 格式)。
- **.NET 框架:** 採用 `Serilog`，利用其強大的 Sink 支援與 Enrichment 功能 (如預設帶入 CorrelationId)。
- **Node.js 框架:** 採用 `Pino`，以極低效能消耗為首要考量，產出標準 JSON。

## Alternatives Considered
- **Winston (Node.js) / NLog (.NET):** 雖然成熟，但在高併發下效能稍差，且設定 JSON 輸出較繁瑣。

## Consequences
- **Positive:** 無論是 .NET 還是 Node，輸出的 Log 結構將高度統一，易於後端分析引擎剖析。
- **Negative:** 開發者在本地 Debug 時，需依賴工具將 JSON Log 美化 (Pretty Print) 才易於閱讀。

## Security Impact
必須設定 Logging Redaction / Masking 規則，確保密碼、信用卡號、授權 Token 等敏感資訊絕不能進入日誌系統。

## Future Evolution
未來將於 API Gateway 層級產出 Request-ID (Correlation ID)，並透過 HTTP Header (如 `X-Correlation-ID`) 貫穿整個微服務鏈，記錄至每一筆 Log 中。
