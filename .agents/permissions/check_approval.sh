#!/bin/bash
python3 -c '
import sys, json, re

try:
    raw = sys.stdin.read()
    if not raw.strip():
        print(json.dumps({
            "decision": "force_ask",
            "reason": "HARD GATE: Empty payload received; fail closed."
        }))
        sys.exit(0)
    data = json.loads(raw)
except Exception:
    print(json.dumps({
        "decision": "force_ask",
        "reason": "HARD GATE: Malformed JSON payload received; fail closed."
    }))
    sys.exit(0)

if not isinstance(data, dict):
    print(json.dumps({
        "decision": "force_ask",
        "reason": "HARD GATE: Payload must be a JSON object; fail closed."
    }))
    sys.exit(0)

tool_call = data.get("toolCall") if "toolCall" in data else data
if not isinstance(tool_call, dict):
    print(json.dumps({
        "decision": "force_ask",
        "reason": "HARD GATE: Tool call is not an object; fail closed."
    }))
    sys.exit(0)

tool_name = tool_call.get("name")
args = tool_call.get("args")

if not isinstance(tool_name, str) or not tool_name or not isinstance(args, dict):
    print(json.dumps({
        "decision": "force_ask",
        "reason": "HARD GATE: Invalid toolCall structure; fail closed."
    }))
    sys.exit(0)

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
    if "hooks.json" in command_line or "check_approval.sh" in command_line:
        print(json.dumps({
            "decision": "force_ask",
            "reason": "HARD GATE: Shell access to governance resources requires explicit human approval."
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

# 3. Protected production Domain writes / execution
if tool_name in ["write_to_file", "replace_file_content", "multi_replace_file_content"]:
    stripped_target = re.sub(r"EnterpriseCommerce\.Domain\.(?:UnitTests|Tests|IntegrationTests)", "", target_file)
    if "EnterpriseCommerce.Domain" in stripped_target:
        print(json.dumps({
            "decision": "force_ask",
            "reason": "HARD GATE: Production Domain write requires explicit human approval."
        }))
        sys.exit(0)
elif tool_name == "run_command":
    stripped_cmd = re.sub(r"EnterpriseCommerce\.Domain\.(?:UnitTests|Tests|IntegrationTests)", "", command_line)
    if "EnterpriseCommerce.Domain" in stripped_cmd:
        print(json.dumps({
            "decision": "force_ask",
            "reason": "HARD GATE: Shell command referencing production Domain requires explicit human approval."
        }))
        sys.exit(0)

# All other routine operations (Application, WebApi, Infrastructure, tests, builds, etc.)
print(json.dumps({"decision": "allow"}))
'
