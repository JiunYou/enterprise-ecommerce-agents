import os
import json
import sys
import glob

def check_exists(path):
    if not os.path.exists(path):
        print(f"Error: {path} is missing.")
        return False
    return True

errors = 0

# Root contract
if not check_exists("AGENTS.md"):
    errors += 1
else:
    size = os.path.getsize("AGENTS.md")
    print(f"AGENTS.md size: {size} bytes")

expected_agents = ["orchestrator", "product-manager", "system-architect", "architecture-reviewer",
    "domain-architect", "dotnet-backend", "nodejs-backend", "frontend", "database",
    "devops", "qa", "security", "compliance", "documentation-reviewer", "release"]

# Agents check
found_agents = [d for d in os.listdir(".agents/agents") if os.path.isdir(os.path.join(".agents/agents", d))]
if set(found_agents) != set(expected_agents):
    print("Error: Agent directories do not match canonical 15 exactly.")
    print("Found:", found_agents)
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
            if "### From" in content:
                print(f"Error: merge artifact found in {agent_file}")
                errors += 1
            frontmatter_count = content.count("---") // 2
            if frontmatter_count != 1:
                print(f"Error: Expected 1 frontmatter block, found {frontmatter_count} in {agent_file}")
                errors += 1

            # generic description check
            if "Handles" in content and "tasks and responsibilities" in content:
                print(f"Error: Generic description in {agent_file}")
                errors += 1

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
            frontmatter_count = content.count("---") // 2
            if frontmatter_count != 1:
                print(f"Error: Expected 1 frontmatter block in {skill_file}")
                errors += 1
            if "Capability for" in content:
                print(f"Error: Generic description in {skill_file}")
                errors += 1

# Routing JSON
with open(".agents/routing/agents.json", "r") as f:
    agents_json = json.load(f)
    if agents_json.get("entrypoint") != "orchestrator": errors += 1
    if agents_json.get("max_handoff_depth") > 2: errors += 1
    names = [a['id'] for a in agents_json['agents']]
    if set(names) != set(expected_agents): errors += 1

with open(".agents/routing/skills.json", "r") as f:
    skills_json = json.load(f)
    for agent in expected_agents:
        if agent not in skills_json["routing"]:
            print(f"Error: Agent {agent} missing from skills.json routing")
            errors += 1

# Memory
with open(".agents/memory/catalog.json", "r") as f:
    catalog = json.load(f)
    ids = [e['id'] for e in catalog]
    if len(ids) != len(set(ids)): errors += 1

    for entry in catalog:
        if not check_exists(entry['path']): errors += 1
        if type(entry.get('domains')) is not list: errors += 1
        if type(entry.get('triggers')) is not list: errors += 1
        if not entry.get('summary'): errors += 1

        # Verify status matches ADR source
        if "decisions" in entry['path']:
            with open(entry['path'], "r") as adr:
                content = adr.read()
                # find Status:
                for line in content.split('\n'):
                    if line.lower().startswith("status:"):
                        source_status = line.split(":", 1)[1].strip().strip("*").strip()
                        if entry['status'].lower() != source_status.lower():
                            print(f"Error: Status mismatch for {entry['id']}. Catalog: {entry['status']}, Source: {source_status}")
                            errors += 1
                        break

with open(".agents/memory/state/current.md", "r") as f:
    content = f.read()
    if "\\n" in content:
        print("Error: literal \\n found in current.md")
        errors += 1

if errors > 0:
    print(f"Validation failed with {errors} errors.")
    sys.exit(1)
else:
    print("Governance validation passed.")
    sys.exit(0)
