import os
import json
import sys

def check_exists(path):
    if not os.path.exists(path):
        print(f"Error: {path} is missing.")
        return False
    return True

errors = 0

# 1. Required root checks
if not check_exists("AGENTS.md"):
    errors += 1
else:
    size = os.path.getsize("AGENTS.md")
    print(f"AGENTS.md size: {size} bytes")
    if size > 100000:
        print("Error: AGENTS.md unreasonable growth.")
        errors += 1

expected_agents = ["orchestrator", "product-manager", "system-architect", "architecture-reviewer",
    "domain-architect", "dotnet-backend", "nodejs-backend", "frontend", "database",
    "devops", "qa", "security", "compliance", "documentation-reviewer", "release"]

# 2. Agent checks
found_agents = [d for d in os.listdir(".agents/agents") if os.path.isdir(os.path.join(".agents/agents", d))]
if set(found_agents) != set(expected_agents):
    print("Error: Agent directories do not match canonical 15 exactly.")
    errors += 1

for agent in expected_agents:
    agent_file = f".agents/agents/{agent}/agent.md"
    if not check_exists(agent_file):
        errors += 1
    else:
        with open(agent_file, "r") as f:
            content = f.read()
            if "\\n" in content:
                print(f"Error: literal \\n found in {agent_file}")
                errors += 1
            if "### From" in content or "===" in content:
                print(f"Error: merge artifact found in {agent_file}")
                errors += 1
            if "Provides implementation and review for" in content:
                print(f"Error: Generic implementation description in {agent_file}")
                errors += 1
            if len(content.strip()) < 20:
                print(f"Error: Body effectively empty in {agent_file}")
                errors += 1

            blocks = content.split("---")
            if len(blocks) != 3 or not content.startswith("---"):
                print(f"Error: Expected exactly 1 frontmatter block in {agent_file}")
                errors += 1
            else:
                fm = blocks[1]
                if f"name: {agent}" not in fm:
                    print(f"Error: Frontmatter name doesn't match directory in {agent_file}")
                    errors += 1

# 3. Skill checks
expected_skills = [
    "api-security", "code-review", "ddd", "devops", "docker-engineering", "dotnet",
    "ecommerce-domain", "ecommerce-security", "external-api-integration", "frontend-engineering",
    "git-workflow", "javascript-engineering", "mysql", "nextjs", "nodejs", "rabbitmq",
    "rest-api-design", "security", "software-architecture", "system-modeling", "testing", "threat-modeling"
]
found_skills = [d for d in os.listdir(".agents/skills") if os.path.isdir(os.path.join(".agents/skills", d))]
if set(found_skills) != set(expected_skills):
    print("Error: Skill directories do not match canonical 22 exactly.")
    errors += 1

for skill in expected_skills:
    skill_file = f".agents/skills/{skill}/SKILL.md"
    if not check_exists(skill_file):
        errors += 1
    else:
        with open(skill_file, "r") as f:
            content = f.read()
            if "\\n" in content:
                print(f"Error: literal \\n found in {skill_file}")
                errors += 1
            if "### From" in content:
                print(f"Error: merge artifact found in {skill_file}")
                errors += 1
            if "\n\n\n" in content:
                print(f"Error: more than two consecutive blank lines in {skill_file}")
                errors += 1
            h1_count = sum(1 for line in content.split("\n") if line.startswith("# "))
            if h1_count != 1:
                print(f"Error: Expected exactly 1 H1 in {skill_file}, found {h1_count}")
                errors += 1

            blocks = content.split("---")
            if len(blocks) != 3 or not content.startswith("---"):
                print(f"Error: Expected exactly 1 frontmatter block in {skill_file}")
                errors += 1
            else:
                fm = blocks[1]
                if f"name: {skill}" not in fm:
                    print(f"Error: Frontmatter name doesn't match directory in {skill_file}")
                    errors += 1

