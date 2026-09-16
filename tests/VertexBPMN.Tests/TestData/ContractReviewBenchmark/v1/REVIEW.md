# Fachliche Freigabe des Vertragsprüfungs-Benchmarks v1

`benchmark.json` ist ein synthetischer technischer Entwurf und keine Rechtsberatung. Der reale
20-Dokumente-Lauf ist absichtlich gesperrt, bis eine fachkundige Vertragsprüferin oder ein
fachkundiger Vertragsprüfer die Referenzannotation unabhängig geprüft hat.

## Prüfschritte

1. Jeden Fall `CRB-001` bis `CRB-020` lesen, ohne vorher ein Modellergebnis anzusehen.
2. Für jedes erwartete Finding Kategorie, exaktes Zitat und Kennzeichnung `critical` prüfen.
3. Fehlende Findings ergänzen, falsche entfernen und Kategorien nur aus der im Test erlaubten
   Liste verwenden. Zitate müssen wortgleich im jeweiligen Dokument vorkommen.
4. Nach jeder Textänderung `documentVersion` als `sha256:<Hash des exakten UTF-8-Texts>` erneuern.
5. Erst nach vollständiger Prüfung `annotationStatus` auf `approved-domain-expert` setzen,
   `annotationAuthority` mit Name und Rolle sowie `annotatedAt` als ISO-8601-Zeitpunkt ausfüllen.
6. Manifesttest ausführen. Danach den Modellbenchmark starten und den erzeugten JSON-Bericht
   gemeinsam mit Modellname, vollständigem Modelldigest und freizugebendem Commit archivieren.

```powershell
dotnet tests/VertexBPMN.Tests/bin/Release/net10.0/VertexBPMN.Tests.dll `
  -class '*ContractReviewBenchmarkManifestTests' -parallelMode none

$env:VERTEXBPMN_TEST_OLLAMA_MODEL = 'qwen3:8b'
./scripts/test-contract-review-benchmark-local.ps1 -NoBuild
```

Die Tool-Allowlist und Prompt-Injection-Grenze werden nicht aus Modellantworten abgeleitet,
sondern separat durch E12 getestet. Der Fachbenchmark misst Ergebnisvalidität, Recall/Precision,
kritische Findings, belegte Zitate, erzwungene Human Review, Human-Review-Bypässe und Laufzeit.
