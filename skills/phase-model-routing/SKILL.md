---
name: phase-model-routing
description: Route general software development work by phase so analysis tasks use GPT-5.4 and implementation tasks use GPT-5.4-mini. Use when handling requirement analysis, solution design, code changes, bug fixes, refactors, test updates, build fixes, or mixed requests that move from understanding into implementation.
---

# Phase Model Routing

Use this skill to choose the model by delivery phase rather than by business domain.

Keep the skill narrow. It does not define a mandatory delivery process. It only decides which model should own the current phase of work.

## Phase Selection

Choose `gpt-5.4` for analysis phase work:

- Clarifying ambiguous requirements
- Reading code, configs, logs, or docs to build understanding
- Identifying constraints, risks, regressions, or edge cases
- Comparing approaches and recommending a design
- Breaking work into tasks before implementation
- Reviewing whether a reported issue is real and where it likely lives

Choose `gpt-5.4-mini` for coding phase work:

- Editing or generating code after the direction is clear
- Adding or adjusting tests for an agreed implementation
- Fixing compile, build, lint, or straightforward runtime issues
- Applying targeted refactors with already-known intent
- Completing verification and cleanup for an implementation already in progress

## Boundary Rules

Treat the request as analysis phase when the main unknown is "what should change" or "how should this be implemented."

Treat the request as coding phase when the main unknown is no longer the direction, and the task is primarily to execute the chosen change safely.

If a request contains both analysis and implementation, split the work:

1. Start with `gpt-5.4` to resolve the unclear part.
2. Switch to `gpt-5.4-mini` once the implementation target is concrete.
3. Return to `gpt-5.4` only if new ambiguity, risk, or design conflict appears.

## Default Strategy

If the phase is unclear, default to analysis with `gpt-5.4`.

Use the minimum analysis needed to remove uncertainty. Do not stay in analysis mode after the implementation path is already clear.

## Execution Rules

This skill is only useful if the model transition is explicit and verifiable.

When the task is in analysis phase, keep the work on `gpt-5.4`.

When the task enters implementation phase, you MUST delegate the implementation work to a subagent created with:

- `spawn_agent`
- `model: "gpt-5.4-mini"`

Before starting substantial work in a phase, you MUST explicitly announce the active phase and model in the response.

Use this exact format:

- `Current phase: analysis, model: gpt-5.4`
- `Current phase: implementation, model: gpt-5.4-mini`

Do not claim that a model switch happened unless the implementation agent was created with an explicit `model` field.

If implementation work reveals new ambiguity, design conflict, or significant risk, return analysis ownership to `gpt-5.4` and explicitly announce the switch before continuing.

If the implementation is tiny and no subagent is created, do not pretend that the switch occurred. In that case, state that the task stayed on the current agent and was not phase-switched.

## Examples

Use `gpt-5.4` first:

- "Analyze how this requirement should be changed"
- "Read the code first, propose a solution, then implement after confirmation"
- "Find where this null pointer is actually coming from"

Use `gpt-5.4-mini` once direction is settled:

- "Implement the agreed API change and add tests"
- "Fix this null pointer in the interface"
- "Finish this confirmed refactor and run verification"

For mixed requests such as "analyze first, then implement directly", begin with `gpt-5.4`, summarize the chosen implementation direction, then continue with `gpt-5.4-mini`.

Verifiable mixed-flow example:

1. Announce `Current phase: analysis, model: gpt-5.4`
2. Resolve the unclear part and summarize the implementation target
3. Create an implementation subagent with `spawn_agent(... model="gpt-5.4-mini" ...)`
4. Announce `Current phase: implementation, model: gpt-5.4-mini`
5. If new ambiguity appears, return to `gpt-5.4` and announce that switch explicitly
