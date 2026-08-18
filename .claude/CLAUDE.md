# CLAUDE.md

Behavioral guidelines to reduce common LLM coding mistakes. Merge with project-specific instructions as needed.

**Tradeoff:** These guidelines bias toward caution over speed. For trivial tasks, use judgment.

## 1. Think Before Coding

**Don't assume. Don't hide confusion. Surface tradeoffs.**

Before implementing:

- State your assumptions explicitly. If uncertain, ask.
- If multiple interpretations exist, present them - don't pick silently.
- If a simpler approach exists, say so. Push back when warranted.
- If something is unclear, stop. Name what's confusing. Ask.

## 2. Simplicity First

**Minimum code that solves the problem. Nothing speculative.**

- No features beyond what was asked.
- No abstractions for single-use code.
- No "flexibility" or "configurability" that wasn't requested.
- No error handling for impossible scenarios.
- If you write 200 lines and it could be 50, rewrite it.

Ask yourself: "Would a senior engineer say this is overcomplicated?" If yes, simplify.

## 3. Surgical Changes

**Touch only what you must. Clean up only your own mess.**

When editing existing code:

- Don't "improve" adjacent code, comments, or formatting.
- Don't refactor things that aren't broken.
- Match existing style, even if you'd do it differently.
- If you notice unrelated dead code, mention it - don't delete it.

When your changes create orphans:

- Remove imports/variables/functions that YOUR changes made unused.
- Don't remove pre-existing dead code unless asked.

The test: Every changed line should trace directly to the user's request.

## 4. Goal-Driven Execution

**Define success criteria. Loop until verified.**

Transform tasks into verifiable goals:

- "Add validation" -> "Write tests for invalid inputs, then make them pass"
- "Fix the bug" -> "Write a test that reproduces it, then make it pass"
- "Refactor X" -> "Ensure tests pass before and after"

For multi-step tasks, state a brief plan:

```
1. [Step] -> verify: [check]
2. [Step] -> verify: [check]
3. [Step] -> verify: [check]
```

Strong success criteria let you loop independently. Weak criteria ("make it work") require constant clarification.

## 5. No Line Wrapping in Documentation

When writing any documentation (.md files or other text files), do not insert line breaks in the middle of sentences to limit line length. Each sentence or continuous thought must be written on a single line, regardless of how long it is. Never break a sentence at 80, 85, or any other fixed column limit.

## 6. ASCII Only

Use only ASCII characters in all output: text, code, comments, file content.

- No Unicode dashes (no em-dash, no en-dash), curly quotes, arrows, bullets, or any non-ASCII symbol.
- As a dash use a single ASCII hyphen "-". Do NOT use a double hyphen "--" as a dash. This applies to prose only. The following are NOT dashes and must stay unchanged: CLI flags (--no-build, -w), code syntax, and arrows in diagrams (mermaid and ASCII art: -->, ->, ---, +--).
- No guillemets / angle quotes. Use plain ASCII double quotes ("...") instead. This applies to all languages, including Russian text: write "..." not the angled quote pair.
- In markdown tables and separators use plain hyphens and pipes.
- If a character is not in the 0x20-0x7E range, do not use it.

Cyrillic letters are the one exception: the project's documentation and code comments are written in Russian.

## 7. No Accidentally-Optional Parameters

**Don't make a parameter optional just to skip updating call sites.**

A parameter should be optional only when it has a default that makes semantic sense for the API. If a new parameter is logically required, make it required and update every call site - do not give it `= null`/a default value to avoid the edits. The tell-tale of this slop is an "optional" parameter that is actually required, which then forces null checks everywhere downstream. Applies to C# default parameter values and to TypeScript optional `?` parameters alike.

## 8. Assert the Whole Value, Not Its Absence

**Pin down what the value is, not just what it is not.**

Negative or partial assertions are weak: they pass even when the output silently drifts, because they never state the full expected value. Avoid them as the primary check - this includes negative asserts (TUnit `.DoesNotContain(...)`) and bare substring matches (`Assert.That(text).Contains("fragment")`). Prefer, in order:

- `await Assert.That(value).IsEqualTo(expected)` on the full string or object.
- `await Assert.That(collection)` checked against its complete expected contents.
- A Verify snapshot when the value is large or its whole shape matters (see [docs/spec/test-strategy.md](../docs/spec/test-strategy.md)).

Use a partial or negative check only when the full value genuinely cannot be pinned down, and pair it with positive assertions of what must be present.

## 9. No Rejected Options in Deliverables

**State the decision as a fact. Never carry your own rejected option into the artifact.**

When a choice is made - especially after the user corrects an earlier proposal - the plan, doc, comment, or code records only the final decision, written positively. Do not anchor it against the discarded alternative.

