# Orchestrator Routing Rules

## Domain Change
**Input Example:** "Modify Order Aggregate"
**Route:** Domain Agent -> Architecture Agent -> QA Agent -> Validation Agent

## Feature Development
**Input Example:** "Add checkout feature"
**Route:** Orchestrator -> Architecture Agent -> Backend Agent -> Frontend Agent -> QA Agent -> Security Agent -> Validation Agent

## Bug Fix
**Input Example:** "Fix checkout crash"
**Route:** Diagnosis -> Relevant Developer Agent -> QA -> Security -> Validation
