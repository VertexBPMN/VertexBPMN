# A01 – Architektur- und Security-Review

Stand: 2026-09-12. Gegenstand: [External-Task-Vertrag v1](2026-09-12_External-Agent-A01_Vertrag.md).
Reviewart: separates dokumentiertes Selbstreview des Entwurfs gegen A00 und E01–E14; keine unabhängige Zweitprüfung und keine Laufzeitabnahme.

| Prüfpunkt | Befund und festgelegte Behandlung | Umsetzungsnachweis |
|---|---|---|
| OIDC-Integration | Bestehende Rollen-Allowlist würde Workerrolle entfernen. Normalisierung und explizite Bearerpolicy gemeinsam erweitern; Adminrolle allein reicht nicht. | A03: echte Handlerfälle für gültige/ungültige Token, Rolle, Issuer, Tenant, Topic, Profil |
| Identität | Body-WorkerId ist keine Autorität. Bindung an validiertes `(iss, sub)` und Tenant; keine Wildcards. | E07, einschließlich Completion-Replay durch fremden Subject |
| Lease-Fencing | LockOwner allein schützt nicht vor alten Requests desselben Workers. Lease-ID plus Generation in jeder Mutation prüfen. | E03/E04 mit zwei DbContexts und gleicher Workeridentität |
| Verlorene Antworten | Wiederholtes Complete/Fail darf weder doppelt fortsetzen noch Retrybudget erneut ändern. Receipts enthalten Requestidentität und serverseitigen Hash. | E05; Crash nach Commit/vor HTTP-Antwort |
| Claim-Verlust | Eine verlorene Claim-Antwort verbraucht einen Versuch; automatische Rückgabe derselben Lease wäre ohne Requestvertrag unsicher. Kontrollierter Ablauf/Retry und Ratelimit sind definiert. | E08/E11; dokumentierte mögliche Erschöpfung bei maxRetries=0 |
| Abbruch gegen Complete | Job-CAS allein reicht nicht. Prozess und Wait müssen dieselbe Sperr-/Revisionsordnung verwenden. Pending-Fortsetzung kann nach Ergebnisannahme noch annulliert werden. | E06/E10, beide Reihenfolgen mit Barrieren |
| Deadline | Prüfung nach Sperrerwerb, Gleichheit ist abgelaufen. Serverseitige Zeit und absolute Deadline; Heartbeat verlängert sie nicht. | E04/E08 mit steuerbarer Zeit; Mehrreplikat-Clock-Skew bleibt Betriebsgrenze |
| Fortsetzung | Persistenter Datensatz in derselben DB beseitigt Brokerpflicht, verlangt aber einen echten Consumer und Recovery für Pending-Tokens. Keine anschließenden Inline-Netzwerkhandler in dieser Transaktion. | E06/E11 mit Prozesskill; A04 ggf. Runtime-Aufteilung |
| Multi-Instance/Loops | Element-/Token-ID allein ist ungeeignet. Eigene persistente ActivityExecutionId je Eintritt und lokaler Scope; keine freien Outputvariablen. | E02/E09, Wiederholung desselben Eintritts und neue Schleifeniteration |
| JSON/Schema | Doppelte Keys abweisen, UTF-8-Größenlimit und deterministischer Hash; 1/1.0 bewusst verschieden. Schema/Policy nach Vorvalidierung transaktional erneut binden. | E05/E13, Größen-/Tiefengrenzen und Schemawechsel-Race |
| SQLite/PostgreSQL | Numerische UTC-Zeit vermeidet DateTimeOffset-Sortierprobleme; neue Tabellen additiv, Upgrade mit bestehenden Daten. | A02: beide Provider und Downgrade-Guard |
| Dokument/Prompt | Jobgebundener Zugriff und keine Inhalte in Logs. Bereits gelesene Worker-Daten lassen sich durch Leaseverlust nicht zurückrufen. | E07/E12; Logprüfung, Egressprüfung in A05/A07 |
| Aufbewahrung | Produktfristen sind nicht beschlossen. Pilotwerte sind Vorschläge; Produktion benötigt explizites Profil. Receiptfrist begrenzt Replaygarantie, Pending-Arbeit darf nie gepurged werden. | A05/A07: Cleanup, Tombstone, Replay nach Ablauf, Backup-Retention |
| Modellserver | Private Ziele nur im getrennten Workerprofil mit exakter Allowlist, keine Redirects/Cloud-Fallbacks. Bestehender Connector-SSRF-Schutz bleibt intakt. | A05: reale Adapter-/Netzwerknegativtests |
| Unsupported Engines | Simple/Legacy-Distributed dürfen den Task nicht als normalen Service Task durchlaufen lassen. Prüfung auch für Subprozesse und Call Activities. | A02: explizite Ablehnung vor Seiteneffekten |

## Ergebnis

Der Entwurf ist als Grundlage für A02 verwendbar. Es bestehen keine unentschiedenen generischen Protokollfragen, die Job-/Wait-Modellierung blockieren. Die Umsetzung von Policy, Leasing und Continuation bleibt A03/A04 zugeordnet und ist noch nicht erfolgt. Der Begriff „A01 abgeschlossen“ bezeichnet die Vertragsarbeit einschließlich dieses Reviews.

Die Produktentscheidungen für A05 bleiben offen: fachlicher Umfang und Prüfer, Runtime/Modell/Hardware, Dokumentstore und Aufbewahrung, Güteschwellen und Ressourcenlimits. Ohne diese Entscheidungen ist keine echte Agent-/Produktionsabnahme möglich.

Für diese Dokumentänderung wurden keine neuen Laufzeittests ausgeführt. Die 39 bestandenen Tests aus A00 stammen aus einer vorhandenen Assembly und werden hier nicht als erneuter Testlauf oder Nachweis des neuen Vertrags gewertet. Prüfung dieses Pakets: Integrationsstellen gelesen, Zustands-/Fehlermatrix und Race-Reihenfolgen geprüft, Dokumentverweise und Git-Whitespace geprüft.
