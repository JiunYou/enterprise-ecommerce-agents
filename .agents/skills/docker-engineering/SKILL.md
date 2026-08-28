---
name: docker-engineering
description: Use when creating or modifying Dockerfiles, docker-compose, and container orchestration.
---
# Docker Engineering Skill

## Guidance
- Use multi-stage builds to optimize image size.
- Adhere to the principle of least privilege (run as non-root).
- Organize layers to maximize caching.
- Securely pass secrets during build and runtime.
- Maintain a `.dockerignore` file.

## Boundaries / Validation
Must adhere to enterprise container security guidelines.
