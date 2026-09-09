# VertexBPMN – Phase 0: Entscheidungsvorlage Zielumgebung

Stand: 2026-09-09. Dieser Bericht bündelt die laut `Produktionsprofil` (2026-09-08) noch offenen
Nutzerentscheidungen, die Pilotbetrieb (P8_AC_03), Zielumgebungs-Installation (P8_AC_04) und die
produktive Freigabe freischalten. **Keine der Optionen ist hier entschieden** – es ist eine
Entscheidungsvorlage mit Empfehlung (Empfehlung zuerst geführt) und Abnahmekonsequenz je Option.
Dieses Dokument enthält **keine** Secrets/Produktions-Verbindungsdaten.

## Offene Entscheidungen (aus dem Produktionsprofil)

| # | Entscheidung | Aktueller Stand |
|---|---|---|
| E1 | Produktionshosting, Betriebssystem, Ressourcen | offen (K8s laut Runbook eine Option) |
| E2 | Identity Provider + TLS-Terminierung | offen |
| E3 | Secret Store / Keyring | offen |
| E4 | Backupintervall, Aufbewahrung, Verantwortliche | offen |
| E5 | Alarmempfänger / -schwellen | offen (Phase 8) |
| E6 | Maximale Timer-Verzögerung (Betriebsziel) | offen (Vorschlag p95 ≤ 30 s) |
| E7 | Wartende Instanzen / Historienwachstum quantifizieren | offen |

Bereits entschieden (übernommen): PostgreSQL + RabbitMQ als Zielprofil; 2 API-Replikas + 1 Worker;
mittel (1–10 Starts/s, 50–500 Benutzer); Verfügbarkeit 99,5 %; API-p95 < 1 s / p99 < 3 s;
RPO ≤ 15 min / RTO ≤ 4 h.

## E1 – Hosting / Betriebssystem / Ressourcen

- **(Empfohlen) Kubernetes (k3s/EKS/AKS) gemäß `docs/runbooks/production-deployment.md`.**
  Runbook deckt getrennten Migrations-Job, externe DB/Broker und gemeinsamen Data-Protection-Keyring
  ab; skaliert zu den 2+1-Replikas. Konsequenz: Zielcluster-Abnahme (Phase 4–6 lokal nachgestellt,
  echter Cluster noch offen) + Ressourcenprofil aus Kapazitätsplan (Phase 6: p95 351 ms / p99 574 ms
  gemessen, 232 MB Speicher stabil) hochskalieren.
- 2 VMs + Docker-Compose: einfacher, aber kein eingebautes HA/Rollout; Registry-SQLite-Konkurrenz
  bei 2 Replikas ist dann hands-off zu betreiben (Phase-4-Befund beachten).
- Einzel-VM: nicht konform zu 2-API-Replika-Zusage; nur für Vorproduktions-/Pilot-Ersatz.

**Abnahme-Konsequenz:** Erst mit Zielumgebungsentscheidung sind P8_AC_03 (Pilot auf echter
Umgebung) und P8_AC_04 (Installation der Artefakte) abnahmefähig. Bis dahin liegen nur die
lokalen Nachweise vor (Artefakte gebaut + Paket-/CLI-Test, Alarmsignalquellen lokal gefeuert).

## E2 – Identity Provider + TLS

- **(Empfohlen) OpenID-Connect-Provider (keycloak, Entra ID, Zitadel) mit Authorization-Code +
  PKCE.** Erfüllt die Phase-1/3-Anforderung „echter IdP“; OAuth2-State/Token-Endpunkt sind bereits
  implementiert und gegen simulierten Endpunkt getestet (echter IdP noch offen, Phase 3 Req 2).
- Lokal nutzbare Alternative: eingebauter Entwicklungs-IdP (nur Pilot/Vorproduktion).

**Abnahme-Konsequenz:** Reale IdP-Abnahme (Login, Token-Ablauf, Rollenentzug, verweigerte Zugriffe)
in Phase 3 erfordert E2. TLS-Terminierung am Ingress/Proxy mit Zeitziel (z. B. 90-Tage-Rotation)
festzulegen.

## E3 – Secret Store / Keyring

- **(Empfohlen) Dedizierter Secret Store (HashiCorp Vault / Kubernetes-Secrets + externe Quelle /
  Cloud-KMS), referenziert über Env-Variablen.** Data-Protection-Keyring gemeinsam (Runbook). Der
  Secret-Store-Provider (ConfigurationSecretProvider) existiert; produktive Provider-Abnahme offen.
- Vermeiden: Secrets im Repo/Image/Alarmtext (verletzt Phase-8-P8_AC_02).

## E4 – Backup / Aufbewahrung

- **(Empfohlen) pg_dump-basiert, Intervall ≤ 15 min (RPO), Aufbewahrung z. B. 14 tägliche + 12
  monatlich, wöchentlicher Restore-Probelauf.** Phase 5 hat lokalen Restore mit RTO ≈ 3,9 s / RPO 0
  gemessen; Zielwerte für die Zielumgebung real zu messen. Verantwortliche Person/Rolle benennen.

## E5 – Alarmempfänger / -schwellen

- **Empfohlen:** Offset der lokalen Alarmschwellen (`docs/runbooks/alerts.md`) mit Zielumgebungs-
  Werten bestätigen; Empfänger (OnCall/PagerDuty/E-Mail-Verteiler) benennen. Phase 8 hat die
  Signalquellen lokal nachgewiesen; Verantwortliche sind eine Organisationsentscheidung.

## E6 – Maximale Timer-Verzögerung

- **(Empfohlen) p95 ≤ 30 s** (How-to aus Phase 6 gemessen: Timer-Boundary feuert über
  PollingScheduler ~5 s + JobExecutor). Ein größerer p95-Wert reduziert die Alarm-Empfindlichkeit.

## E7 – Wartende Instanzen / Historienwachstum

- Wartende Instanzanzahl und Historien-Aufbewahrung je Mandant quantifizieren, bevor das
  Kapazitäts-/Aufbewahrungsprofil finalisiert wird (beeinflusst E4 und Phase-6-Grenzen).

## Nächste Schritte zur Freigabe (Checkliste)

1. E1–E6 entscheiden (dieses Dokument ausfüllen/kommentieren).
2. Zielumgebungs-Installation der gebauten Artefakte (GitHub-Release bzw. NuGet-Feed) + SDK/CLI
   aus Paketen auf der Zielumgebung (P8_AC_04 Rest).
3. Pilot mit 1–3 vereinbarten realen Geschäftsabläufen + Beobachtungsdauer (z. B. 2 Wochen),
   Alarmierung/Wiederherstellung live prüfen (P8_AC_03).
4. Versionierten Freigabebericht auf `master` @ Zielcommits finalisieren und produktiv rollen.
