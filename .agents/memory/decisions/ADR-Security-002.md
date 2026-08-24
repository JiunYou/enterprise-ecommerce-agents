# ADR-Security-002: Secret Management Strategy Decision

## Metadata
- **ADR ID:** ADR-Security-002
- **Status:** Proposed
- **Owner Agent:** Security Review Agent
- **Created Date:** 2026-08-24

## Context
本平台 (Hybrid Architecture) 由多個分散的容器 (Docker) 組成，需要安全地存取資料庫連線字串、金流 API Keys 與 JWT 簽章密鑰，絕對不能硬編碼於版本控制系統中。

## Options
- **Option A:** K8s/Docker Built-in Secrets (Container Native)。
- **Option B:** HashiCorp Vault (自託管)。
- **Option C:** Cloud Secret Manager (AWS Secrets Manager / Azure Key Vault)。

## Decision
採用 **Option C: Cloud Secret Manager** 搭配 IAM Role 存取控制。

## Consequences
- **Positive:** 完全交由雲端供應商託管，具備最高級別的安全稽核認證 (FIPS 140-2)；完美整合自動輪換 (Rotation) 與精細的 IAM 權限控管；維運成本遠低於自建 Vault。
- **Negative:** 綁定特定的雲端供應商，跨雲遷移時需調整 Secret 注入的設定。
