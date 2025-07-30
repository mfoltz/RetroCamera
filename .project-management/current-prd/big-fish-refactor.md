# Big Fish Refactor

## Problem Statement

Large portions of the repository deviate from the coding standards defined in `AGENTS.md`. The most notable issues include:

- Private fields use an underscore prefix instead of the camelCase style described in `AGENTS.md`.
- Methods grow longer than 30 lines, violating readability recommendations.
- Lack of XML documentation comments for public APIs.
- Inconsistent naming across files and excessive commented-out code in patches.

These inconsistencies make the project harder to maintain and extend.

## Objectives

1. Align naming conventions with `AGENTS.md`.
2. Break up lengthy methods for better readability.
3. Introduce XML comments for all public members.
4. Remove obsolete or commented code while preserving functionality.

## Proposed Features

- **Field Naming Update**: Rename private and static fields across the project to camelCase without the underscore prefix.
- **Method Refactors**: Split complex methods such as those in `Systems/RetroCamera.cs` and `Patches/MainMenuPatch.cs` into smaller functions that obey the Single Responsibility Principle.
- **Documentation Pass**: Add XML comments describing purpose, parameters, and return values for public classes and methods.
- **Clean-up Pass**: Remove commented-out sections that are no longer used and ensure each class has a clear responsibility.

## Acceptance Criteria

- All private fields follow camelCase naming with no underscore prefix.
- Methods do not exceed ~30 lines unless absolutely necessary and contain clear responsibilities.
- Every public class and method has an XML comment.
- Code builds successfully with .NET 6 and retains existing functionality.

## Timeline

- **Week 1**: Audit the codebase, identify violations, and plan refactoring steps.
- **Week 2**: Rename fields and update references. Begin splitting large methods.
- **Week 3**: Complete method refactors and add XML comments.
- **Week 4**: Final clean up, run linting/build, and verify gameplay functionality.

## Stakeholders

- Repository maintainers
- Community contributors and testers

## Risks

- Widespread renaming may introduce regressions if not carefully tested.
- Refactoring large methods could inadvertently change game behavior.