# 4. Routing checks
with open(".agents/routing/agents.json", "r") as f:
    agents_json = json.load(f)
    if agents_json.get("entrypoint") != "orchestrator": errors += 1
    if agents_json.get("max_handoff_depth") > 2: errors += 1
    names = [a['id'] for a in agents_json['agents']]
    if set(names) != set(expected_agents): errors += 1
    if len(names) != len(set(names)):
        print("Error: Agent IDs not unique")
        errors += 1
    risk = agents_json.get("risk_rules", {})
    if not check_exists(risk.get("source", "")):
        print("Error: risk_rules source doesn't exist")
        errors += 1
    if risk.get("routine_requires_human_approval") is not False: errors += 1
    if risk.get("high_risk_requires_human_approval") is not True: errors += 1

with open(".agents/routing/skills.json", "r") as f:
    skills_json = json.load(f)
    for agent in expected_agents:
        if agent not in skills_json["routing"]:
            print(f"Error: Agent {agent} missing from skills.json routing")
            errors += 1
        else:
            refs = skills_json["routing"][agent].get("default", []) + skills_json["routing"][agent].get("optional", [])
            if len(refs) != len(set(refs)):
                print(f"Error: Duplicate skill refs in routing for {agent}")
                errors += 1
            for s in refs:
                if s not in expected_skills:
                    print(f"Error: Obsolete or invalid skill '{s}' referenced by {agent}")
                    errors += 1

# 5. Permission checks
perm_matrix = ".agents/permissions/agent-permission-matrix.md"
if not check_exists(perm_matrix):
    errors += 1
else:
    size = os.path.getsize(perm_matrix)
    if size == 0:
        print("Error: permission matrix is zero bytes")
        errors += 1
    with open(perm_matrix, "r") as f:
        content = f.read()
        for a in expected_agents:
            if a not in content:
                print(f"Error: Agent {a} not found in permission matrix")
                errors += 1
        for obsolete in ["Memory Agent", "Master Orchestrator", "Documentation Validator"]:
            if obsolete in content:
                print(f"Error: Obsolete agent {obsolete} found in permission matrix")
                errors += 1

# 6. Required governance files
required_gov = [
    ".agents/rules/mandatory-rules.md",
    ".agents/rules/agent-governance-policy.md",
    ".agents/rules/permission-enforcement.md",
    ".agents/rules/approval-gates.md",
    ".agents/rules/execution-boundary.md",
    ".agents/rules/security-rules.md",
    ".agents/docs/governance/metadata-standard.md",
    ".agents/docs/governance/adr-template.md"
]
for p in required_gov:
    if not check_exists(p):
        errors += 1
    else:
        if os.path.getsize(p) == 0:
            print(f"Error: {p} is empty")
            errors += 1

with open(".agents/rules/permission-enforcement.md", "r") as f:
    if ".agents/permissions/agent-permission-matrix.md" not in f.read():
        print("Error: permission-enforcement does not reference canonical matrix path")
        errors += 1

# 7. Memory checks
decisions_dir = ".agents/memory/decisions"
found_adrs = []
if os.path.exists(decisions_dir):
    found_adrs = [f for f in os.listdir(decisions_dir) if f.endswith(".md")]

