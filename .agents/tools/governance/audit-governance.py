import os
import json
import sys

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

# Routing
if not check_exists(".agents/routing/agents.json"): errors += 1
if not check_exists(".agents/routing/skills.json"): errors += 1

# Agents
expected_agents = ["orchestrator", "product-manager", "system-architect", "architecture-reviewer",
    "domain-architect", "dotnet-backend", "nodejs-backend", "frontend", "database",
    "devops", "qa", "security", "compliance", "documentation-reviewer", "release"]

if os.path.exists(".agents/routing/agents.json"):
    with open(".agents/routing/agents.json") as f:
        data = json.load(f)
        names = [a['id'] for a in data['agents']]
        if set(names) != set(expected_agents):
            print("Error: Agent names in agents.json do not match expected list.")
            errors += 1

for agent in expected_agents:
    if not check_exists(f".agents/agents/{agent}/agent.md"):
        errors += 1

# Skills
expected_skills = [
    "api-security", "code-review", "ddd", "devops", "docker-engineering", "dotnet",
    "ecommerce-domain", "ecommerce-security", "external-api-integration", "frontend-engineering",
    "git-workflow", "javascript-engineering", "mysql", "nextjs", "nodejs", "rabbitmq",
    "rest-api-design", "security", "software-architecture", "system-modeling", "testing", "threat-modeling"
]
for skill in expected_skills:
    if not check_exists(f".agents/skills/{skill}/SKILL.md"):
        errors += 1

# Memory
if not check_exists(".agents/memory/catalog.json"): errors += 1
if not check_exists(".agents/memory/state/current.md"): errors += 1

if errors > 0:
    print(f"Validation failed with {errors} errors.")
    sys.exit(1)
else:
    print("Governance validation passed.")
    sys.exit(0)
