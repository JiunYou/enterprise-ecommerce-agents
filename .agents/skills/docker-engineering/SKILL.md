---
name: docker-engineering
description: Use when creating or modifying Dockerfiles, docker-compose, and container orchestration.
---
# Docker Engineering Skill



# Docker Engineering

## Container Design
- **Dockerfile**: 遵循最佳實踐撰寫高效能且安全的 Dockerfile。
- **Multi-stage Build**: 利用多階段建置分離編譯環境與執行環境，減少 Image Size。
- **Image Optimization**: 減少層次 (Layers)、使用 `.dockerignore`、選擇 Alpine 或 distroless 輕量級 Base Image。

## Runtime
- **Lifecycle**: 掌握容器的啟動、停止與重啟策略 (Restart Policies)。
- **Health Check**: 實作 `HEALTHCHECK` 指令確保服務可用性。
- **Resource Limit**: 設置 CPU 與記憶體限制 (`--cpus`, `--memory`) 避免資源耗盡。

## Networking
- **Container Network**: 熟悉 Bridge, Host, Overlay 網路模式。
- **Service Discovery**: 運用 Docker 內部 DNS 進行容器間通訊。
- **Port Mapping**: 安全與有效率地綁定主機與容器的通訊埠。

## Storage
- **Volume**: 使用 Docker Volumes 處理資料持久化 (Persistence)。
- **Persistence**: 隔離容器生命週期與資料，避免資料遺失。
- **Backup**: 建立 Volume 備份與還原機制。

## Security
- **Non-root Container**: 避免使用 `root` 執行容器，建立特定 User 權限。
- **Image Vulnerability Scan**: 使用掃描工具 (如 Trivy) 檢查 Image 漏洞。
- **Secret Handling**: 安全注入與管理機敏資料，避免將 Secrets 寫入 Dockerfile 或 Image 層。

## Production
- **Logging**: 設置與管理 Logging Drivers (如 json-file, syslog) 並整合集中式 Log 系統。
- **Monitoring**: 監控容器狀態 (`docker stats`、cAdvisor 整合)。
- **Registry**: 安全地推送與管理私有 Registry Image。
