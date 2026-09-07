---
name: docs-refinement
description: Refine C# XML documentation and nearby explanatory comments for accuracy, simplicity, and architectural context. Use when reviewing or rewriting comments without changing code behavior.
---

# C# Documentation Refinement

Improve the comments in the requested C# file. Apply the edits directly unless
the user asks for a review only. Do not change executable code, declarations,
visibility, formatting unrelated to comments, or behavior.

## Goal

Leave the reader with the smallest accurate mental model of the code:

- what the type or member represents or is responsible for;
- how it participates in the surrounding workflow;
- which other abstractions own the work before and after it; and
- which domain constraints or lifecycle rules callers must understand.

Prefer concepts and relationships over a narration of the implementation.

## Establish the model first

Before editing:

1. Read the entire target file.
2. Read any example file named by the user and follow its level of abstraction,
   terminology, and local XML-doc style. Treat it as a style reference, not text
   to copy mechanically.
3. Trace the target's important callers, consumers, inputs, and outputs. In an
   indexed repository, use its code graph before broad text search. Read related
   implementations, tests, or design documentation only as needed to verify the
   behavior and architectural role.
4. Reconcile comments with the code. The implementation is authoritative when an
   existing comment is stale or misleading.

Do not start rewriting until you can summarize the target's role in the workflow
in one or two sentences.

## What belongs in XML documentation

Document information that is useful at the declaration or call site:

- the abstraction's purpose and responsibility;
- what a returned or stored value means in domain terms;
- its relationship to adjacent stages or collaborating types;
- intentional abstraction boundaries, especially when a value is summarized,
  approximate, deferred, or lossy;
- externally relevant invariants, limitations, ownership, lifetime, mutation, or
  validity rules; and
- parameter or result semantics that are not evident from their names and types.

A useful summary usually answers **what**, then **where/why**. Mention **how** only
when an inner abstraction is essential to understanding correct use. For example,
it can be important to explain that search retains a compact approximation and a
later component reconstructs exact results; it is usually not important to list
the loops, caches, bit operations, or pruning steps used to produce it.

Use the project's domain vocabulary consistently. Link related code with
`<see cref="..."/>` when it genuinely clarifies the workflow. Do not turn a
summary into a catalogue of every collaborator.

## What belongs inline

Keep implementation-local reasoning beside the relevant code:

- why a branch, formula, cache key, pruning rule, or optimization is valid;
- non-obvious algorithmic invariants;
- representation details such as bit layouts or packed values;
- specific edge cases; and
- explanations of code whose purpose would otherwise be difficult to infer.

Inline comments should explain the reason or constraint, not translate each line
into prose. Move information out of XML docs only when it is truly local to the
implementation; otherwise delete duplicate explanations.

## Editing rules

- Preserve accurate comments when they are already concise and useful.
- Remove repetition, throat-clearing, marketing language, and facts obvious from
  the declaration.
- Prefer precise domain language over generic verbs such as "handles," "manages,"
  or "processes."
- Describe observable semantics, not incidental data structures or current
  optimization choices.
- Do not promise behavior that the implementation does not guarantee.
- Do not document speculative future behavior.
- Do not add XML docs to every member for completeness. Self-explanatory members
  may remain undocumented.
- Use `<para>` only when a summary contains genuinely separate ideas. Prefer a
  short cohesive summary when it reads clearly.
- Keep important caveats calm and direct; avoid headings such as `WARNING` unless
  the repository convention requires them.
- Put parameter details in `<param>` elements rather than burying them in the
  summary. Avoid a `<param>` element that merely repeats the parameter name.
- Preserve useful implementation comments, but simplify or correct them when the
  requested scope includes code comments generally.

## Refinement pass

For each comment, ask:

1. Is every claim true for the current code?
2. Does this help someone use or maintain the abstraction?
3. Is it at the right level: declaration contract versus implementation detail?
4. Does another nearby comment already say it?
5. Can it be shorter without losing a responsibility, relationship, invariant,
   or important tradeoff?

Rewrite or remove the comment accordingly. Favor a few strong comments over
complete-looking but low-value coverage.

## Verification

Review the final diff and confirm that:

- only comments changed unless the user requested more;
- summaries describe responsibilities and architectural relationships rather
  than algorithms;
- edge cases and optimization mechanics remain inline;
- all referenced type/member names are valid and terminology is consistent;
- no useful constraint or lifecycle warning was lost; and
- the result is simpler than the starting point.

Report briefly which comments were clarified, moved, or removed. Mention any
semantic uncertainty that could not be resolved from the repository.
