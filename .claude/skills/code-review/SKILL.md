
---
name: code-review
description: Review code for duplication, consistency, maintainability, architectural seams, testability, security, and adherence to existing project patterns. Use when asked to review, audit, or evaluate code quality.
---

# Code Quality Review

Review the requested code for quality, consistency, maintainability, and architectural integrity.

The goal is to keep the codebase clean, modular, testable, and easy to extend without introducing unnecessary complexity.

Review the existing architecture and conventions before making recommendations.

## 1. Duplication and Reuse

- Identify duplicated or substantially similar logic.
- Search the existing codebase for methods, functions, classes, or utilities that already provide the same functionality.
- Identify functionality that has been implemented multiple times in slightly different ways.
- Recommend reusing or consolidating existing functionality where appropriate.
- Do not recommend creating new methods or abstractions when existing ones adequately solve the problem.
- Avoid consolidating code that is only superficially similar but serves different responsibilities.

## 2. Naming Consistency

- Check naming conventions for methods, functions, classes, variables, interfaces, and files.
- Follow conventions already established in the project.
- Identify misleading, ambiguous, or unnecessarily abbreviated names.
- Check that names accurately describe responsibilities and behavior.
- Identify inconsistent terminology for the same concept.
- Avoid recommending renaming solely for stylistic preference.

## 3. Maintainability and Simplicity

- Identify unnecessarily complex methods or functions.
- Look for unused code, imports, variables, and dependencies.
- Identify excessive nesting or complicated conditional logic.
- Look for magic numbers and hardcoded values that should be named constants or configuration.
- Identify opportunities to simplify logic without changing behavior.
- Prefer straightforward solutions over clever or overly abstract implementations.
- Avoid introducing abstractions without a clear, practical benefit.
- Identify code that is difficult to understand or modify safely.

## 4. Error Handling

- Check for missing, inconsistent, or inappropriate error handling.
- Identify swallowed exceptions and silent failures.
- Check that errors provide useful diagnostic information.
- Identify cases where failures could leave the application in an inconsistent state.
- Check that recovery and fallback behavior is appropriate.
- Avoid recommending excessive defensive programming for impossible or already-handled conditions.

## 5. Security

- Identify potential security vulnerabilities.
- Check input validation and sanitization where appropriate.
- Identify unsafe handling of user-controlled data.
- Check for exposed secrets, credentials, or sensitive information.
- Identify insecure assumptions about trusted inputs.
- Check that authorization and access restrictions are enforced where applicable.
- Prioritize realistic vulnerabilities over speculative concerns.

## 6. Architecture and Separation of Concerns

- Check consistency with established architectural patterns.
- Identify components with too many unrelated responsibilities.
- Look for inappropriate dependencies between unrelated systems.
- Check that business logic is separated from UI, persistence, and infrastructure concerns where practical.
- Identify circular dependencies.
- Evaluate whether changes are appropriately localized.
- Identify situations where modifying one feature requires changes to unrelated systems.
- Prefer existing interfaces, services, utilities, and architectural patterns.
- Do not recommend restructuring working code solely to satisfy theoretical design principles.

## 7. Testing and Test Coverage

- Identify important behavior that lacks adequate test coverage.
- Check that tests cover meaningful edge cases and failure scenarios.
- Identify tests that are overly coupled to implementation details.
- Check whether tests are deterministic and repeatable.
- Identify dependencies that make automated testing unnecessarily difficult.
- Recommend practical tests that protect existing behavior.
- Avoid recommending tests that provide little meaningful value.

## 8. Architectural Seams and Testability

A seam is a boundary where one component or implementation can be changed, replaced, or tested without requiring changes to unrelated components.

Evaluate whether the architecture provides appropriate seams between systems.

### Dependency Management

- Identify tightly coupled components.
- Check whether external dependencies can be substituted, mocked, or replaced for testing.
- Identify hardcoded dependencies that make testing or replacement difficult.
- Prefer existing dependency injection and interface patterns.
- Avoid introducing interfaces or abstractions solely for hypothetical future requirements.

### Component Boundaries

- Check that components have clear responsibilities.
- Identify systems that directly manipulate another system's internal state.
- Look for dependencies on implementation details rather than established contracts.
- Check whether components communicate through appropriate interfaces or boundaries.
- Identify unnecessary knowledge of unrelated systems.

### Change Isolation

- Evaluate whether a change to one feature can be implemented without modifying unrelated features.
- Identify changes that would require coordinated modifications across multiple unrelated components.
- Look for shared logic that creates unintended coupling.
- Identify opportunities to isolate frequently changing behavior.
- Recommend seams where they provide clear benefits for maintainability, testing, or future development.

