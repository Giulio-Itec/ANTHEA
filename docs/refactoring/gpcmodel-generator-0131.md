# Model combination generator extraction — task 0131

The Modelli proposal, preview, generation and saved-choice reconciliation use
`GPC.Model.Design.Combinations.ModelCombinationGenerator`. The rule set is explicitly
`Ntc2018GeneralStructuralV1`, with `AntheaNtc2018V1` naming. No road profile or stringer
feature is part of this extraction.

GPC owns the exact case identities, recognized choices, correlated load groups,
validation order, coefficient enumeration, seismic alternatives, limits, state counts,
names and IDs. The existing `GeotechnicalCombinations`, `CombinationProfile`,
`SeismicCombinations`, `CombinationMatrix` and result evaluation arithmetic are unchanged.

ANTHEA retains snapshot/catalogue extraction (including MIDAS STLD traversal), its
public UI wrappers and JSON DTOs, Italian diagnostic sentences, document replacement,
name-collision handling, revisions, result retention, copy/update and undo. The editor
uses GPC's explicit `NormalizeForEditing` transition. Raw service calls still reject
directions on non-seismic cases. Category and nature mappings are checked against GPC.

## Frozen evidence

Commit `df1525d1` captured the unchanged production generator from ANTHEA
`bce5de06fa3b3cfdf866e8ab3b504c03e3d0100b`, using its committed BB snapshot. The
corpus is `tests/fixtures/model-combinations-0131`: 52 ordered input families in
it-IT and en-US, plus two result/document captures. Per culture there are 10,637
successful generated rows and 40,258 term factors. Every double is written as its
IEEE-754 bit pattern; the manifest pins the original DLL hashes and runtime.

The exact original source is in
`tests/ANTHEA.Legacy/ModelCombinations/ModelCombinationGenerator.original.cs.txt`.
The adjacent `.cs.fixture` retains the executable legacy generator, with only the
type/namespace qualification needed to link it into the equivalence test assembly.
It is never compiled into production. The check compares both the frozen captures
and this legacy oracle. Capture mode refuses to run with the new GPC API.

The result capture includes all seven G+B rows and their envelope, plate and beam
components, nodal translations/rotations, principal governing alternatives,
warnings, saved definitions/choices, document fingerprints, reopen and regeneration
with current results retained. Existing independent hand expectations are unchanged.

## Candidate-only build

This branch needs the isolated task 0131 snapshot via `GpcLibDir` until the coordinator
installs the final coherent set. The committed `lib/Checker` snapshot is unchanged.
The candidate is staged using `tools/libs/Update-Snapshot.ps1 -ReleaseSet ...
-AllowCandidate -Staging ... -Repos ...`; no official set, push or deployment is made.

```powershell
$snapshot = (Resolve-Path ./supporto/artefatti/0131/snapshot).Path
& ./build/ci.ps1 -Stage build,fast,ui `
  -Only '^(ModelWorkspace.Checks|ModelCombination.Equivalence.Checks|ModelViewer.CombinationBench.Checks|Architecture.Tests|ModelViewer.Combinations.UiChecks)$' `
  -GpcLibDir $snapshot -Tag 0131 -Keep -NoPrune
```

The targeted run passed: ModelWorkspace 821 checks, Architecture 643, WPF combinations 32, the independent combination bench and all 106 exact captures. Its summary is `supporto/artefatti/0131/ci-targeted-absolute/summary.json`. The first invocation used a relative GpcLibDir and failed to find manifest.props; using the absolute path above fixed the invocation without source changes.

The WPF catalogue stays at 74 suites. The new equivalence suite is in the fast stage.
The coordinator runs level-2 `baseline` after integration/library installation, then
the final candidate full/baseline/installer and required native gates.

## Declared new resource diagnostic

The service has an explicit `CombinationGenerationLimits.MaximumWork` budget, default
1,000,000, in addition to the compatibility limits of 12 pattern groups and 4096 unique
rows. It bounds candidate-row factor work and directional seismic products before
allocating the existing generator's dictionaries/variant list. Exceeding it returns
`WorkLimitExceeded` and no rows; the Italian facade explains the count and limit.
This is a new resource refusal for unusually large requests, including potentially
large accidental catalogues otherwise below 4096 rows. It is not a normative change
or an assertion of universal legacy equivalence outside that budget. An explicit
higher budget is supported by GPC. No frozen input hits this new guard.

Task evidence and final outcomes: `chatgpt-compiti/0131-resoconto.md` in the repository
root's sibling task directory. Native/shared-host/deployment evidence and road rules
remain separate gates.
