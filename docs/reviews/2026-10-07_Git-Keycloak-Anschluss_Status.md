# Git-Integration: OIDC/Keycloak – Zwischenstand

## Verbindliche Entscheidung

Der Benutzer hat OIDC/Keycloak als Identitätsquelle für Git-Aufträge gewählt.
Die aktuelle Implementierung verwendet dafür einen austauschbaren Keycloak-Admin-REST-Adapter;
OIDC selbst definiert keine Benutzerverwaltungs-API. Es gibt keinen lokalen Benutzer-Fallback.

Der Adapter verwendet den konfigurierten JWT-Issuer und einen mandantengebundenen,
geschützt gespeicherten `KeycloakAdmin`-Credential-Verweis mit `clientId` und
`clientSecret`. Benutzerstatus, eindeutiges `tenant_id` und die effektiven
Client-Rollen des konfigurierten API-Audiences werden ohne Rollen-Cache gelesen.
Repository-ACLs bleiben zusätzlich verbindlich. Dauerhafte Commit-Aufträge
werden an die konfigurierte Authority gebunden.

## In Arbeit, nicht vollständig abgenommen

Provider, API-Endpunkte, Hintergrundverarbeitung und Studio-Git-Panel sind im
Arbeitsbaum vorhanden. Dies ist kein Abschlussnachweis für G04/G05/G06.
Insbesondere offen:

- Lokale Live-Abnahme für Rollenentzug, geerbte Gruppenrollen und lesenden Service-Account bestanden; verschachtelte Composite-Rollen und Push-spezifische Abnahme weiterhin offen. Siehe [Abnahmebericht](2026-10-07_Keycloak-Lokale-Abnahme.md).
- API-Negativtests sowie vollständiger Provider- und sichtbarer Studio-End-to-End-Lauf.
- Prüfung der Read-Job-Bereinigung und Fehlerpfade nach einer übernommenen Lease.
- Verifikation der Session-Fortschreibung anhand des bestätigten Push-Ergebnisses.
- Wiederaufnahme nach verlorener Snapshot-Antwort beziehungsweise unterbrochener Studio-Verbindung.
- Vollständige Abschlussprüfung und Regression; vorhandene Analyzer-Schulden sind nicht durch diagnostische Builds gelöst.

## Aktuell geprüfter Teil

`KeycloakSourceControlActorResolverTests`: sechs bestanden, null Fehler,
null übersprungen. Die Tests verwenden einen kontrollierten HTTP-Handler,
keinen echten Keycloak-Server. Nachgewiesen sind erneute Rollenabfragen,
Filterung auf bekannte API-Client-Rollen, Ablehnung deaktivierter oder einem
anderen Mandanten zugeordneter Benutzer sowie ungültiger Authority-Konfiguration.

Der Testprojekt-Build bestand mit ausgeschalteten Analyzern. Das ist kein
Nachweis eines erfolgreichen strikten Builds.

Im Studio wurden außerdem die XML-Zuordnung zum unveränderlichen Commit-Snapshot
und die erneute Push-Freigabe desselben bereits veröffentlichten Commits korrigiert.
Diese UI-Korrekturen sind noch nicht im Browser abgenommen.

Keine neuen Commits, Pushes oder externen Ressourcen in diesem Arbeitsschritt.