- Bad: "Store settings via `OwnsOne(...).ToJson()`, not via `ValueConverter`." / "`ValueConverter` is deliberately not used."
- Good: "Store settings via `OwnsOne(...).ToJson()`."

Justify "why this way" by the merits of the chosen approach, not by contrast with the rejected one. A reader of the deliverable did not see the discarded option and does not need it; "X, not Y" framing leaks the dialogue and drags a mistake into a document that should read as a clean decision. This is the deliverable-wide counterpart of rule 8 (assert what the value is) and of the project's "no archaeology" doc voice.

An ADR section named "Почему не альтернативы" is the one place where rejected options belong - described neutrally, as a comparison of options.

## 10. Explain in Plain Words, Don't Just Name the Concept

**Write for a reader who does not already carry the jargon in their head.**

Prose that leans on an unexplained term of art (a design-pattern name, an abstraction noun) as its load-bearing word reads as clumsy and opaque: the reader must decode the term before reaching the meaning. Naming the mechanism ("decoupling via a seam", "port/adapter indirection") is not explaining it - and swapping one jargon heading for another does not fix it.

**The full rules, with before/after examples, live in [docs/guides/documentation-style.md](../docs/guides/documentation-style.md). Read it before writing or editing any `.md` file.** The current reference document is [docs/spec/arch-design/core/core-architecture.md](../docs/spec/arch-design/core/core-architecture.md). This assignment is temporary by nature: it becomes the design document of the first real module as soon as that document exists.

The checkable requirements, applying to every genre:

1. **Explain a term where it first appears, or replace it with a description.** The tell: drop the reader's knowledge of the term - if the sentence stops meaning anything, the term is doing the explaining instead of you. Keeping the term is fine _after_ the explanation, as a label for something already understood.
2. **One paragraph, one thought.** Aim for 200-500 characters, hard ceiling 800. A longer paragraph almost always packs several independent claims - split them.
3. **One sentence, one thought.** At most one parenthetical. A semicolon joining two independent rules means a full stop belongs there.
4. **Verbs, not nominalizations.** "the resolver parses the token", not "token resolution is performed".
5. **Back every claim of benefit with a number, a comparison, or a concrete consequence.** "faster", "cheaper", "simpler" with nothing behind it is an opinion the reader can neither check nor argue with.
6. **Define a thing by what it is.** A rejected alternative belongs only in a section that compares options, described neutrally. A heading built on negation ("X, not Y") tells a reader who knows neither X nor Y nothing at all.
7. **No English words in Cyrillic transliteration** beyond names that come from the code. Type, file, route and library names stay verbatim in backticks; everything else gets a Russian word.

Also: open a section by promising its shape ("Задачу усложняют три обстоятельства"); make a bolded lead-in a complete claim rather than a noun-phrase heading; voice the reader's objection and answer it; state a cause outright instead of leaving it to be inferred.

Plain does not mean shallow: no technical fact, type, route or number is dropped for the sake of readability. The text gets longer, because a justification that used to be crammed into a parenthetical becomes its own sentence.

## 11. Design for Concurrent Data Access

**Assume more than one caller touches the same data at once. Plan the guard before writing the code, not after a race surfaces.**

Any code that creates or mutates shared or persisted state can run concurrently - two requests, two browser tabs, a request racing a background job. Name the concurrency in the plan and choose the guard up front, one per failure mode:

- **Create races** (two callers insert "the same" row): make get-or-create idempotent behind a unique constraint or a per-key serialization gate; the loser reads the winner's row instead of throwing.
- **Lost updates** (two overlapping read-modify-write cycles on one row): round-trip an optimistic-concurrency token (rowversion) through the read DTO and back on write; a stale token is a conflict (-> 409), never a silent overwrite.
- **Exclusive work** (only one job/run may be active at a time): claim it atomically (a filtered unique index or a conditional insert), and derive status as a fact from an append-only log rather than a mutable field two writers can stomp.

The tell of unhandled concurrency: a status or ownership modelled as a plain mutable field updated in place while more than one actor can reach it. State the assumption explicitly (rule 1); if a path is genuinely single-writer, say why it cannot race.

Three races almost every product built on this skeleton will have, as worked examples:

- **First sign-in.** Two tabs finish the OIDC redirect at once and both try to create the local user for the same `sub`. Guard: idempotent get-or-create behind a unique constraint on `sub`.
- **Scheduled run.** The scheduler fires while a user presses "run now" by hand. Two runs for one period must not appear. Guard: atomic claim through a filtered unique index on (owner, period).
- **Two-tab edit.** The user edits the same entity in two tabs and both submit. Guard: rowversion round-tripped through the DTO; a stale token is a 409, not a silent overwrite of the other tab's text.

## 12. Read the Relevant ADRs Before Implementing

**The project's conventions live in the ADRs. Consult the ones for the layer you are about to touch - before writing code, not after a reviewer points them out.**

