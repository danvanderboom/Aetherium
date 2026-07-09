# Localization — Design Vision

**Status:** Living design. No i18n infrastructure exists; all player-facing text is hardcoded English, including procedurally *generated* prose — see §8.
**Scope:** What great localization practice teaches, Aetherium's id/catalog/grammar model, why procedural generation changes the problem, the LLM localization pipeline, and the maturity ladder.
**Audience:** Engine maintainers, OpenSpec proposal authors, and game/campaign designers building on Aetherium.

---

## 1. Framing

Aetherium is an engine, not a game — and for localization that cuts deeper than usual: **the engine must not even contain English.** The destination is an engine that emits *string ids and parameters*, worlds that ship *catalogs and grammars* as content, and clients that render in the player's locale. English becomes just another catalog.

Aetherium starts closer to this than most engines, and further. Closer: the server already streams *semantic* perception (tags, ids, enums) rather than rendered text, so most of the wire protocol is locale-free by construction — the same architectural bet that paid off for accessibility. Further: the narrative subsystem procedurally **generates** player-facing prose by interpolating English fragments (`$"Rescue {goal.NPCType}"`), which no string table can fix. Translating a sentence is a catalog problem; translating a *sentence generator* is a grammar problem, and it is the genuinely hard, genuinely interesting part of this design.

## 2. What the practice teaches

| System / practice | What it does | The lesson |
|---|---|---|
| **ICU MessageFormat / CLDR** (industry baseline) | Messages with named parameters, plural categories, gender/select rules per locale | **Never concatenate; never assume plural rules.** English's one/other is the simplest case of dozens; parameters and plural selection belong in the message format, not code |
| **Paradox (CK3/Stellaris) localization files** | YAML-per-locale catalogs with in-string data functions (`[Root.GetName]`); moddable and community-translated in the open | **Catalogs as moddable data files.** When localization is plain data, communities localize games publishers never would — the exact content-pack philosophy Aetherium already holds |
| **FF14's localization team** | Treats each language as *adaptation* with its own voice, not translation; four languages, all first-class | **Localization is authorship.** The pipeline must give locale authors creative room (own idioms, own register), which id+grammar indirection provides and string-replacement forbids |
| **WoW and MMO practice** | One server serves all locales; clients hold locale packs and render ids locally | **The server is locale-agnostic.** Locale is a rendering concern; two players on one map see the same world in different languages |
| **Dwarf Fortress / roguelike name generators** | Procedural names and histories from grammar rules | **Generated text needs per-locale grammars.** A name generator that concatenates English morphemes produces gibberish in Polish; each locale needs its own generative rules, authored not translated |
| **Pseudo-localization** (industry QA staple) | A fake locale (`[!!! Ŕéšçûé толсто !!!]`) exposes hardcoded strings, truncation, and encoding bugs before any translator is hired | **Enforce mechanically, early.** Localizability regressions are lint failures, not launch surprises |

Distilled, the four properties the system needs — each mapped to the Aetherium asset it builds on:

| Property | In play | Engine asset |
|---|---|---|
| **Ids on the wire** | Perception/results carry `text:` ids + params, never prose | Semantic perception (shipped, with listed exceptions — §8) |
| **Catalogs as content** | Per-locale YAML, shipped/modded like any content pack | Content-atlas & pack conventions (shipped/designed) |
| **Grammars for generated text** | Narrative emits (template id, slots); locales author template sets | Narrative generators (shipped, English-interpolating) |
| **Mechanical enforcement** | Pseudo-locale + hardcoded-string lint in CI | `ColorblindLintRule` precedent (shipped) |

## 3. The layered, composable model

```
1. IDS         every player-facing string is a TextRef{id, params}
               item.torch.name · affordance.unlock_door · result.downed_cannot_act
                    │
2. CATALOGS    per-locale YAML: id → message (ICU-style params, plural/select)
               en is a catalog like any other; shipped/modded as content packs
                    │
3. GRAMMARS    generated text: generators emit (template id, slot values);
               each locale ships template/grammar sets — quest titles, lore,
               names authored per locale, not translated post hoc
                    │
4. RENDERING   client picks locale, resolves ids, applies fonts/shaping;
               missing key → fallback chain + visible marker
                    │
5. PIPELINE    authoring flow: extraction → catalogs → MT baseline →
               human/LLM polish → pseudo-locale QA → CI lint
```

Design rules that keep this composable:

- **`TextRef` is the only way text crosses the wire.** DTO fields that today carry English (`ItemDto.Label`, `AffordanceDto.Label`, `TileTypeDto.Name`, result `Reason` strings, `Weather`/`Season`) migrate to ids + params, with the raw string retained during transition. Enum-like strings (`Weather = "Clear"`) become enums/tags whose display is a catalog lookup — they were never text at all.
- **Parameters are typed slots, never pre-rendered fragments.** `result.pickup_failed{item: item.torch.name}` lets the German catalog inflect the item name; `"Failed to pick up " + label` forbids it. Message selection (plural, gender, case) lives in the catalog entry, per ICU practice.
- **Generators choose *what to say*; grammars choose *how to say it*.** The quest generator's output becomes `(quest.rescue.title, {npc: npc.merchant})` — pure semantics. Each locale's grammar pack renders that as it sees fit, including structures English doesn't have. This is the FF14 lesson applied to procedural content: locale authors adapt the *generator*, not its output.
- **Fallback is a chain with a visible seam.** Missing id → world's default locale → engine marker (`⟦quest.rescue.title⟧`). Silent fallback to English is how games ship half-translated without noticing.
- **The server stays locale-free.** No locale on sessions, no translated text in grain state. The one nuance: grammar/catalog *content* is world data (a world authored only in Japanese is valid); the *engine* still never contains a display string.

