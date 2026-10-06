# G04 – deterministische lokale Commitobjekte

## Implementierter Teilumfang

`ControlledGitProcess.BuildCommitAsync` erstellt reale Git-Commitobjekte aus validierten, unveränderlichen BPMN-Snapshots. Die Basis wird in einen privaten, pro Aufruf eindeutigen Index übernommen; ausschließlich explizite Modellpfade werden ersetzt. Originalbytes gehen über begrenzte Prozess-Standardeingabe an `hash-object --no-filters`. Kein Checkout, kein `add --all`, keine Filter oder Submodule-Ausführung. Der private Index einschließlich Lock wird anschließend entfernt.

Bestehende Dateimodi bleiben erhalten. Symlink-/Gitlink-Ziele, Datei-Verzeichnis-Kollisionen und abweichende Groß-/Kleinschreibung bestehender Pfade werden abgelehnt. Commitzeit und Operation-ID sind explizite Eingaben; bei unveränderten Eingaben entstehen dieselben Tree-/Commitobjekt-IDs. Signierung und Hooks werden nicht aus Repository-Konfiguration übernommen.

Wichtig: Die Commitzeit muss später aus einer dauerhaft gespeicherten Annahme stammen, nicht aus der Uhrzeit des Wiederholungsversuchs. Diese Methode veröffentlicht **keinen Branch** und ersetzt weder frische ACL-Prüfung noch Datenbank-Fencing. Sie ist intern und noch nicht als vollständiger Provider registriert.

## Lokale Prüfung

Technische Referenzen für den checkoutfreien Objektaufbau: [Git hash-object](https://git-scm.com/docs/git-hash-object), [Git update-index](https://git-scm.com/docs/git-update-index), [Git commit-tree](https://git-scm.com/docs/git-commit-tree).

Reale Git-HTTPS-Fixture mit unabhängigem `git show`, `ls-tree` und Parent-Abgleich: ursprüngliche CRLF-Bytes, unveränderte Attribute/Submodule-Metadaten/Gitlinks, keine Ausführung manipulierter Hooks und identische Commit-ID bei Wiederholung. Zusätzliche Negativfälle: Default-Branch und Case-Kollision.

Der erste HTTPS-Lauf hatte drei Fehler beim Windows-Zertifikatskettenaufbau bereits im Serverstart. Jede isolierte CA erhält jetzt einen eindeutigen Subject-Namen statt des gemeinsam genutzten Namens. Erneuter HTTPS-/Commit-Lauf: **6 bestanden, 0 Fehler, 0 übersprungen** (32,852 s), ohne Abschalten von TLS-/Revocation-Prüfungen. Der gemeinsame Windows-Chain-Cache ist eine plausible Erklärung; keine abschließende Betriebssystemdiagnose behauptet.

## Offen

Separate lokale Adapter-Suite nach zusätzlichen Negativfällen: **63 bestanden, 0 Fehler, 0 übersprungen** (33,219 s). PostgreSQL-Fälle explizit nicht ausgewählt, nicht als bestanden gerechnet.

Solution-Release-Build mit unveränderten Editorassets (`SkipBpmnIoAssetBuild=true`): **0 Fehler, 65 bestehende Warnungen**, 70,43 s. Kein warning-freier Build behauptet.

Unveränderte CI-safe Regression entsprechend `.github/workflows/ci.yml`, nach erfolgreichem Build mit `--no-build --no-restore`: **1.388 gesamt, 1.380 bestanden, 0 Fehler, 8 bestehende externe Opt-in-Fälle übersprungen**, 101,555 s. Keine lokalen Native-Git- oder PostgreSQL-Voraussetzungen in den Workflow aufgenommen.

- Dauerhafter Worker: Annahmezeit/typisierten Request laden, ACL erneut prüfen, Lease/Fence sichern, erwartete Commit-ID/Receipt dauerhaft speichern.
- Lokale Branch-Publikation per Compare-and-Swap und Crash-/Antwortverlust-Reconciliation.
- Push mit erwarteter Remote-Ref, anschließend echte GitHub-Abnahme.
- Gesamte G02/G03/G04-Abnahme weiterhin nicht abgeschlossen. PostgreSQL für diesen neuen Teil nicht erneut geprüft; SQL Server auf Nutzerwunsch nicht geprüft.
- Bestehende Studio-Bundles unverändert gelassen und nicht Teil dieses Änderungspakets. CI-Workflow unverändert; native lokale Tests nicht in reguläre PR-Tests aufgenommen.
