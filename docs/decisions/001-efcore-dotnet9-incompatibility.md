# Decision: EF Core and .NET 9 Incompatibility

**Date:** 2025-08-11

## Context
- The project targets .NET 9 for all components.
- Entity Framework Core (EF Core) is the planned ORM for persistence.
- As of this date, the latest available EF Core version (10.0.0-preview.6) only supports .NET 10.0 and is not compatible with .NET 9.0.
- No stable or prerelease EF Core package is available for .NET 9 on public NuGet feeds.

## Decision
- Document this incompatibility as a critical architectural constraint.
- Recommend one of the following paths:
  1. **Target .NET 8 (LTS):** Downgrade all projects to .NET 8, which is fully supported by EF Core 8.x and other ecosystem tools.
  2. **Wait for EF Core support for .NET 9/10:** Pause persistence implementation until EF Core is available for .NET 9 or upgrade to .NET 10 preview when stable.
- For now, persistence code and interfaces are scaffolded, but implementation and integration are blocked until a compatible EF Core version is released or the target framework is changed.

## Consequences
- Persistence and database integration cannot proceed on .NET 9 until EF Core support is available.
- All other architectural and domain work can continue, but DB-backed features will remain unimplemented.

---
*This decision should be revisited when new EF Core or .NET versions are released.*
