---
Document ID: DOC-ADR-010
Version: 1.0
Owner Agent: Enterprise Software Delivery Architect
Created Date: 2026-08-24
Status: Proposed
Related ADR: ADR-008
---

# ADR-010: CI/CD Pipeline Decision

## Status
Proposed

## Context
在 Polyglot Monorepo (ADR-008) 下，我們需要一個高度自動化、支援多語言建置，並內建安全掃描的 CI/CD 流程，以確保每次 Pull Request 的品質。

## Decision
採用 **GitHub Actions** 作為全域 CI/CD 平台。
規劃分為三大類別 Pipeline：
1. **CI Pipeline (PR Trigger):**
   - 針對有異動的目錄 (Path Filtering) 進行 Restore, Lint, Format, Build。
   - 執行 Unit Test 與輕量級 Integration Test。
2. **Security Pipeline (PR & Scheduled Trigger):**
   - 包含 SAST (如 SonarQube/CodeQL)、Dependency Vulnerability Scan (Dependabot/Snyk)、Secret Detection (TruffleHog)。
   - Docker Container Image Scan (Trivy)。
3. **CD Pipeline (Merge Trigger & Tag Trigger):**
   - `main` 合併後自動建置 Docker Image 推進 Registry (如 GHCR / AWS ECR)。
   - 自動部署至 Development 環境。
   - 需人工核准 (Deployment Approval) 方可推進 Staging 與 Production。

## Alternatives Considered
- **Jenkins:** 彈性最大但維護 Master/Worker 節點成本高。
- **GitLab CI:** 同等強大，但專案目前託管於 GitHub，故直接使用 GitHub Actions 最為原生。

## Consequences
- **Positive:** 開發者與 Agent 無需離開 GitHub 即可檢視所有 Pipeline 狀態；雲端託管零維護成本。
- **Negative:** Monorepo 下的 GitHub Actions 設定較複雜，需維護精準的 `paths:` 過濾，否則會浪費不必要的 CI 分鐘數。

## Security Impact
部署所需之雲端 Credentials 必須採用 OIDC 認證 (如 AWS Role assumed by GitHub Actions) 取代永久性長效 API Key。

## Future Evolution
引入 ArgoCD 或 Flux 實現 GitOps，讓 CD 流程改由 Cluster 主動 Pull 設定，進一步提升部署安全性。
