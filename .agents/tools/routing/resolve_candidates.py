"""
確定性候選人解析器 — 從規範設定消費路由信號並產生 RoutingDecision。

此模組從 agents.json / skills.json 讀取規範資料（不複製），
使用高信賴度確定性信號進行候選人解析：
  1. 路徑所有權（owns）
  2. 顯式技術關鍵字
  3. 風險/審查觸發（review_for / review_rules）
  4. 顯式 Agent 請求

語意模糊性保留給 LLM Orchestrator 處理。

使用方式（CLI）：
  python3 .agents/tools/routing/resolve_candidates.py \\
    --task "Modify JWT authorization in OrdersController" \\
    --path services/backend/EnterpriseCommerce.WebApi/Controllers/OrdersController.cs
"""

from __future__ import annotations

import argparse
import json
import re
import sys
from fnmatch import fnmatch
from pathlib import Path
from typing import Any

from routing_contract import (
    DECISION_VERSION,
    ReasonCode,
    RoutingDecision,
    RoutingStatus,
    RoutingValidationError,
    load_registry,
    validate_decision,
)


# ---------------------------------------------------------------------------
# 設定載入
# ---------------------------------------------------------------------------

def _find_repo_root() -> Path:
    """從此檔案所在位置往上尋找包含 AGENTS.md 的倉庫根目錄。"""
    current = Path(__file__).resolve().parent
    for _ in range(10):
        if (current / "AGENTS.md").exists():
            return current
        parent = current.parent
        if parent == current:
            break
        current = parent
    raise FileNotFoundError("無法找到包含 AGENTS.md 的倉庫根目錄")


def load_config(repo_root: Path) -> tuple[dict[str, Any], dict[str, Any]]:
    """載入 agents.json 和 skills.json 規範設定。"""
    agents_path = repo_root / ".agents" / "routing" / "agents.json"
    skills_path = repo_root / ".agents" / "routing" / "skills.json"

    if not agents_path.exists():
        raise FileNotFoundError(f"規範設定不存在：{agents_path}")
    if not skills_path.exists():
        raise FileNotFoundError(f"規範設定不存在：{skills_path}")

    with open(agents_path, "r", encoding="utf-8") as f:
        agents_data = json.load(f)
    with open(skills_path, "r", encoding="utf-8") as f:
        skills_data = json.load(f)

    return agents_data, skills_data


# ---------------------------------------------------------------------------
# 信號提取
# ---------------------------------------------------------------------------

# 技術關鍵字 → Agent ID 映射（保守 / 高信賴度）
_TECHNOLOGY_SIGNALS: dict[str, str] = {
    "c#": "dotnet-backend",
    "csharp": "dotnet-backend",
    "asp.net": "dotnet-backend",
    "asp.net core": "dotnet-backend",
    "ef core": "dotnet-backend",
    "entity framework": "dotnet-backend",
    ".net": "dotnet-backend",
    "dotnet": "dotnet-backend",
    "webapi": "dotnet-backend",
    "next.js": "frontend",
    "nextjs": "frontend",
    "react": "frontend",
    "frontend ui": "frontend",
    "tailwind": "frontend",
    "css": "frontend",
    "node.js": "nodejs-backend",
    "nodejs": "nodejs-backend",
    "mysql migration": "database",
    "mysql schema": "database",
    "database migration": "database",
    "docker": "devops",
    "dockerfile": "devops",
    "docker-compose": "devops",
    "ci/cd": "devops",
    "ci/cd pipeline": "devops",
    "kubernetes": "devops",
    "k8s": "devops",
    "ddd": "domain-architect",
    "domain model": "domain-architect",
    "aggregate": "domain-architect",
    "bounded context": "domain-architect",
}

# 風險/審查觸發關鍵字 — 從 agents.json review_for + review_rules 衍生
_SECURITY_TRIGGERS: list[str] = [
    "auth", "authorization", "authenticate", "authentication",
    "jwt", "token", "oauth", "rbac",
    "payment", "pay", "checkout payment", "payment gateway",
    "secret", "credential", "api key", "private key",
    "external api", "webhook", "third-party",
    "pii", "personal data", "user data",
    "privileged operation", "privilege",
    "injection", "xss", "csrf", "sql injection",
]

_COMPLIANCE_TRIGGERS: list[str] = [
    "privacy", "gdpr", "data protection",
    "pci", "pci dss", "pci compliance",
    "regulatory", "regulation", "compliance",
    "audit", "audit log", "audit trail",
]

