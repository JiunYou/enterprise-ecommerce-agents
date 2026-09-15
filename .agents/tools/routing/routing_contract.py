"""
RoutingDecision 合約 — 機器可讀路由決策結構與驗證邏輯。

此模組定義 RoutingDecision dataclass 和相關驗證函式。
所有 Agent / Skill 引用必須存在於規範登錄（agents.json / skills.json）中。

狀態：routed / ambiguous / unmatched
"""

from __future__ import annotations

import json
from dataclasses import dataclass, field, asdict
from enum import Enum
from pathlib import Path
from typing import Any


# ---------------------------------------------------------------------------
# 常量
# ---------------------------------------------------------------------------

DECISION_VERSION = "1.0"


class RoutingStatus(str, Enum):
    """路由決策狀態。"""
    ROUTED = "routed"
    AMBIGUOUS = "ambiguous"
    UNMATCHED = "unmatched"


class ReasonCode(str, Enum):
    """確定性原因碼 — 僅包含實際由實作使用的碼。"""
    PATH_OWNERSHIP = "PATH_OWNERSHIP"
    EXPLICIT_TECHNOLOGY = "EXPLICIT_TECHNOLOGY"
    PRIMARY_FOR_MATCH = "PRIMARY_FOR_MATCH"
    EXPLICIT_AGENT = "EXPLICIT_AGENT"
    SECURITY_REVIEW_REQUIRED = "SECURITY_REVIEW_REQUIRED"
    COMPLIANCE_REVIEW_REQUIRED = "COMPLIANCE_REVIEW_REQUIRED"
    ARCHITECTURE_REVIEW_REQUIRED = "ARCHITECTURE_REVIEW_REQUIRED"
    DEFAULT_SKILL = "DEFAULT_SKILL"
    OPTIONAL_SKILL_TRIGGER = "OPTIONAL_SKILL_TRIGGER"
    AMBIGUOUS_ROUTE = "AMBIGUOUS_ROUTE"
    NO_ROUTE_MATCH = "NO_ROUTE_MATCH"


@dataclass
class SignalEvidence:
    """決策過程中提取的信號證據。"""
    paths: list[str] = field(default_factory=list)
    technologies: list[str] = field(default_factory=list)
    risk_topics: list[str] = field(default_factory=list)


@dataclass
class RoutingDecision:
    """機器可讀路由決策。"""
    decision_version: str = DECISION_VERSION
    status: str = RoutingStatus.UNMATCHED.value
    primary_agent: str | None = None
    candidate_agents: list[str] = field(default_factory=list)
    required_reviewers: list[str] = field(default_factory=list)
    default_skills: list[str] = field(default_factory=list)
    candidate_optional_skills: list[str] = field(default_factory=list)
    signals: dict[str, list[str]] = field(default_factory=lambda: {
        "paths": [], "technologies": [], "risk_topics": []
    })
    reason_codes: list[str] = field(default_factory=list)

    def to_dict(self) -> dict[str, Any]:
        return asdict(self)

    def to_json(self, indent: int = 2) -> str:
        return json.dumps(self.to_dict(), indent=indent, ensure_ascii=False)


# ---------------------------------------------------------------------------
# 驗證
# ---------------------------------------------------------------------------

class RoutingValidationError(Exception):
    """路由決策驗證失敗。"""


def load_registry(agents_json_path: Path, skills_json_path: Path) -> tuple[set[str], set[str]]:
    """從規範設定檔載入有效 Agent ID 和 Skill ID 集合。"""
    with open(agents_json_path, "r", encoding="utf-8") as f:
        agents_data = json.load(f)
    with open(skills_json_path, "r", encoding="utf-8") as f:
        skills_data = json.load(f)

    valid_agents = {a["id"] for a in agents_data["agents"]}
    valid_skills = set(skills_data["skills"])
    return valid_agents, valid_skills


def validate_decision(
    decision: RoutingDecision,
    valid_agents: set[str],
    valid_skills: set[str],
) -> list[str]:
    """
    驗證 RoutingDecision，回傳錯誤訊息列表（空 = 通過）。

    驗證規則：
    1. status 必須是有效值
    2. primary_agent 若存在必須在 valid_agents 中
    3. candidate_agents 必須全部在 valid_agents 中且無重複
    4. required_reviewers 必須全部在 valid_agents 中且無重複
    5. default_skills / candidate_optional_skills 必須在 valid_skills 中且無重複
    6. reason_codes 必須全部是有效的 ReasonCode 且無重複
    7. status 與 agent 狀態的一致性：
       - routed => 必須有 primary_agent
       - ambiguous => 不得有 primary_agent，但 candidate_agents 不得為空
       - unmatched => 不得有 primary_agent 且 candidate_agents 必須為空
    8. decision_version 必須存在
    """
    errors: list[str] = []

    # version
    if not decision.decision_version:
        errors.append("decision_version 為空")

    # status
    valid_statuses = {s.value for s in RoutingStatus}
    if decision.status not in valid_statuses:
        errors.append(f"無效的 status: {decision.status!r}（有效值：{valid_statuses}）")

    # primary_agent
    if decision.primary_agent is not None:
        if decision.primary_agent not in valid_agents:
            errors.append(f"primary_agent {decision.primary_agent!r} 不存在於 agents.json")

    # candidate_agents
    _check_refs(decision.candidate_agents, valid_agents, "candidate_agents", "agents.json", errors)

    # required_reviewers
    _check_refs(decision.required_reviewers, valid_agents, "required_reviewers", "agents.json", errors)

    # skills
    _check_refs(decision.default_skills, valid_skills, "default_skills", "skills.json", errors)
    _check_refs(decision.candidate_optional_skills, valid_skills, "candidate_optional_skills", "skills.json", errors)

    # reason_codes
    valid_codes = {c.value for c in ReasonCode}
    for code in decision.reason_codes:
        if code not in valid_codes:
            errors.append(f"無效的 reason_code: {code!r}")
    if len(decision.reason_codes) != len(set(decision.reason_codes)):
        errors.append("reason_codes 包含重複值")

    # status / agent 一致性
    if decision.status == RoutingStatus.ROUTED.value:
        if decision.primary_agent is None:
            errors.append("status=routed 但 primary_agent 為 None")
    elif decision.status == RoutingStatus.AMBIGUOUS.value:
        if decision.primary_agent is not None:
            errors.append("status=ambiguous 但 primary_agent 不為 None")
        if not decision.candidate_agents:
            errors.append("status=ambiguous 但 candidate_agents 為空")
    elif decision.status == RoutingStatus.UNMATCHED.value:
        if decision.primary_agent is not None:
            errors.append("status=unmatched 但 primary_agent 不為 None")
        if decision.candidate_agents:
            errors.append("status=unmatched 但 candidate_agents 不為空")

    return errors


def _check_refs(
    refs: list[str],
    valid_set: set[str],
    field_name: str,
    source_file: str,
    errors: list[str],
) -> None:
    """檢查引用列表中的每一項是否存在於有效集合中，且無重複。"""
    for ref in refs:
        if ref not in valid_set:
            errors.append(f"{field_name} 中的 {ref!r} 不存在於 {source_file}")
    if len(refs) != len(set(refs)):
        errors.append(f"{field_name} 包含重複值")
