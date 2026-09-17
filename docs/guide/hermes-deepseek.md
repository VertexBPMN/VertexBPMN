# Hermes mit DeepSeek für VertexBPMN

## Projektkontext laden

Hermes lädt die `AGENTS.md` aus dem aktuellen Arbeitsverzeichnis beim
Sitzungsstart. Das ist unabhängig vom ausgewählten API-Modell. Deshalb Hermes
im Repository-Stamm starten, nachdem die Dateien im eigenen Checkout vorhanden
sind. Dein Hermes läuft unter Ubuntu auf einer Azure-VM; maßgeblich ist deshalb
der Checkout auf dieser VM, nicht der Windows-Checkout:

```bash
# Den tatsächlichen Repository-Pfad auf der VM einsetzen.
cd /pfad/zum/VertexBPMN
pwd
git status --short
git branch --show-current
test -f AGENTS.md && test -f docs/guide/hermes-deepseek.md
hermes
```

Die Dateien zuerst committen/pushen und auf der VM in den gewünschten Branch
übernehmen. Bei sauberem `master` kann dies mit `git pull --ff-only origin master`
erfolgen. Bei lokalen Änderungen oder einem anderen Branch erst den Stand prüfen;
keinen Reset und keinen erzwungenen Branchwechsel durchführen.

Eine neue Sitzung starten, falls die Agent-Datei erst nach Sitzungsbeginn
hinzugefügt wurde. Zum Kontrollieren Hermes bitten, die geladene `AGENTS.md`
und den aktuellen Branch zu nennen. Eine Datei namens `DEEPSEEK.md` ist nicht
erforderlich; deren automatische Erkennung wird hier nicht vorausgesetzt.

Die vorhandene DeepSeek-API-Konfiguration bleibt in Hermes. Modell-ID, API-Key
und persönliche Hermes-Konfiguration gehören nicht ins Repository. Diese Dateien
ändern weder Modellwahl noch Hermes-Berechtigungen und garantieren keine
fehlerfreie Umsetzung.

## Voraussetzungen auf der Ubuntu-VM

- Den durch `global.json` geforderten .NET-SDK-Stand mit `dotnet --info` prüfen;
  auch Node/npm und Browser-Abhängigkeiten nur für die jeweiligen Arbeitspakete
  anhand der Projektdateien ermitteln.
- Befehle in Bash ausführen und Linux-Pfade/Großschreibung beachten. Windows-
  Laufwerksbuchstaben und `wslc.exe` sind auf dieser VM keine lokalen Werkzeuge.
  Bestehende WSLC-Unterstützung im Produkt trotzdem unverändert erhalten.
- Für externe Tests explizite Testinstanzen von PostgreSQL/RabbitMQ konfigurieren.
  Vorhandene Datenbanken oder Azure-Ressourcen nicht als entbehrliche Testdaten
  behandeln. Ohne Testdienste bleiben entsprechende Abnahmen offen.
- Eine der VM zugewiesene Managed Identity kann durch Azure-Credential-Erkennung
  erreichbar sein. Für Cloudtests Identität, Abonnement und isolierte Testressourcen
  ausdrücklich auswählen. Keine beiläufigen Zugriffe auf Produktionsressourcen;
  eine VM-Identität ist keine Deploymentfreigabe.
- Browsertests benötigen Playwright-/Browser-Voraussetzungen auf Ubuntu. Ein
  Headless-Lauf auf der VM ist kein vom Benutzer beobachteter sichtbarer UI-Test.

## Startprompt für die Azure-Aufgabe

```text
Lies AGENTS.md und den vollständigen Plan
docs/reviews/2026-09-17_Azure-Deployment-und-Service-Bus-Umsetzungsplan.md.
Prüfe Branch, Arbeitsverzeichnis und den tatsächlichen Implementierungsstand.
Du arbeitest unter Ubuntu auf einer Azure-VM. Verwende Bash und den dortigen
Checkout; erhalte dennoch Windows-/WSLC-Kompatibilität des Projekts.

Beginne mit P0 und dem Vertragsinventar aus P3. Arbeite für die Implementierung
auf einem eigenen Feature-Branch, z. B. hermes/azure-service-bus.
Dokumentiere technische Entscheidungen und kennzeichne offene Angaben zu
Azure-Abonnement, Region, Budget und Identity Provider als offen.
Bereite danach P1 mit gezielten Regressionstests vor und implementiere den
Service-Bus-Outbox-Adapter gemäß Plan. Erhalte alle lokalen Profile.

Führe relevante Tests aus und dokumentiere Ergebnisse im Plan. Erstelle keine
Azure-Ressourcen. Commit, Push und Deployment sind nicht Teil dieses Auftrags.
Berichte zum Abschluss konkrete Änderungen, Testergebnisse und offene Punkte.
```

Für die nächste Phase denselben Plan wieder einlesen lassen und das konkrete
Arbeitspaket nennen. Nach Kontextkomprimierung gehören Branch, geänderte Dateien,
Testergebnisse, offene Entscheidungen und nächster Schritt in die Übergabe.
Die Dateien im Repository bleiben maßgeblich gegenüber einer älteren Chat-Zusammenfassung.

## Abnahme

Der Agent soll jedes Ergebnis mit Code- und Testnachweisen belegen. Insbesondere
Inbox-Transaktionen, Refresh-Token-Rotation, Datenmigration und Rollback unabhängig
reviewen lassen. Fehlende lokale Dienste oder fehlender Azure-Zugang werden als
ausstehende Prüfung erfasst. Tests nicht abschwächen, um diese Lücken zu verdecken.

Projektregeln stehen zentral in [AGENTS.md](../../AGENTS.md); Phasen und
Abnahmekriterien im [Azure-Plan](../reviews/2026-09-17_Azure-Deployment-und-Service-Bus-Umsetzungsplan.md).

Quellen, geprüft am 17.09.2026:

- [Hermes: Context Files](https://hermes-agent.nousresearch.com/docs/user-guide/features/context-files)
- [Hermes: Projektkontext und Discovery](https://hermes-agent.nousresearch.com/docs/guides/tips#context-files)