_ARCHITECTURE_TRIGGERS: list[str] = [
    "architecture boundary", "cross-module",
    "dependency boundary", "architecture adr",
    "major domain architecture",
    "cross-service", "cross-aggregate",
    "bounded context change",
]


def _extract_path_owners(
    paths: list[str],
    agents_data: dict[str, Any],
) -> list[tuple[str, str]]:
    """
    根據 agents.json 中的 owns glob 模式解析路徑所有權。
    回傳 (agent_id, matched_path) 列表。
    """
    results: list[tuple[str, str]] = []
    for agent in agents_data["agents"]:
        for pattern in agent.get("owns", []):
            for path in paths:
                # 正規化路徑（移除前導 /）
                normalized = path.lstrip("/")
                if fnmatch(normalized, pattern):
                    results.append((agent["id"], normalized))
    return results


def _extract_technology_signals(task: str) -> list[tuple[str, str]]:
    """
    從任務描述中提取技術信號。
    回傳 (agent_id, matched_keyword) 列表。
    保守匹配：只匹配精確的關鍵字邊界。
    """
    task_lower = task.lower()
    results: list[tuple[str, str]] = []
    seen_agents: set[str] = set()

    # 從長到短排序，優先匹配較長的片段
    sorted_keywords = sorted(_TECHNOLOGY_SIGNALS.keys(), key=len, reverse=True)

    for keyword in sorted_keywords:
        agent_id = _TECHNOLOGY_SIGNALS[keyword]
        if keyword in task_lower and agent_id not in seen_agents:
            results.append((agent_id, keyword))
            seen_agents.add(agent_id)

    return results


def _extract_risk_reviewers(task: str) -> list[tuple[str, str]]:
    """
    從任務描述中提取風險信號並映射到審查者。
    回傳 (reviewer_agent_id, matched_trigger) 列表。
    """
    task_lower = task.lower()
    results: list[tuple[str, str]] = []
    seen: set[str] = set()

    for trigger in _SECURITY_TRIGGERS:
        if trigger in task_lower and "security" not in seen:
            results.append(("security", trigger))
            seen.add("security")
            break

    for trigger in _COMPLIANCE_TRIGGERS:
        if trigger in task_lower and "compliance" not in seen:
            results.append(("compliance", trigger))
            seen.add("compliance")
            break

    for trigger in _ARCHITECTURE_TRIGGERS:
        if trigger in task_lower and "architecture-reviewer" not in seen:
            results.append(("architecture-reviewer", trigger))
            seen.add("architecture-reviewer")
            break

    return results


def _normalize_token(word: str) -> str:
    """基本單複數與詞元正規化（保守且確定性）。"""
    w = word.lower().strip(",.:;()[]'\"")
    if w.endswith("ies") and len(w) > 4:
        return w[:-3] + "y"
    if w.endswith("es") and len(w) > 3 and w.endswith(("ches", "shes", "sses", "xes", "zes")):
        return w[:-2]
    if w.endswith("s") and len(w) > 3 and not w.endswith("ss"):
        return w[:-1]
    return w


def _match_primary_topic(topic: str, task: str) -> bool:
    """
    確定性、基於詞彙與單複數正規化的 primary_for 比對。
    不使用 ML 或外部相依，支援精確子字串與多詞概念匹配。
    """
    topic_lower = topic.lower()
    task_lower = task.lower()
    if topic_lower in task_lower:
        return True

    # 提取並正規化 topic 中的單詞
    topic_words = re.findall(r"[a-zA-Z0-9_\-\.]+", topic_lower)
    if not topic_words:
        return False

    norm_topic_words = [_normalize_token(w) for w in topic_words]

    # 提取並正規化 task 中的詞元集合
    task_words = re.findall(r"[a-zA-Z0-9_\-\.]+", task_lower)
    task_tokens: set[str] = set()
    for w in task_words:
        task_tokens.add(w)
        task_tokens.add(_normalize_token(w))

    # topic 中的所有關鍵詞元皆須於 task_tokens 中出現
    return all(w in task_tokens for w in norm_topic_words)


def _extract_primary_for_matches(
    task: str,
    agents_data: dict[str, Any],
) -> list[tuple[str, str]]:
    """
    從任務描述與 agents.json 的 primary_for 進行匹配。
    回傳 (agent_id, matched_topic) 列表。
    排除 orchestrator（它的 primary_for 是路由本身）。
    """
    results: list[tuple[str, str]] = []
    for agent in agents_data["agents"]:
        if agent["id"] == "orchestrator":
            continue
        for topic in agent.get("primary_for", []):
            if _match_primary_topic(topic, task):
                results.append((agent["id"], topic))
                break  # 每個 agent 最多匹配一次
    return results