### Testability

- Check whether business logic can be tested independently of UI and infrastructure.
- Identify components that require extensive setup or unrelated systems to run tests.
- Check whether nondeterministic dependencies such as time, randomness, file systems, and external services can be controlled during testing.
- Identify opportunities to test components independently.
- Prefer lightweight, practical testing seams over elaborate mocking frameworks.

## 9. State Management and Data Integrity

- Identify duplicated or conflicting sources of truth.
- Check that state ownership is clear.
- Look for unexpected mutations or side effects.
- Identify cases where state changes could leave related systems inconsistent.
- Check that persistence and state transitions follow established patterns.
- Identify potential race conditions where concurrency is relevant.
- Avoid introducing unnecessary state management infrastructure.

## 10. Extensibility

- Identify areas where adding new functionality would require modifying numerous existing components.
- Check whether existing extension points are being used consistently.
- Identify hardcoded assumptions that unnecessarily restrict future functionality.
- Evaluate whether new features can be added without destabilizing existing systems.
- Prefer practical extensibility based on actual project needs.
- Do not recommend speculative abstractions for features that may never exist.

## 11. Performance and Resource Management

- Identify obvious performance bottlenecks.
- Look for unnecessary repeated calculations, allocations, or data retrieval.
- Identify resources that are not properly released or disposed.
- Check for unnecessary work inside frequently executed code paths.
- Consider memory usage and responsiveness where relevant.
- Do not recommend micro-optimizations without a meaningful benefit.
- Distinguish demonstrated performance problems from potential concerns requiring measurement.

## 12. Consistency With Existing Code

- Compare implementations against established project patterns.
- Identify cases where similar functionality uses unnecessarily different approaches.
- Check whether existing utilities and shared components are being used appropriately.
- Identify deviations from project conventions that create maintenance costs.
- Do not recommend changing established conventions without a compelling reason.

# Review Process

Follow this process when performing a review.

1. Determine the requested review scope.
2. Inspect relevant project structure and existing conventions.
3. Search for existing implementations before identifying duplication or recommending new abstractions.
4. Examine the relevant code and its dependencies.
5. Evaluate the code against the review categories above.
6. Inspect relevant tests where available.
7. Identify concrete issues supported by code evidence.
8. Prioritize findings by severity and practical impact.
9. Recommend the simplest effective solution consistent with existing architecture.

Do not assume a file is unused or functionality is duplicated without checking references and usage.

Do not claim a system lacks a seam without examining the relevant component boundaries.

If the review scope prevents verification, clearly identify the limitation.

# Review Output

Organize findings by severity:

- **Critical:** Security vulnerabilities, data loss risks, or defects that could cause catastrophic failures.
- **High:** Significant correctness, architectural, reliability, or maintainability problems.
- **Medium:** Issues that create meaningful technical debt, unnecessary coupling, or future maintenance difficulties.
- **Low:** Minor inconsistencies, cleanup opportunities, or small improvements.

For each finding, provide:

1. **Location:** File path and line number where possible.
2. **Issue:** A concise description of the problem.
3. **Impact:** Why it matters.
4. **Recommendation:** A specific, practical improvement.
5. **Severity:** Critical, High, Medium, or Low.

## Reporting Rules

- Report actionable findings rather than generic advice.
- Do not invent issues to populate every category.
- Do not repeat the same underlying issue across multiple categories.
- Distinguish confirmed problems from potential risks.
- Prioritize root causes over symptoms.
- Explain trade-offs when a recommendation introduces additional complexity.
- Group related findings when they share the same root cause.
- If no meaningful issues are found, say so.
- Include a brief overall assessment of code quality and architectural health.
- Prioritize findings that improve change isolation, reduce duplication, or prevent future regressions.

# Restrictions

- Do not modify code unless explicitly requested.
- Do not automatically refactor code during a review.
- Do not introduce new dependencies without justification.
- Do not recommend large-scale rewrites when targeted improvements are sufficient.
- Do not prioritize theoretical architectural purity over practical maintainability.
- Do not recommend abstractions merely because they are common design patterns.
- Preserve existing behavior when recommending improvements.
- Prefer fixing root causes rather than adding workarounds for symptoms.
- Follow existing project conventions unless there is a concrete reason not to.

# Guiding Principle

Good architecture makes changes predictable.

A well-designed system should allow developers to modify, test, extend, or replace one component without unnecessarily affecting unrelated components.

Favor simplicity, reuse, clear responsibilities, appropriate seams, and localized changes over complexity and abstraction for its own sake.