with open(".agents/memory/catalog.json", "r") as f:
    catalog = json.load(f)
    ids = [e['id'] for e in catalog]
    if len(ids) != len(set(ids)): errors += 1

    catalog_adrs = []
    for entry in catalog:
        if not check_exists(entry['path']): errors += 1
        if type(entry.get('domains')) is not list or len(entry['domains']) == 0:
            print(f"Error: Empty domains for {entry['id']}")
            errors += 1
        if type(entry.get('triggers')) is not list or len(entry['triggers']) == 0:
            print(f"Error: Empty triggers for {entry['id']}")
            errors += 1
        summary = entry.get('summary', '')
        if not summary or summary.startswith("Architectural decision record for") or summary.startswith("Historical ADR"):
            print(f"Error: Meaningless summary for {entry['id']}")
            errors += 1

        if "decisions" in entry['path']:
            filename = os.path.basename(entry['path'])
            catalog_adrs.append(filename)
            with open(entry['path'], "r") as adr:
                content = adr.read()
                # Try to parse status flexibly
                source_status = None
                for line in content.split('\n'):
                    cleaned = line.strip().lstrip('- *').strip()
                    if cleaned.lower().startswith("status:"):
                        source_status = cleaned.split(":", 1)[1].strip().strip("*").strip()
                        break
                if source_status:
                    if entry['status'] != source_status:
                        print(f"Error: Status mismatch for {entry['id']}. Catalog: {entry['status']}, Source: {source_status}")
                        errors += 1
                else:
                    if entry['status'] != "Unspecified":
                        print(f"Error: ADR source {filename} lacks explicit Status, catalog must be 'Unspecified' but was '{entry['status']}'")
                        errors += 1

    if set(found_adrs) != set(catalog_adrs):
        print("Error: ADR files do not match catalog entries exactly.")
        errors += 1

if check_exists(".agents/memory/state/current.md"):
    with open(".agents/memory/state/current.md", "r") as f:
        content = f.read()
        if "\\n" in content:
            print("Error: literal \\n found in current.md")
            errors += 1

# 8. Active stale-reference scan
stale_terms = [
    "master-orchestrator-agent", "Master Orchestrator", "error-memory-agent", "Memory Agent",
    "api-integration-agent", "architecture-documentation-agent", "documentation-validator-agent",
    "enterprise-nodejs", "enterprise-dotnet", "devops-engineering", "domain-driven-design",
    "mysql-enterprise", "secure-development", "clean-code", "database-design"
]
active_dirs = [
    "AGENTS.md", ".agents/README.md", ".agents/agents", ".agents/skills",
    ".agents/routing", ".agents/rules", ".agents/workflows", ".agents/permissions"
]
for path in active_dirs:
    if os.path.isfile(path):
        files = [path]
    elif os.path.isdir(path):
        files = [os.path.join(dp, f) for dp, dn, filenames in os.walk(path) for f in filenames if f.endswith(('.md', '.json'))]
    else:
        continue

    for f_path in files:
        if "decisions" in f_path: continue
        with open(f_path, "r") as f:
            content = f.read()
            for term in stale_terms:
                if term in content:
                    print(f"Error: Stale reference '{term}' found in active file {f_path}")
                    errors += 1

# 9. Hook and approval gate checks
hooks_path = ".agents/hooks.json"
if not check_exists(hooks_path):
    errors += 1
else:
    try:
        with open(hooks_path, "r") as f:
            hooks_json = json.load(f)
        pre_tool_use = hooks_json.get("high-risk-gate", {}).get("PreToolUse", [])
        if not pre_tool_use:
            print("Error: PreToolUse not found in hooks.json")
            errors += 1
        else:
            matcher = pre_tool_use[0].get("matcher", "")
            if matcher == "*":
                print("Error: hook matcher must not be '*'")
                errors += 1
            authorized_tools = ["write_to_file", "replace_file_content", "multi_replace_file_content", "run_command"]
            for tool in authorized_tools:
                if tool not in matcher:
                    print(f"Error: hook matcher missing authorized tool '{tool}'")
                    errors += 1
    except Exception as e:
        print(f"Error: hooks.json failed to parse: {e}")
        errors += 1

approval_script = ".agents/permissions/check_approval.sh"
if not check_exists(approval_script):
    errors += 1
else:
    if os.path.getsize(approval_script) == 0:
        print("Error: check_approval.sh is empty")
        errors += 1

if errors > 0:
    print(f"Validation failed with {errors} errors.")
    sys.exit(1)
else:
    print("Governance validation passed.")
    sys.exit(0)
