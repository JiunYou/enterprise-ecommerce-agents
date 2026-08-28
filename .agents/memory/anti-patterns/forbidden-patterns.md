# Forbidden Patterns

## Example: Domain Event Serialization

**Problem:**
Infrastructure directly serializes Domain Events.

**Impact:**
Breaks integration boundary.

**Correct Pattern:**
Domain Event -> Integration Event Mapper -> Publisher
