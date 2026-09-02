# bpmn.io Studio Shell

Status: Phase 1 baseline implemented.

The Studio uses a thin, stable shell around the bpmn.io family of toolkits. Razor pages own workflow actions such as load, save, export and deploy. Reusable surface components own the browser canvas and call JavaScript interop modules.

## Surfaces

| Artifact | Editor component | Viewer component | Toolkit target |
| --- | --- | --- | --- |
| BPMN | `BpmnModelerSurface` | `BpmnViewerSurface` | `bpmn-js` Modeler / NavigatedViewer |
| DMN | `DmnModelerSurface` | `DmnViewerSurface` | `dmn-js` Modeler / Viewer |
| Forms | `FormBuilderSurface` | `FormViewerSurface` | `form-js` Editor / Viewer |
| CMMN | `CmmnModelerSurface` | `CmmnViewerSurface` | `cmmn-js` Modeler / Viewer |

## Asset pipeline

Runtime assets stay local under `src/VertexBPMN.Studio/wwwroot/lib/<toolkit>/` and are loaded by `Components/App.razor`. The Studio-specific wrapper modules live under `src/VertexBPMN.Studio/wwwroot/js/`.

The local browser bundles are generated from pinned npm packages in `src/VertexBPMN.Studio/package.json` and `package-lock.json`:

| Package | Version |
| --- | --- |
| `bpmn-js` | `18.24.0` |
| `bpmn-js-properties-panel` | `5.63.0` |
| `@bpmn-io/properties-panel` | `3.48.0` |
| `dmn-js` | `17.10.1` |
| `@bpmn-io/form-js` | `1.24.1` |
| `cmmn-js` | `0.20.0` |
| `esbuild` | `0.28.2` |

Build manually from `src/VertexBPMN.Studio`:

```bash
npm ci
npm run build:bpmnio
```

The Studio project also runs the asset build through the `BuildBpmnIoAssets` MSBuild target when inputs are newer than the generated files. CI sets up Node.js 22 before the .NET build so this remains reproducible on GitHub Actions.

The wrapper modules intentionally check for toolkit constructors at runtime:

- BPMN modeler: `window.BpmnJS` or `window.BpmnModeler`
- BPMN viewer: `window.BpmnNavigatedViewer`, `window.BpmnViewer` or `window.BpmnJS`
- DMN: `window.DmnJS`, `window.DmnModeler`, `window.DmnViewer`
- Forms: `window.FormEditor.FormEditor`, `window.FormViewer.Form`
- CMMN: `window.CmmnJS`, `window.CmmnModeler`, `window.CmmnViewer`

If a bundle is missing or still a placeholder, the wrapper renders a non-editing fallback and preserves the XML/JSON artifact. This keeps Playwright and the Studio shell stable while real toolkit bundles are updated.

## User flows covered

- BPMN: load template, edit, preview, export XML, deploy to repository API.
- DMN: edit decision table XML, preview, export, deploy, evaluate, list definitions/instances.
- Forms: edit schema, save a Studio draft, preview runtime rendering, export JSON.
- CMMN: load template, edit, preview, export, register model and execute existing case actions.

## Test coverage

`tests/VertexBPMN.Studio.UiTests/StudioUiContractTests.cs` contains Playwright smoke coverage for all four shells. The test verifies headings, primary save/deploy buttons, export buttons, editor surfaces and viewer surfaces.

Phase 2 Vertex extensions: `vertex.json` is bundled into the properties-panel asset and copied to `wwwroot/lib/vertex-bpmn-moddle/vertex.json`. See [vertex-bpmn-moddle.md](vertex-bpmn-moddle.md).

## Follow-up

The next hardening step is adding UI-level smoke coverage that verifies the real constructors are present in the browser, not just that the Studio shell renders. The current wrappers still keep fallback rendering so the shell degrades gracefully if an asset build is missing.
