"""
路由回歸測試執行器 — 驗證確定性候選人解析器的正確性。

讀取 routing_fixtures.json 並逐一驗證每個測試案例。
安全敏感的審查者缺漏會導致測試失敗。

使用方式：
  python3 .agents/tools/routing/run_routing_tests.py

結束碼：0 = 全部通過, 1 = 有失敗
"""

from __future__ import annotations

import json
import sys
from pathlib import Path

# 確保可以匯入同目錄模組
sys.path.insert(0, str(Path(__file__).resolve().parent))

from resolve_candidates import resolve_candidates
from routing_contract import RoutingValidationError


def load_fixtures(fixtures_path: Path) -> list[dict]:
    """載入測試固件。"""
    with open(fixtures_path, "r", encoding="utf-8") as f:
        data = json.load(f)
    return data["fixtures"]


def run_tests(repo_root: Path) -> tuple[int, int, int, list[str]]:
    """
    執行所有路由回歸測試。

    回傳：(total, passed, failed, failure_details)
    """
    fixtures_path = repo_root / ".agents" / "tools" / "routing" / "routing_fixtures.json"
    fixtures = load_fixtures(fixtures_path)

    total = len(fixtures)
    passed = 0
    failed = 0
    failure_details: list[str] = []

    # 統計指標
    primary_agent_correct = 0
    primary_agent_total = 0
    reviewer_expected_total = 0
    reviewer_found_total = 0
    invalid_route_count = 0

    for fixture in fixtures:
        fixture_id = fixture["id"]
        errors: list[str] = []

        try:
            decision = resolve_candidates(
                task=fixture["task"],
                paths=fixture.get("paths", None) or None,
                requested_agent=fixture.get("requested_agent", None),
                repo_root=repo_root,
            )

            # 驗證 status
            if decision.status != fixture["expected_status"]:
                errors.append(
                    f"status: 預期 {fixture['expected_status']!r}, 實際 {decision.status!r}"
                )

            # 驗證 primary_agent
            expected_primary = fixture.get("expected_primary_agent")
            primary_agent_total += 1
            if decision.primary_agent != expected_primary:
                errors.append(
                    f"primary_agent: 預期 {expected_primary!r}, 實際 {decision.primary_agent!r}"
                )
            else:
                primary_agent_correct += 1

            # 驗證 required_reviewers（包含檢查）
            for reviewer in fixture.get("expected_reviewers_contain", []):
                reviewer_expected_total += 1
                if reviewer in decision.required_reviewers:
                    reviewer_found_total += 1
                else:
                    errors.append(
                        f"required_reviewers 缺少: {reviewer!r}"
                    )
                    # 安全敏感審查者缺漏是嚴重錯誤
                    if reviewer in ("security", "compliance"):
                        errors.append(
                            f"[嚴重] 安全/合規審查者 {reviewer!r} 缺漏"
                        )

            # 驗證 default_skills（包含檢查）
            for skill in fixture.get("expected_default_skills_contain", []):
                if skill not in decision.default_skills:
                    errors.append(
                        f"default_skills 缺少: {skill!r}"
                    )

            # 驗證 reason_codes（包含檢查）
            for code in fixture.get("expected_reason_codes_contain", []):
                if code not in decision.reason_codes:
                    errors.append(
                        f"reason_codes 缺少: {code!r}"
                    )

        except RoutingValidationError as e:
            errors.append(f"RoutingValidationError: {e}")
            invalid_route_count += 1
        except Exception as e:
            errors.append(f"未預期錯誤: {type(e).__name__}: {e}")
            invalid_route_count += 1

        if errors:
            failed += 1
            detail = f"  FAIL [{fixture_id}]: {fixture['description']}"
            for err in errors:
                detail += f"\n    - {err}"
            failure_details.append(detail)
            print(f"  FAIL  {fixture_id}")
        else:
            passed += 1
            print(f"  PASS  {fixture_id}")

    # 統計報告
    print("\n" + "=" * 60)
    print(f"測試結果: {total} 案例, {passed} 通過, {failed} 失敗")
    print(f"primary_agent 準確率: {primary_agent_correct}/{primary_agent_total}")
    if reviewer_expected_total > 0:
        print(f"required_reviewer 召回率: {reviewer_found_total}/{reviewer_expected_total}")
    if invalid_route_count > 0:
        print(f"無效路由數: {invalid_route_count}")
    print("=" * 60)

    if failure_details:
        print("\n失敗詳情:")
        for detail in failure_details:
            print(detail)

    return total, passed, failed, failure_details


