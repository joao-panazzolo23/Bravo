# Token Efficiency Guidelines

## 1. Core Principle

Minimize token consumption, tool calls, context usage, and unnecessary execution without compromising correctness.

**Efficiency is a development requirement, not an optional optimization.**

Do not perform work that does not contribute directly to the requested task.

## 2. Context Management

- Never read the entire repository unless explicitly necessary.
- Start by inspecting the relevant project structure.
- Search for specific classes, methods, interfaces, and references.
- Read only the files and sections required to understand the task.
- Avoid reopening files already inspected unless their contents may have changed.
- Do not repeatedly rediscover information already available in the conversation.
- Do not load unrelated documentation.
- Read additional guidelines only when they are relevant to the current task.

Prefer targeted searches over broad directory exploration.

## 3. Implementation Efficiency

- Make the smallest change that correctly solves the problem.
- Modify existing implementations when appropriate instead of creating alternatives.
- Avoid unnecessary classes, interfaces, methods, abstractions, and dependencies.
- Do not introduce generic solutions for isolated problems.
- Do not refactor unrelated code.
- Do not rewrite entire files when a localized modification is sufficient.
- Reuse existing infrastructure and established project conventions.
- Avoid speculative improvements and premature optimizations.

Do not confuse more code with better engineering.

## 4. Tool Usage

- Execute only commands necessary for the current task.
- Avoid repeating successful commands without a specific reason.
- Prefer targeted build and test commands.
- Never dump entire directory trees, logs, or configuration files unnecessarily.
- Filter command output to the relevant information.
- Avoid running multiple equivalent searches.
- Do not inspect dependencies or framework internals unless required.

If a command fails, inspect its error before deciding what to do next.

Never repeat a failing operation without changing the underlying conditions.

## 5. Build and Testing

Follow `TESTING_GUIDELINES.md`.

- Test only affected functionality.
- Build only affected projects when possible.
- Compile before executing tests.
- Avoid unnecessary full-solution builds.
- Do not create tests without meaningful behavioral value.
- Stop testing once the required verification is successful.

Do not spend tokens investigating unrelated pre-existing failures.

## 6. Scope Control

Strictly respect the user's requested scope.

- Do not implement additional features.
- Do not anticipate requirements that were not requested.
- Do not perform unrelated cleanup.
- Do not rename or reorganize unrelated components.
- Do not change public contracts unless required.
- Do not modify production configuration unnecessarily.

If an additional issue is discovered, report it instead of automatically fixing it.

## 7. Reasoning and Communication

- Keep progress updates concise.
- Do not narrate every inspection or tool invocation.
- Avoid repeating the user's requirements.
- Do not explain obvious implementation details.
- Do not produce lengthy summaries of unchanged code.
- Report only relevant decisions, changes, and verification results.

Think through the implementation before executing commands.

Avoid exploring multiple approaches when an existing project convention already provides a clear solution.

## 8. Failure Recovery

When encountering an error:

1. Read the relevant error output.
2. Identify the underlying cause.
3. Inspect only the components involved.
4. Apply the smallest appropriate fix.
5. Retry the affected operation once the cause is addressed.

Do not restart the entire workflow unless necessary.

Do not repeatedly attempt different solutions without evidence.

If blocked, report the blocker and request clarification when necessary.

## 9. Completion Criteria

A task is complete when:

- The requested functionality is implemented.
- Relevant compilation succeeds.
- Required tests pass, when applicable.
- No unrelated modifications were introduced.

Once these conditions are satisfied, STOP.

Do not continue exploring, optimizing, refactoring, or testing without a concrete reason.

## 10. Mandatory Rules

1. Minimum necessary context.
2. Minimum necessary file modifications.
3. Minimum necessary tool invocations.
4. Minimum necessary tests.
5. No unrelated refactoring.
6. No speculative implementation.
7. No repetitive investigation.
8. No unnecessary explanations.
9. No work beyond the requested scope.

**Optimize for the smallest correct solution, not the largest possible implementation.**