## 4. Creative leaps

1. **LLM locale packs with in-context QA.** Aetherium's agents can do more than machine-translate a catalog: an LLM agent can author a full locale grammar pack (quest templates, name generators, lore styles with a native register) as a PR-shaped content-pack contribution — and then **play the game in that locale** through the agent/perception stack to verify the generated text reads sensibly in context, flagging awkward output with replay evidence. Self-testing localization is a pipeline no studio has.
2. **Native procedural prose per locale.** Because generation emits semantics and locales own grammars, a Polish player gets quest titles *composed in Polish* — correct cases, native idiom — not translated English. For a procedurally-narrated engine this is the difference between localized and translated, and almost no procedural game clears it.
3. **One id discipline, three payoffs.** The same `TextRef` indirection that enables localization is what lets the screen-reader client speak any locale ([accessibility](audits/2026-07-06-engine-gap-analysis/design-next-steps.md#413-accessibility-contract-via-perception)) and lets agents reason over semantics instead of parsing English. Localization here is not a feature bolted on; it is the perception philosophy finishing its job.
4. **Localizability as CI, like colorblindness.** The shipped `ColorblindLintRule` pattern extends naturally: a pseudo-locale run plus a lint that flags English literals entering player-facing DTO fields. Regressions become red builds the day they're written.

## 5. Maturity ladder

| Tier | Ships | Depends on |
|---|---|---|
| **T0 — Id substrate** | `TextRef{id, params}` type; catalog schema + loader; `en` catalog seeded from the real hardcoded strings (§8 inventory); items/affordances/result strings carry ids alongside existing text | String inventory (done, §8) |
| **T1 — Rendering** | Console client resolves ids from catalogs; locale selection; fallback chain + missing-key marker; `Weather`/`Season` and kin become tags with catalog display | T0 |
| **T2 — Message richness** | ICU-subset params (plural/select); typed slots inflectable per locale; second real catalog proves the seam; pseudo-locale + hardcoded-string lint in CI | T1 |
| **T3 — Grammars** | Narrative/lore/name generators emit (template id, slots); grammar packs per locale; generated quests render natively in every shipped locale | T2; narrative (shipped) |
| **T4 — Pipeline & delivery** | Locale packs as content packs (signing/delivery per modding SDK); extraction tooling; MT-baseline + polish workflow | T2; content packs (§4.15) |
| **T5 — Living localization** | LLM locale-pack authoring with in-context agent QA; community locale contributions via the content pipeline | T3–T4; agent infra (shipped) |

## 6. The ECA graduation path

Localization is mostly data, not behavior, so its ECA story is scoped: grammar packs may include **selection rules** (`when: quest.target.tag == undead, prefer: template.rescue.grim`) in the same condition vocabulary as everything else, letting locale authors vary register by context. Beyond that, text generation stays declarative by design — prose logic in scripts is how consistency dies.

## 7. Anti-goals

- **No English in the engine.** `en` is a catalog. Engine code never contains a display string (error/log text for operators is exempt — that's observability, not player text).
- **No string concatenation or interpolation for player-facing text.** Ever. The narrative generators' current `$"Rescue {npc}"` pattern is the named anti-pattern this design retires.
- **No server-side locale.** Sessions have no language; the same world serves all locales simultaneously.
- **No silent fallback.** Missing translations are visible (markers, lint counts), or they never get fixed.
- **No shipping raw machine translation as final.** MT is a baseline for human/LLM-polished catalogs, per the pipeline tier.
- **Fonts, shaping, and RTL layout are client concerns.** The contract's only obligation: any Unicode string renders somewhere (already true).

## 8. Current state

- **Infrastructure: none.** Zero `.resx`, no `IStringLocalizer`, no `CultureInfo` in product code, no openspec mention.
- **The wire is mostly clean already:** `PerceptionDto` is largely ids/enums/coordinates. The English leaks, cataloged: `ItemDto.Label` (default `"Item"`), `CharacterDto.Name` (default `"Character"`), `AffordanceDto.Label`, `InteractionResultDto.Label`/`.Description`, `TileTypeDto.Name`, `PerceptionDto.Weather`/`.Season` (string-typed: `"Clear"`, `"spring"`). `AudioPerceptionDto` fields are semantic asset keys — already fine.
- **Hardcoded English inventory (T0 seed list):** item labels per entity class (`TorchItem.cs` `"Torch"` et al.); interaction labels in `InteractionSystem.cs` (`"Unlock Door"`, `"Lockpick"`, `"Force Open"`…); `GameMapGrain` result/`Reason` strings (`"You are downed and cannot act."`, `"Location is not passable"`…); console client UI strings (`"Inventory [0/10]: (empty)"`); `PerformanceAnalyzer`'s English recommendation prose (see [gameplay-telemetry.md](gameplay-telemetry.md) §3).
- **The hard case is live and growing:** narrative generation interpolates English throughout — `NarrativeGraphGenerator.cs` (`$"Retrieve {GetItemDisplayName(itemTypes)}"`, `$"Rescue {goal.NPCType}"`), `NarrativeConsequenceEngine.cs` (`$"Repayment from {goal.NPCType}"`), `LoreGenerator.cs` (full prose paragraphs via `StringBuilder`). Every new generator written before T3 deepens the migration.
- **The gap in one sentence:** the perception architecture already did the hard structural work; what remains is an id/catalog seam (mechanical), a grammar seam for generators (the real design work), and CI enforcement so the debt stops compounding.