# ---------------------------------------------------------------------------
# 技能解析
# ---------------------------------------------------------------------------

def _resolve_skills(
    candidate_agents: list[str],
    skills_data: dict[str, Any],
) -> tuple[list[str], list[str]]:
    """
    從 skills.json routing 對映中，根據候選 agent 解析 default + optional skills。
    """
    default_skills: list[str] = []
    optional_skills: list[str] = []

    routing = skills_data.get("routing", {})
    valid_skills_set = set(skills_data.get("skills", []))

    seen_default: set[str] = set()
    seen_optional: set[str] = set()

    for agent_id in candidate_agents:
        agent_routing = routing.get(agent_id, {})

        for skill in agent_routing.get("default", []):
            if skill in valid_skills_set and skill not in seen_default:
                default_skills.append(skill)
                seen_default.add(skill)

        for skill in agent_routing.get("optional", []):
            if skill in valid_skills_set and skill not in seen_optional and skill not in seen_default:
                optional_skills.append(skill)
                seen_optional.add(skill)

    return default_skills, optional_skills


# ---------------------------------------------------------------------------
# 主解析器
# ---------------------------------------------------------------------------

def resolve_candidates(
    task: str,
    paths: list[str] | None = None,
    requested_agent: str | None = None,
    *,
    repo_root: Path | None = None,
) -> RoutingDecision:
    """
    確定性候選人解析。

    參數：
        task: 任務描述文字
        paths: 可選的相關檔案路徑列表
        requested_agent: 可選的顯式指定 Agent ID
        repo_root: 可選的倉庫根目錄（預設自動偵測）

    回傳：
        RoutingDecision — 經過驗證的路由決策
    """
    if repo_root is None:
        repo_root = _find_repo_root()

    agents_data, skills_data = load_config(repo_root)
    valid_agents, valid_skills = load_registry(
        repo_root / ".agents" / "routing" / "agents.json",
        repo_root / ".agents" / "routing" / "skills.json",
    )

    # -- 收集信號 --
    candidate_agents: list[str] = []
    reason_codes: list[str] = []
    signal_paths: list[str] = []
    signal_techs: list[str] = []
    signal_risks: list[str] = []

    # 1. 顯式 Agent 請求
    if requested_agent is not None:
        if requested_agent not in valid_agents:
            raise RoutingValidationError(
                f"requested_agent {requested_agent!r} 不存在於 agents.json"
            )
        candidate_agents.append(requested_agent)
        reason_codes.append(ReasonCode.EXPLICIT_AGENT.value)

    # 2. 路徑所有權
    if paths:
        path_owners = _extract_path_owners(paths, agents_data)
        for agent_id, matched_path in path_owners:
            signal_paths.append(matched_path)
            if agent_id not in candidate_agents:
                candidate_agents.append(agent_id)
            if ReasonCode.PATH_OWNERSHIP.value not in reason_codes:
                reason_codes.append(ReasonCode.PATH_OWNERSHIP.value)

    # 3. 技術信號
    tech_matches = _extract_technology_signals(task)
    for agent_id, keyword in tech_matches:
        signal_techs.append(keyword)
        if agent_id not in candidate_agents:
            candidate_agents.append(agent_id)
        if ReasonCode.EXPLICIT_TECHNOLOGY.value not in reason_codes:
            reason_codes.append(ReasonCode.EXPLICIT_TECHNOLOGY.value)

    # 4. primary_for 匹配（僅作為後備信號）
    # 信號優先順序：explicit agent > path ownership > technology > primary_for
    # 當強確定性信號已識別實作候選者時，primary_for 不應引入競爭候選者
    has_strong_signals = len(candidate_agents) > 0
    primary_matches = _extract_primary_for_matches(task, agents_data)
    if not has_strong_signals:
        for agent_id, _topic in primary_matches:
            if agent_id not in candidate_agents:
                candidate_agents.append(agent_id)
            if ReasonCode.PRIMARY_FOR_MATCH.value not in reason_codes:
                reason_codes.append(ReasonCode.PRIMARY_FOR_MATCH.value)

    # 5. 風險 / 審查者
    required_reviewers: list[str] = []
    risk_matches = _extract_risk_reviewers(task)
    for reviewer_id, trigger in risk_matches:
        signal_risks.append(trigger)
        if reviewer_id not in required_reviewers:
            required_reviewers.append(reviewer_id)
        # 對應原因碼
        if reviewer_id == "security" and ReasonCode.SECURITY_REVIEW_REQUIRED.value not in reason_codes:
            reason_codes.append(ReasonCode.SECURITY_REVIEW_REQUIRED.value)
        elif reviewer_id == "compliance" and ReasonCode.COMPLIANCE_REVIEW_REQUIRED.value not in reason_codes:
            reason_codes.append(ReasonCode.COMPLIANCE_REVIEW_REQUIRED.value)
        elif reviewer_id == "architecture-reviewer" and ReasonCode.ARCHITECTURE_REVIEW_REQUIRED.value not in reason_codes:
            reason_codes.append(ReasonCode.ARCHITECTURE_REVIEW_REQUIRED.value)

    # -- 決定狀態 --
    # 動態從 agents.json 識別純審查者（無 primary_for 且無 owns 的 agent）
    # 若為使用者顯式請求（requested_agent），則不予過濾
    review_only_agents = {
        agent["id"] for agent in agents_data["agents"]
        if not agent.get("primary_for") and not agent.get("owns")
    }

    implementation_candidates = [
        a for a in candidate_agents
        if a not in review_only_agents or a == requested_agent
    ]

    if len(implementation_candidates) == 1:
        status = RoutingStatus.ROUTED.value
        primary_agent = implementation_candidates[0]
        reason_codes_final = reason_codes
    elif len(implementation_candidates) > 1:
        status = RoutingStatus.AMBIGUOUS.value
        primary_agent = None
        if ReasonCode.AMBIGUOUS_ROUTE.value not in reason_codes:
            reason_codes.append(ReasonCode.AMBIGUOUS_ROUTE.value)
        reason_codes_final = reason_codes
    else:
        status = RoutingStatus.UNMATCHED.value
        primary_agent = None
        implementation_candidates = []
        if ReasonCode.NO_ROUTE_MATCH.value not in reason_codes:
            reason_codes.append(ReasonCode.NO_ROUTE_MATCH.value)
        reason_codes_final = reason_codes

    # -- 技能解析 --
    skill_agents = implementation_candidates if implementation_candidates else []
    default_skills, optional_skills = _resolve_skills(skill_agents, skills_data)

    if default_skills and ReasonCode.DEFAULT_SKILL.value not in reason_codes_final:
        reason_codes_final.append(ReasonCode.DEFAULT_SKILL.value)
    if optional_skills and ReasonCode.OPTIONAL_SKILL_TRIGGER.value not in reason_codes_final:
        reason_codes_final.append(ReasonCode.OPTIONAL_SKILL_TRIGGER.value)

    # -- 組裝決策 --
    decision = RoutingDecision(
        decision_version=DECISION_VERSION,
        status=status,
        primary_agent=primary_agent,
        candidate_agents=implementation_candidates,
        required_reviewers=required_reviewers,
        default_skills=default_skills,
        candidate_optional_skills=optional_skills,
        signals={
            "paths": signal_paths,
            "technologies": signal_techs,
            "risk_topics": signal_risks,
        },
        reason_codes=reason_codes_final,
    )

    # -- 驗證 --
    errors = validate_decision(decision, valid_agents, valid_skills)
    if errors:
        raise RoutingValidationError(
            f"RoutingDecision 驗證失敗：{'; '.join(errors)}"
        )

    return decision


# ---------------------------------------------------------------------------
# CLI 介面
# ---------------------------------------------------------------------------

def main() -> None:
    parser = argparse.ArgumentParser(
        description="確定性候選人解析器 — 產生機器可讀 RoutingDecision"
    )
    parser.add_argument(
        "--task", required=True,
        help="任務描述文字"
    )
    parser.add_argument(
        "--path", action="append", default=None,
        help="相關檔案路徑（可多次指定）"
    )
    parser.add_argument(
        "--agent", default=None,
        help="顯式指定 Agent ID"
    )
    parser.add_argument(
        "--repo-root", default=None,
        help="倉庫根目錄（預設自動偵測）"
    )

    args = parser.parse_args()

    try:
        repo_root = Path(args.repo_root) if args.repo_root else None
        decision = resolve_candidates(
            task=args.task,
            paths=args.path,
            requested_agent=args.agent,
            repo_root=repo_root,
        )
        print(decision.to_json())
        sys.exit(0)
    except (RoutingValidationError, FileNotFoundError) as e:
        print(json.dumps({"error": str(e)}, ensure_ascii=False, indent=2),
              file=sys.stderr)
        sys.exit(1)


if __name__ == "__main__":
    main()
