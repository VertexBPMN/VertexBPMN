# GS04: profilgebundene Bearer-Credentials

Stand: 12. Oktober 2026. Branch `codex/git-generic-servers`, Basis `02ebe35`, lokaler uncommittierter Kandidat. Fortsetzung des [standardbasierten Bearer-IPC-Nachweises](2026-10-12_Git-GS05_Hostingregistry-und-Bearer-IPC.md).

## Produktpfad

Der bereits unter Windows nativ geprüfte Bearerkanal ist jetzt am profilgebundenen Produktbroker angeschlossen. `HttpBearer` unterstützt HTTPS-Lese-, Commit- und Push-Aufträge im vorhandenen geschützten Ausführungsvertrag Version 2. API-/Studio-Verfügbarkeiten berücksichtigen die Profilfähigkeiten und aktuellen Grants. Legacy-Worker übernehmen diese Aufträge nicht.

Credentialvertrag: Typ `GitHttpBearer`, geschützte Felder `token` und `expiresAt`. Die tatsächliche Ablaufzeit muss als Roundtripwert von `DateTimeOffset.ToString("O", CultureInfo.InvariantCulture)` vorliegen. Ungültige, überlange oder in weniger als 30 Sekunden ablaufende Zeitwerte verhindern bereits die Tokenauflösung. Eine Lease gilt maximal zwei Minuten und nie länger als die angegebene Ablaufzeit. Vertex beschafft oder erneuert dieses Token nicht; automatische Entra-Anmeldung und neue Identitätsquellen sind nicht angeschlossen.

Der Broker prüft aktuellen Actor, Tenant, Grants, Ziel und Profil vor sowie während der Secretauflösung. Credentialtyp, Referenz, Tenant, Pflichtfelder und Metadatenrevision werden nach Tokenauflösung erneut verglichen. Widerruf oder Rotation ergeben einen sicheren Fehler statt einer neuen Lease. Bekannte Fehlercodes und Caller-Cancellation bleiben erhalten; Backendfehlermeldungen werden nicht veröffentlicht.

Die ursprünglichen Git-/Netzwerk-/CA-Zielbelege bleiben unverändert gebunden. Keine Tokenwerte in Aufträgen, Profilen, Argumentlisten, Prozessumgebung oder persistierter Gitkonfiguration; kein Basic-Fallback. Unbekannte Hostingadapter erhalten dadurch keine PR-Fähigkeit.

## Änderungen

- `ProfiledGitCredentialBroker.Bearer.cs` und gezielte Auswahl in `ProfiledGitCredentialBroker.cs`.
- HTTPS-Auftragsannahme, Commit-/Push-Vertragsprüfung und Repository-Verfügbarkeit akzeptieren zusätzlich `HttpBearer`.
- Zehn neue Fälle in `SourceControlConnectionBindingTests.Bearer.cs`: geschützte Read-/Commit-Aufträge, Sperre für Legacyclaiming, korrekte Ausgabe, abgelaufene/falsche Zeitwerte, falscher Credentialtyp/Tenant und Rotation/Rollen-/Profilwechsel während der Auflösung.
- Testcredential-Datenquelle und Commitfixture gezielt um Bearer erweitert; bestehende Basicfälle bleiben unverändert aktiv.
- [Betriebsrunbook](../runbooks/git-connection-profiles.md), lokale Wiki-/Website-Dokumentation ergänzt. Dokumentationsbranches bleiben `codex/git-generic-servers-docs`; keine Veröffentlichung.

## Lokale Prüfung

- Gesamtbuild: **erfolgreich, keine Warnungen/Fehler**, 40,66 Sekunden, Release/`--no-restore`, unveränderte Editorassets übersprungen.
- Zentraler Testprojektbuild nach den letzten zwei Auftragsfällen/Formatkorrekturen: **erfolgreich**, keine Warnungen/Fehler, 33,24 Sekunden. Testdiscovery bestätigt den zusätzlichen Auftragsfall; kein null-entdeckter Nachweis.
- Gezielte vollständige Profil-/Security-/Hosting-/Persistenz-Auswahl: **392 bestanden**, keine Fehler/Skips, 36,567 Sekunden; inklusive aller zehn neuen Bearerfälle.
- Check-only-Formatprüfung der zwei neuen Broker-/Testdateien: erfolgreich. D1–D7 für acht konkrete geänderte Dateien ausgeführt; alle acht Clean-Code-Regeln zusätzlich im betroffenen Code manuell geprüft. Kein bestätigter offener Wartbarkeitsbefund im geprüften Umfang; keine Aussage über den kompletten angesammelten Featurebranch.
- `git diff --check` in Produkt, Wiki und Website erfolgreich.
- Native 131 Git-/IPC-/Hostingprüfungen stammen aus der unmittelbar vorherigen Kanalumsetzung. Der native Kanal selbst blieb in dieser Fortsetzung unverändert; ein kombinierter echter Anbieter-/API-/Studio-Lauf ist damit weiterhin nicht abgenommen.

Erste breite CI-Auswahl dieser Fortsetzung: 1.769 Fälle, **1.760 bestanden, ein Fehler, acht externe Skips**, 1:57,278. Der vorhandene C.8.0-Diagnosetest zeigt jetzt die ursprüngliche Ausnahme: `Jint.Constraints.TimeConstraint.Check` beendet `vertexFeelEvaluate` nach dem bestehenden zweisekündigen Limit. Erst danach meldet das Gateway die fehlende Variable `Vacation Approval`. Der Timeout ist damit als Ursache dieses konkreten Fehlerlaufs belegt; eine belastbare Behebung der intermittierenden Laufzeit-/Lastproblematik ist nicht durchgeführt. Weder Zeitlimit noch Conformance-Assertions oder Fixtures wurden abgeschwächt.

Nach dem abschließenden Testprojektbuild und den zwei zusätzlich entdeckten Auftragsfällen wurde die identische CI-safe-Auswahl erneut ausgeführt: **1.763 bestanden, keine Fehler, acht externe Skips**, 1.771 insgesamt, 1:36,103. Der vorherige FEEL-Timeout ist dadurch nicht als behoben nachgewiesen; die Laufzeitstabilität bleibt offen. Logs liegen ignoriert unter `artifacts/git-generic-servers/gs05-bearer-profile-*.log`.

## Offene Gesamtarbeit

Profilgebundene Hostingaufträge mit geschützter Provider-/API-/Repositoryidentität, automatische Entra-Tokenbeschaffung, Azure-/GitLab-/Gitea-/Forgejo-/Enterpriseadapter, SSH und Linux-Abnahme bleiben offen. Ebenso echte gemeinsame API-/Studio-/Server-Recovery-Abnahme und die C.8.0-Laufzeitstabilität. Dies ist keine Support- oder Produktionsfreigabe.
