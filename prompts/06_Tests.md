# Testing Guidelines

## 1. Core Principle

Tests must provide meaningful confidence in the correctness of the application.

**Do not write tests just to increase test coverage or because a class exists.**

Prioritize correctness, maintainability, execution speed, and meaningful behavioral verification.

## 2. What Should Be Tested

Prioritize the following:

### High Priority

- Business rules and domain logic.
- Complex calculations and data transformations.
- Application use cases and command/query handlers.
- Data validation and boundary conditions.
- Import processes, especially checkpointing, retries, and failure recovery.
- Critical integration points between application components.
- Error handling and unexpected input scenarios.

### Medium Priority

- Repository implementations when they contain meaningful query logic.
- External API integrations.
- Serialization and deserialization.
- Mapping logic when transformations are non-trivial.

### Low Priority / Usually Not Necessary

- Simple DTOs and data containers.
- Properties with no custom logic.
- Trivial constructors.
- Simple getters and setters.
- Framework-generated behavior.
- Thin wrappers that contain no meaningful logic.
- Every possible CRUD operation when the behavior is already covered at a more relevant level.

Do not create tests for these components unless there is a concrete reason.

## 3. Test Scope

Follow these rules strictly:

1. Test the functionality being implemented or modified.
2. Test directly affected components when necessary.
3. Do not create tests for unrelated existing functionality.
4. Do not attempt to test the entire application after every change.
5. Avoid duplicating coverage across multiple test layers.
6. Prefer a small number of meaningful tests over dozens of redundant ones.

Expand the testing scope only when a change introduces significant cross-component impact.

## 4. Test Execution Workflow

Follow this sequence:

1. Identify the existing test projects and testing conventions.
2. Identify which components are affected by the implementation.
3. Determine which behaviors actually require verification.
4. Create only the necessary tests.
5. Compile the affected test project.
6. Fix compilation errors before attempting execution.
7. Execute the relevant tests.
8. Report the results.

**Never execute tests before ensuring that the test project compiles.**

If compilation fails:

- Inspect the actual compiler errors.
- Determine whether the problem is in the test or production code.
- Fix the underlying issue without introducing unrelated changes.
- Rebuild before proceeding.

Never repeatedly execute a failing test suite without understanding the failure.

## 5. Test Architecture

- Keep tests in dedicated test projects.
- Follow the existing project structure and naming conventions.
- Keep each test focused on a single behavior.
- Use descriptive test names.
- Avoid putting multiple unrelated test classes in one file.
- Never introduce testing dependencies without a clear justification.
- Avoid unnecessary abstractions, base test classes, and helper frameworks.

Use mocks only when an actual external dependency or isolation boundary requires them.

Prefer real domain objects and simple test data over excessive mocking.

## 6. Regression Testing

When fixing a bug, create a regression test when the bug involves meaningful application behavior.

The test should:

- Reproduce the original failure.
- Verify the expected behavior after the fix.
- Avoid depending on implementation details.

Do not create regression tests for trivial changes that introduce no meaningful behavioral risk.

## 7. Integration Tests

Integration tests should be used when correctness depends on actual interaction between components.

Examples:

- Database persistence and queries.
- External API communication.
- Dependency injection configuration.
- Application startup and endpoint behavior.

Do not turn every unit test into an integration test.

Avoid requiring external services, production credentials, or manually configured infrastructure unless explicitly necessary.

## 8. Token and Execution Efficiency

Testing must be resource-conscious.

- Do not run the entire test suite unnecessarily.
- Do not generate large volumes of repetitive tests.
- Do not repeatedly inspect unrelated files.
- Do not regenerate tests that already provide sufficient coverage.
- Reuse existing test infrastructure and conventions.
- Prefer targeted build and test commands.

If a test project contains unrelated compilation failures, identify and report them instead of expanding the scope of the current task.

## 9. Final Report

After testing, provide a concise report containing:

- What was tested.
- Which test projects were compiled.
- Which tests were executed.
- Whether compilation succeeded.
- Whether tests passed or failed.
- Any known failures unrelated to the current implementation.

Never claim that tests passed if they were not executed successfully.

## 10. Mandatory Rules

1. Correctness is more important than test coverage percentage.
2. Test behavior, not implementation details.
3. Test only what is relevant.
4. Compile before executing.
5. Never modify production code just to accommodate a poorly designed test.
6. Do not introduce unnecessary testing infrastructure.
7. Keep tests isolated, readable, and maintainable.
8. When uncertain whether a test is worthwhile, evaluate its actual value before creating it.

**The objective is not to maximize the number of tests. The objective is to maximize confidence while minimizing unnecessary complexity and execution cost.**
