---
Document ID: ADR-015
Version: 1.0
Owner Agent: Enterprise Platform Engineer Agent
Created Date: 2024-05-20
Status: Approved
Related ADR: ADR-008
---

# ADR-015: Local Development Environment Decision

## Status
Approved

## Context
本專案依賴眾多基礎設施 (MySQL, Redis, RabbitMQ, Elasticsearch)。我們必須保證開發者與 AI Agent 在本地端 (Local) 能擁有與 Production 最接近的環境，以避免「在我電腦上是好的 (It works on my machine)」的問題。

## Decision
採用 **Docker Compose** 作為本地開發唯一標準。
- 將所有基礎設施 (Databases, Message Brokers, Cache) 定義在 `infrastructure/docker/docker-compose.yml`。
- 開發者可選擇一鍵啟動所有相依設施後，於本地原生執行 .NET 或 Node，或是連同應用程式一起跑在 Container 中。

## Alternatives Considered
- 要求開發者在本地原生安裝 MySQL、RabbitMQ 等軟體：環境極易髒污且版本難以統一，否決。
- 採用 Kubernetes (Minikube / k3d) 本地開發：對於單純後端與前端開發者而言，學習曲線過於陡峭，資源消耗過大。

## Consequences
- **Positive:** 完全環境隔離，版本統一，拉下程式碼後透過 `docker-compose up -d` 即可開箱即用。
- **Negative:** 需要開發者機器具備足夠的記憶體 (至少 16GB) 來撐起多個容器運行 (特別是 Elasticsearch)。

## Security Impact
本地 `docker-compose.yml` 中的預設密碼 (如 `MYSQL_ROOT_PASSWORD`) 僅限本機開發使用。絕對禁止將這些預設密碼帶入 Staging 或 Production 環境。

## Future Evolution
未來若微服務數量激增，可考慮引入 DevContainers 或雲端開發環境 (Cloud IDE / GitHub Codespaces) 來卸載本地機器的運算壓力。
