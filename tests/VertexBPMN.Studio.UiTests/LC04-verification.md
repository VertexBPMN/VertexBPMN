# LC04 Studio verification

## Executed

- Studio Release build: `dotnet build src/VertexBPMN.Studio -c Release --no-restore --nologo -m:1 -p:SkipBpmnIoAssetBuild=true`: **0 warnings, 0 errors**.
- `node --test tests/VertexBPMN.Studio.UiTests/form-viewer.test.mjs tests/VertexBPMN.Studio.UiTests/task-form-contracts.test.mjs`: **6 passed, 0 failed**. Four JS interop behavior tests and two source-contract checks; source checks are not component/browser tests. Each added behavior was observed failing before implementation. Node emits a pre-existing module-type warning.
- Browser Use exercised the built Studio `/tasks` and real bundled form-js against a deliberately local HTTP contract stub (not the product API). Observed: bound list action opens the dialog; snapshot revision 1/hash displayed despite matching local example key; editable prefill and readonly invoice displayed; required-empty submission showed validation error without a completion request; 409 and 403 contract responses left the dialog open; identical conflict retry preserved its idempotency key; changing data changed its key; valid submission closed dialog and emitted only `{note: "Approved in browser"}` with tenant `tenant-a`, expectedRevision `7`, formSchemaHash `snapshot-hash-1`, and Idempotency-Key.
- Form-js updates are debounced. Browser events must settle before submitting; immediate edits can submit previous form state.

## Added but not executed

`TaskFormsUiTests` contains two Playwright tests: bound list routing, and snapshot/prefill/required validation/editable output plus completion wire fields/header. The existing test host is opt-in extended with task-form contract endpoints.

Building `VertexBPMN.Studio.UiTests` with `--no-restore` failed with NETSDK1004: missing `obj/project.assets.json`; Microsoft.Playwright and Chromium packages are not cached. Restore was outside the delegated no-restore constraint and disk was about 550MB free. Local real E2E infrastructure ports 55432 and 55672 were unreachable. No real API/DB LC-AC-05/06 acceptance is claimed, no OIDC authorization proof, and revision selector browser/API acceptance remains to run.

Temporary browser contract host/log/request evidence reside under `/home/azureuser/.hermes/cache/scratch/lc04-*`, including `lc04-browser-contract-requests.jsonl` and `lc04-studio-build-final.log`. No commits or pushes.