def run_property_tests(repo_root: Path) -> tuple[int, int, int, list[str]]:
    """
    執行屬性與合約測試：
    1. invalid reference rejection (拒絕未知 Agent / Skill)
    2. deterministic repeated result (重複呼叫 10 次結果一致)
    3. no all-reviewer fanout (非全面選派所有審查者)
    """
    from routing_contract import RoutingDecision, RoutingStatus, load_registry, validate_decision

    print("\n屬性與合約測試")
    print("=" * 60)

    passed = 0
    failed = 0
    details: list[str] = []

    valid_agents, valid_skills = load_registry(
        repo_root / ".agents" / "routing" / "agents.json",
        repo_root / ".agents" / "routing" / "skills.json",
    )

    # 1. invalid reference rejection - unknown requested_agent
    try:
        resolve_candidates(
            task="Some task",
            requested_agent="nonexistent-phantom-agent",
            repo_root=repo_root,
        )
        failed += 1
        details.append("  FAIL [invalid-agent-rejection]: 未拒絕不存在的 requested_agent")
        print("  FAIL  invalid-agent-rejection")
    except RoutingValidationError:
        passed += 1
        print("  PASS  invalid-agent-rejection")
    except Exception as e:
        failed += 1
        details.append(f"  FAIL [invalid-agent-rejection]: 拋出未預期異常 {type(e).__name__}: {e}")
        print("  FAIL  invalid-agent-rejection")

    # 2. invalid reference rejection - validate_decision with invalid skill
    bad_decision = RoutingDecision(
        status=RoutingStatus.ROUTED.value,
        primary_agent="dotnet-backend",
        candidate_agents=["dotnet-backend"],
        default_skills=["nonexistent-fake-skill"],
    )
    val_errors = validate_decision(bad_decision, valid_agents, valid_skills)
    if any("nonexistent-fake-skill" in err for err in val_errors):
        passed += 1
        print("  PASS  invalid-skill-rejection")
    else:
        failed += 1
        details.append("  FAIL [invalid-skill-rejection]: validate_decision 未拒絕不存在的 skill")
        print("  FAIL  invalid-skill-rejection")

    # 3. deterministic repeated result (10 iterations)
    try:
        results = [
            resolve_candidates(
                task="Modify JWT authorization behavior in OrdersController",
                paths=["services/backend/EnterpriseCommerce.WebApi/Controllers/OrdersController.cs"],
                repo_root=repo_root,
            ).to_json()
            for _ in range(10)
        ]
        if len(set(results)) == 1:
            passed += 1
            print("  PASS  deterministic-10-repeat")
        else:
            failed += 1
            details.append("  FAIL [deterministic-10-repeat]: 10 次執行產出非完全一致之結果")
            print("  FAIL  deterministic-10-repeat")
    except Exception as e:
        failed += 1
        details.append(f"  FAIL [deterministic-10-repeat]: 執行時發生錯誤 {e}")
        print("  FAIL  deterministic-10-repeat")

    # 4. no all-reviewer fanout
    try:
        decision = resolve_candidates(
            task="Add pagination to customer order history endpoint",
            paths=["services/backend/EnterpriseCommerce.WebApi/Controllers/OrdersController.cs"],
            repo_root=repo_root,
        )
        all_reviewers = {"security", "compliance", "architecture-reviewer", "documentation-reviewer"}
        if len(decision.required_reviewers) == 0 and len(decision.required_reviewers) < len(all_reviewers):
            passed += 1
            print("  PASS  no-all-reviewer-fanout")
        else:
            failed += 1
            details.append("  FAIL [no-all-reviewer-fanout]: 常規任務不應選派所有審查者")
            print("  FAIL  no-all-reviewer-fanout")
    except Exception as e:
        failed += 1
        details.append(f"  FAIL [no-all-reviewer-fanout]: 執行時發生錯誤 {e}")
        print("  FAIL  no-all-reviewer-fanout")

    print("=" * 60)
    print(f"屬性測試結果: {passed + failed} 案例, {passed} 通過, {failed} 失敗")
    print("=" * 60)

    if details:
        print("\n屬性測試失敗詳情:")
        for d in details:
            print(d)

    return passed + failed, passed, failed, details


def main() -> None:
    # 自動偵測倉庫根目錄
    current = Path(__file__).resolve().parent
    for _ in range(10):
        if (current / "AGENTS.md").exists():
            break
        parent = current.parent
        if parent == current:
            print("錯誤：無法找到包含 AGENTS.md 的倉庫根目錄", file=sys.stderr)
            sys.exit(1)
        current = parent

    print("路由回歸測試")
    print("=" * 60)

    total_f, passed_f, failed_f, _ = run_tests(current)
    total_p, passed_p, failed_p, _ = run_property_tests(current)

    total_failed = failed_f + failed_p
    if total_failed > 0:
        sys.exit(1)
    sys.exit(0)


if __name__ == "__main__":
    main()
