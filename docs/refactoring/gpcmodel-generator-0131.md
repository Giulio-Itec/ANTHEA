# Model combination generator extraction — task 0131

The Modelli proposal, preview, generation and saved-choice reconciliation use
`GPC.Model.Design.Combinations.ModelCombinationGenerator`. The rule set is explicitly
`Ntc2018GeneralStructuralV1`, with `AntheaNtc2018V1` naming. No road profile or stringer
feature is part of this extraction.

## Integration on official BC — task 0146, 23 November 2026

The six original task 0131 commits were merged, in their original history, into
`codex/0146-generatore` from fetched main `5436403e`. There were no conflicts.
The test harness, frozen captures, runner registration and alias/friend-assembly
declarations already imported by task 0143 are identical and remain single copies.
Only the generator adapter and its editor call change in production.

The production build now uses the official BC snapshot already committed on main,
without `GpcLibDir` or a candidate set. `lib` remains unchanged; release manifest
SHA-256 is `687F3ED4B0041BF76C46968D844F09C6F3C51688158FE5D2D1B80BB440DBB1F5`.
ANTHEA stays at 1.19.5 because that version has never shipped as an installer and
this refactoring preserves behaviour without adding a feature or replacing a DLL.

L1 passed on merge `2c382bee`: six builds and seven suites, 13 PASS, no failures.
This includes all five task 0136 section 4 suites, the 106 exact captures and live
legacy parity, native Straus7 elements/stages and the WPF importer checks. Evidence:
`supporto/artefatti/0146/l1-complete`.

L2 `baseline` completed on `7bf82a98` in 1273.7 seconds: 32 passing builds and
79 suites (72 PASS, six exact KNOWN F4 outcomes and one historical banco-ca
outcome). The unchanged runner reports 104 PASS / 6 KNOWN / 1 NEW-FAIL overall;
the last item is covered by task 0146's explicit 23 November user authorization,
not by an allowlist edit. The six F4 comparison hashes match the existing list.

Banco-ca has exactly the historical 2456 rows / 4677 differences in 13 files.
A fresh diagnostic from pre-integration main `5436403e`, using the same BC and
fixtures, reproduces every file counter and all 2000 retained difference details.
The existing exact comparator also checks the full before/after dense corpus:
21 files / 32564 rows, 31320 identical and 1244 differing only by declared random
IDs; zero tolerance-admitted, classified or unclassified differences. This covers
the rows beyond the diagnostic detail cap without changing any expected output.
There is no new failure. Evidence: `supporto/artefatti/0146/gate-verification.json`,
`baseline`, `banco-main-before-complete` and `before-after-dense-comparison.json`.

The optional obsolete F2 reference comparisons and the full contrast run were
not selected by this L2 profile. Their accepted historical outcomes remain
reported separately; no new passing result is claimed for them. No installer was
built. The publication result and remote read-back are in
`chatgpt-compiti/0146-resoconto.md` in the sibling task directory.

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

## Historical task 0131 candidate build

The original task 0131 branch needed its isolated snapshot via `GpcLibDir` before
the final coherent set was installed. The following command and results document
that historical run; current builds use official BC as described above. Task 0131
staged its candidate using `tools/libs/Update-Snapshot.ps1 -ReleaseSet ...
-AllowCandidate -Staging ... -Repos ...`, without an official set, push or deployment.

```powershell
$snapshot = (Resolve-Path ./supporto/artefatti/0131/snapshot-compat).Path
& ./build/ci.ps1 -Stage build,fast,ui `
  -Only '^(ModelWorkspace.Checks|ModelCombination.Equivalence.Checks|ModelViewer.CombinationBench.Checks|Architecture.Tests|ModelViewer.Combinations.UiChecks)$' `
  -GpcLibDir $snapshot -Tag 0131 -Keep -NoPrune
```

The targeted run passed: ModelWorkspace 821 checks, Architecture 643, WPF combinations 32, the independent combination bench and all 106 exact captures. Its summary is `supporto/artefatti/0131/ci-targeted-compat/summary.json`. The first invocation used a relative GpcLibDir and failed to find manifest.props; using the absolute path above fixed the invocation without source changes.

The WPF catalogue stays at 74 suites. The new equivalence suite is in the fast stage.
The coordinator runs level-2 `baseline` after integration/library installation, then
the final candidate full/baseline/installer and required native gates.

## Explicit resource budget and compatibility

The service supports an explicit `CombinationGenerationLimits.MaximumWork` budget.
Passing `new CombinationGenerationLimits()` selects a budget of 1,000,000 work units,
bounding candidate-row factor work and directional seismic products before allocation.
Exceeding an explicitly selected budget returns `WorkLimitExceeded` with no partial rows.

The default request uses `CombinationGenerationLimits.Compatibility`: 12 pattern groups,
4096 unique rows and `MaximumWork = long.MaxValue`. ANTHEA uses this compatibility policy,
so no new work refusal changes legacy acceptance or error precedence. The final review
corrected the earlier implicit work guard. A separate live comparison with the unchanged
compiled legacy generator covers 2000 accidental groups (2005 rows), which an implicit
1,000,000 budget would incorrectly refuse. All 106 frozen captures remain untouched.
Clients needing a finite work budget must choose it explicitly; this changes their
resource acceptance policy. The road client must select and test its policy in step 3.

Task evidence and final outcomes: `chatgpt-compiti/0131-resoconto.md` in the repository
root's sibling task directory. Native/shared-host/deployment evidence and road rules
remain separate gates.
