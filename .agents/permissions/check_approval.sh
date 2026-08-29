#!/bin/bash
python3 -c '
import sys, json, re

try:
    raw = sys.stdin.read()
    if not raw.strip():
        print(json.dumps({"decision": "allow"}))
        sys.exit(0)
    data = json.loads(raw)
except Exception:
    print(json.dumps({"decision": "allow"}))
    sys.exit(0)

tool_call = data.get("toolCall", {}) if isinstance(data, dict) and "toolCall" in data else data
if not isinstance(tool_call, dict):
    tool_call = {}

tool_name = tool_call.get("name", "")
args = tool_call.get("args", {})
if not isinstance(args, dict):
    args = {}

target_file = str(args.get("TargetFile", ""))
command_line = str(args.get("CommandLine", ""))

# 1. Protect Governance Resources (hooks.json, check_approval.sh)
if tool_name in ["write_to_file", "replace_file_content", "multi_replace_file_content"]:
    if re.search(r"(?:^|/|\\)(?:hooks\.json|check_approval\.sh)$", target_file) or "hooks.json" in target_file or "check_approval.sh" in target_file:
        print(json.dumps({
            "decision": "force_ask",
            "reason": "HARD GATE: Modification of governance resources requires explicit human approval."
        }))
        sys.exit(0)
elif tool_name == "run_command":
    if re.search(r"(?:rm|sed|echo|touch|mv|cp|vi|nano|chmod|cat|>)\s+.*(?:hooks\.json|check_approval\.sh)", command_line):
        print(json.dumps({
            "decision": "force_ask",
            "reason": "HARD GATE: Shell modification of governance resources requires explicit human approval."
        }))
        sys.exit(0)

# 2. Database migrations or database-update commands
if tool_name == "run_command":
    if re.search(r"\bdotnet\s+ef\b|\bdatabase\s+update\b|\bmigrations\s+add\b", command_line, re.IGNORECASE):
        print(json.dumps({
            "decision": "force_ask",
            "reason": "HARD GATE: Database migrations or database-update commands require explicit human approval."
        }))
        sys.exit(0)

# 3. Protected production Domain writes (Code modifications to EnterpriseCommerce.Domain excluding UnitTests/Tests)
if tool_name in ["write_to_file", "replace_file_content", "multi_replace_file_content"]:
    if "EnterpriseCommerce.Domain" in target_file and not re.search(r"EnterpriseCommerce\.Domain\.(?:UnitTests|Tests|IntegrationTests)", target_file):
        print(json.dumps({
            "decision": "force_ask",
            "reason": "HARD GATE: Production Domain write requires explicit human approval."
        }))
        sys.exit(0)
elif tool_name == "run_command":
    if re.search(r"(?:rm|sed|echo|touch|mv|cp|vi|nano|chmod)\s+.*EnterpriseCommerce\.Domain", command_line) and not re.search(r"EnterpriseCommerce\.Domain\.(?:UnitTests|Tests|IntegrationTests)", command_line):
        print(json.dumps({
            "decision": "force_ask",
            "reason": "HARD GATE: Shell modification of production Domain requires explicit human approval."
        }))
        sys.exit(0)

# All other routine operations (Application, WebApi, Infrastructure, tests, builds, etc.)
print(json.dumps({"decision": "allow"}))
'
