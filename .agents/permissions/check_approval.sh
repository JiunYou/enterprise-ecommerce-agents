#!/bin/bash
INPUT=$(cat)
TOOL_NAME=$(echo "$INPUT" | grep -o '"name": *"[^"]*"' | head -1 | cut -d'"' -f4)

# 1. Protect Governance Resources (hooks.json, check_approval.sh)
if [[ "$TOOL_NAME" =~ (replace_file_content|multi_replace_file_content|write_to_file) ]]; then
    if echo "$INPUT" | grep -E -q '(hooks\.json|check_approval\.sh)'; then
        echo '{"decision": "force_ask", "reason": "HARD GATE: Modification of governance resources requires explicit human approval."}'
        exit 0
    fi
elif [[ "$TOOL_NAME" == "run_command" ]]; then
    if echo "$INPUT" | grep -E -q '(rm|sed|echo|touch|mv|cp|vi|nano|chmod).*(hooks\.json|check_approval\.sh)'; then
        echo '{"decision": "force_ask", "reason": "HARD GATE: Shell modification of governance resources requires explicit human approval."}'
        exit 0
    fi
fi

# 2. Database migrations
if [[ "$TOOL_NAME" == "run_command" ]]; then
    if echo "$INPUT" | grep -E -q '(dotnet ef|migration|update)'; then
        echo '{"decision": "force_ask", "reason": "HARD GATE: Database migrations require explicit human approval."}'
        exit 0
    fi
fi

# 3. Protected production writes (Code modifications)
# EnterpriseCommerce.Domain, Application, Infrastructure, WebApi are protected.
# .UnitTests and .IntegrationTests are not matched by this regex due to the boundary.
if [[ "$TOOL_NAME" =~ (replace_file_content|multi_replace_file_content|write_to_file) ]]; then
    if echo "$INPUT" | grep -E -q 'EnterpriseCommerce\.(Domain|Application|Infrastructure|WebApi)(/|\\|")'; then
        echo '{"decision": "force_ask", "reason": "HARD GATE: Protected production write requires explicit human approval."}'
        exit 0
    fi
fi

# 4. Protected shell-based write
if [[ "$TOOL_NAME" == "run_command" ]]; then
    # Match shell commands that write/modify files in the protected production directories
    if echo "$INPUT" | grep -E -q '(rm|sed|echo.*>|touch|mv|cp|vi|nano|chmod).*EnterpriseCommerce\.(Domain|Application|Infrastructure|WebApi)(/|\\|")'; then
        echo '{"decision": "force_ask", "reason": "HARD GATE: Protected shell-based write requires explicit human approval."}'
        exit 0
    fi
fi

# All other operations (Routine read/test operations) are allowed autonomously.
echo '{"decision": "allow"}'
