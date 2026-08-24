#!/usr/bin/env python3
import os
import sys

AGENTS_DIR = ".agents"

def check_permission_matrix():
    path = os.path.join(AGENTS_DIR, "permissions", "agent-permission-matrix.md")
    if not os.path.exists(path):
        return False, "Agent permission matrix missing."
    return True, "Agent permission matrix found."

def check_rules():
    rules = [
        "mandatory-rules.md",
        "agent-governance-policy.md",
        "permission-enforcement.md"
    ]
    for rule in rules:
        if not os.path.exists(os.path.join(AGENTS_DIR, "rules", rule)):
            return False, f"Rule missing: {rule}"
    return True, "Rules found."

def check_metadata():
    path = os.path.join(AGENTS_DIR, "docs", "governance", "metadata-standard.md")
    if not os.path.exists(path):
        return False, "Metadata standard missing."
    return True, "Metadata standard found."

def check_adr_format():
    path = os.path.join(AGENTS_DIR, "docs", "governance", "adr-template.md")
    if not os.path.exists(path):
        return False, "ADR template missing."
    return True, "ADR template found."

def check_memory_structure():
    dirs = ["incidents", "patterns", "solutions", "decisions"]
    for d in dirs:
        if not os.path.isdir(os.path.join(AGENTS_DIR, "memory", d)):
            return False, f"Memory structure missing: {d}"
    return True, "Memory structure found."

def check_agents_skills():
    agents_dir = os.path.join(AGENTS_DIR, "agents")
    skills_dir = os.path.join(AGENTS_DIR, "skills")
    
    required_mappings = {
        "frontend-agent.md": ["frontend-engineering", "javascript-engineering"],
        "dotnet-backend-agent.md": ["enterprise-dotnet"],
        "nodejs-backend-agent.md": ["enterprise-nodejs"],
        "mysql-database-agent.md": ["mysql-enterprise"],
        "api-integration-agent.md": ["rest-api-design"],
        "devops-agent.md": ["docker-engineering", "devops-engineering"]
    }
    
    for agent, skills in required_mappings.items():
        if not os.path.exists(os.path.join(agents_dir, agent)):
            return False, f"Agent missing: {agent}"
        for skill in skills:
            if not os.path.exists(os.path.join(skills_dir, skill)):
                return False, f"Skill missing: {skill} for agent {agent}"
                
    return True, "All Agents have corresponding Skills, including Docker Capability."

def check_architecture_capability():
    agents_dir = os.path.join(AGENTS_DIR, "agents")
    skills_dir = os.path.join(AGENTS_DIR, "skills")
    
    if not os.path.exists(os.path.join(agents_dir, "domain-architect-agent.md")):
        return False, "domain-architect-agent.md missing."
        
    required_skills = ["domain-driven-design", "system-modeling", "threat-modeling"]
    for skill in required_skills:
        if not os.path.exists(os.path.join(skills_dir, skill, "SKILL.md")):
            return False, f"Skill missing: {skill}"
            
    security_agent_path = os.path.join(agents_dir, "security-review-agent.md")
    if os.path.exists(security_agent_path):
        with open(security_agent_path, "r", encoding="utf-8") as f:
            content = f.read()
            if "threat-modeling" not in content or "ecommerce-security" not in content or "api-security" not in content:
                return False, "Security Agent is missing required skills."
                
    return True, "Architecture Capability Check complete (Agent, Skill, Workflow)."

def run_audit():
    checks = [
        check_permission_matrix,
        check_rules,
        check_metadata,
        check_adr_format,
        check_memory_structure,
        check_agents_skills,
        check_architecture_capability
    ]
    
    all_passed = True
    print("Running AI Governance Audit...\n")
    for check in checks:
        passed, msg = check()
        if passed:
            print(f"[PASS] {msg}")
        else:
            print(f"[FAIL] {msg}")
            all_passed = False
            
    print("\n===============================")
    if all_passed:
        print("AUDIT RESULT: PASS")
        sys.exit(0)
    else:
        print("AUDIT RESULT: FAIL")
        sys.exit(1)

if __name__ == "__main__":
    if not os.path.exists(AGENTS_DIR):
        print(f"[FAIL] .agents directory not found in {os.getcwd()}")
        sys.exit(1)
    run_audit()
