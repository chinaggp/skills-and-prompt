---
name: phase-model-routing
description: Use when a software development task needs phase-based model routing, model selection, model switch, subagent coding, requirement analysis, solution design, code changes, bug fixes, refactors, tests, build fixes, or Chinese requests such as 分析阶段、编码阶段、代码实现、子代理、需求分析、方案设计.
---

# Phase Model Routing

Use this skill to choose the model by delivery phase rather than by business domain.

Keep the skill narrow. It does not define a full delivery process. It only decides which model should own analysis and which model should own coding.

Core routing:

- Analysis phase: keep ownership on `gpt-5.5`.
- Coding phase: use an implementation subagent with `model: "gpt-5.4-mini"`.

## Phase Selection

Choose `gpt-5.5` for analysis phase work:

- Clarifying ambiguous requirements
- Reading code, configs, logs, or docs to build understanding
- Identifying constraints, risks, regressions, or edge cases
- Comparing approaches and recommending a design
- Breaking work into tasks before implementation
- Reviewing whether a reported issue is real and where it likely lives

Choose an implementation subagent with `model: "gpt-5.4-mini"` for coding phase work:

- Editing or generating code after the direction is clear
- Adding or adjusting tests for an agreed implementation
- Fixing compile, build, lint, or straightforward runtime issues
- Applying targeted refactors with already-known intent
- Completing verification and cleanup for an implementation already in progress

## Boundary Rules

Treat the request as analysis phase when the main unknown is "what should change" or "how should this be implemented."

Treat the request as coding phase when the main unknown is no longer the direction, and the task is primarily to execute the chosen change safely.

If a request contains both analysis and implementation, split the work:

1. Start with `gpt-5.5` to resolve the unclear part.
2. Use an implementation subagent with `model: "gpt-5.4-mini"` once the implementation target is concrete.
3. Return to `gpt-5.5` only if new ambiguity, risk, or design conflict appears.

## Default Strategy

If the phase is unclear, default to analysis with `gpt-5.5`.

Use the minimum analysis needed to remove uncertainty. Do not stay in analysis mode after the implementation path is already clear.

## Execution Rules

This skill is only useful if the model transition is explicit and verifiable.

When the task is in analysis phase, keep the work on `gpt-5.5`.

When the task enters coding or implementation phase, you MUST delegate the implementation work to a subagent created with:

- `spawn_agent`
- `model: "gpt-5.4-mini"`

The implementation subagent owns code edits, test edits, targeted refactors, build fixes, and verification for the concrete implementation target.

Before starting substantial work in a phase, you MUST explicitly announce the active phase and model in the response.

Use this exact format:

- `Current phase: analysis, model: gpt-5.5`
- `Current phase: implementation, model: gpt-5.4-mini, owner: subagent`

Do not claim that a model switch happened unless the implementation agent was created with an explicit `model` field.

If implementation work reveals new ambiguity, design conflict, or significant risk, return analysis ownership to `gpt-5.5` and explicitly announce the switch before continuing.

If platform policy or tool availability blocks subagent creation, do not pretend that the coding phase used `gpt-5.4-mini`. In that case, state that the task stayed on the current agent and was not phase-switched.

## Examples

Use `gpt-5.5` first:

- "Analyze how this requirement should be changed"
- "Read the code first, propose a solution, then implement after confirmation"
- "Find where this null pointer is actually coming from"

Use a `gpt-5.4-mini` implementation subagent once direction is settled:

- "Implement the agreed API change and add tests"
- "Fix this null pointer in the interface"
- "Finish this confirmed refactor and run verification"

For mixed requests such as "analyze first, then implement directly", begin with `gpt-5.5`, summarize the chosen implementation direction, then continue through a `gpt-5.4-mini` implementation subagent.

Verifiable mixed-flow example:

1. Announce `Current phase: analysis, model: gpt-5.5`
2. Resolve the unclear part and summarize the implementation target
3. Create an implementation subagent with `spawn_agent(... model="gpt-5.4-mini" ...)`
4. Announce `Current phase: implementation, model: gpt-5.4-mini, owner: subagent`
5. If new ambiguity appears, return to `gpt-5.5` and announce that switch explicitly