This repository encodes its design decisions as 36 ADRs (`docs/adr/`, indexed in `docs/adr/README.md` and [project-map.md](project-map.md)). Skipping them leads to code that fights the established pattern and gets rewritten.

- **Read selectively by layer.** For frontend work consult the frontend ADRs (0026 Orval, 0027 SPA, 0028 structure, 0029 TanStack Query, 0030 a11y, 0032 frontend tests, 0033 API mocks); for backend work consult the backend ADRs (0006 modular monolith, 0007 PostgreSQL, 0008-0011 layers and DDD, 0012-0014 Wolverine, 0016-0025 cross-cutting mechanisms). Match the ADR to the area you change - do not read all 36 for a one-line edit.
- **Let the ADR shape the approach.** When an ADR names the mechanism for what you are building (server state -> TanStack Query, invariants -> domain, identity -> the resolver seam), follow it; if you deviate, say why in the deliverable.
- **Project-map first for orientation.** [project-map.md](project-map.md) points to the right ADR for each subsystem - use it to find the relevant few quickly.

The tell: if a reviewer has to cite an ADR to explain why the code is wrong, that ADR should have been read first.

## 13. Reads Project Through a Read-Port, Never a Write-Repository

**A read path loads data through a query-repository (read-port). The aggregate's write-repository appears in a read path only to mutate - never merely to load-and-read.**

This project runs strict CQRS (ADR 0010): a query handler projects through a query-repository (`I<Entity>QueryRepository`) that pulls exactly the needed columns into a `*Result`/DTO in SQL. Loading a whole aggregate through its write-repository (`I<Aggregate>Repository`, the Domain write contract) and then filtering it in memory with LINQ-to-Objects is a layer leak: it over-fetches the aggregate graph, carries EF change-tracking into a read, and couples the read to the write model's internal shape.

- **The tell:** a write-repository (`I...Repository`, not `I...QueryRepository`) injected into a `*QueryHandler` (or a read-only service) and used only for `Get...` + in-memory `.Where(...)/.Select(...)`. If the data isn't there, add a projection method to the read-port and query it in SQL.
- **The one legitimate exception - mutation on a read:** a read handler that lazily creates a row on first access (for example, the settings row of an account that has none yet) holds the write-repository to `Add`/`Save`, while still serving the projection through the read-port. The distinguishing question: does the write-repository mutate here, or only load-and-read? Only-read belongs in the read-port.
- **Do not defend an in-memory filter by "the aggregate is small."** Size is the wrong axis - a bright-line "reads go through read-ports" removes per-case size judgment that drifts as data grows and that gets copied onto a large aggregate by the next reader.

## 14. Committed Generated Artifacts Must Be Environment-Independent

**A generated file that lives in the repo must have byte-identical content regardless of the build environment.**

Some artifacts are generated by a build and committed as the source of truth: the OpenAPI document (`apps/backend/DotnetVue3TemplateRu.Api/openapi/DotnetVue3TemplateRu.Api.json`) and Verify snapshots. Their content must not depend on whether the build ran in Development, CI, or a local `yarn dev` - a plain build and a CI build must produce the same bytes. If a code path registers something only in one environment, exclude it from the artifact at the source.

- **The tell:** a committed generated file shows up as modified after merely running the app locally (e.g. the OpenAPI spec dirties on every `yarn dev`). That means its content drifts with the environment, every dev run makes commits look stale, and the drift-guard false-fires between environments.
- **Fix the leak at the source, not with a re-stage/hook band-aid.** A dev-only endpoint that leaks into the OpenAPI doc gets `.ExcludeFromDescription()` - that is why the Scalar redirect at `/` carries it. The rule generalizes: whatever registers `if (env.IsDevelopment())` must stay out of any committed generated output.

## 15. Keep Commit Messages Short

**One subject line. A body of at most two lines, and usually none at all.**

The subject names the main change. Anything else that rode along goes on a single line - "Попутно: ...". Do not restate the diff, do not enumerate files, do not explain motivation at length: the diff is already there, and the reasoning belongs in an ADR.

- **The tell:** a body with bullet lists, paragraphs per subsystem, or a "Внимание:" section. That is a release note, not a commit message.
- **A message that genuinely needs a long body means the commit should be split.** Length is the symptom; mixed concerns are the cause.
- Language and tense follow [CONTRIBUTING.md](../CONTRIBUTING.md): Russian, consistent within a branch.

---

**These guidelines are working if:** fewer unnecessary changes in diffs, fewer rewrites due to overcomplication, and clarifying questions come before implementation rather than after mistakes.

---

## Project Map

The repository orientation map - what this project is, where things live, the conventions, current features, and where to look for details - is maintained in [project-map.md](project-map.md) and imported below, so it loads into context every session. Do not duplicate it here; when a change affects the architecture, update project-map.md (and AGENTS.md if its pointer wording changes).

@project-map.md
