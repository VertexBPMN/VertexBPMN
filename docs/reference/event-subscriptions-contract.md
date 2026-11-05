# LC09 Slice A: persistierte Subscription-Reads

Status: Vertrags-/Persistenznachweis, **keine vollständige LC-AC-13-Abnahme**.

- `Runtime:EventSubscriptions:Enabled` bleibt standardmäßig `false`. HTTP-Create/Verwaltung bleibt offen; Tests legen tenant-owned Subscriptions über Persistenz an. Keine LC09B-Endpunkte.
- Projektion verwendet ausschließlich `Published`-Outboxzeilen mit `PublishedAt` und passender Tenant-ID. Der bestehende Publisher bleibt unverändert.
- PostgreSQL-Projektoren sperren die Subscription mit `FOR UPDATE` vor Auswahl/ID-Vergabe bis Commit. Zwei Serviceinstanzen erzeugen deshalb keine doppelte durable Zeile und kein späteres Commit mit kleinerer Position derselben Subscription. Unique `(SubscriptionId, SourceEventId)` bleibt letzte Sicherung.
- Replay-Reihenfolge ist die **persistierte Projektionsposition**, nicht globale Ereigniszeit. Innerhalb eines Batches: `OccurredAt`, dann Source-ID. Gleichzeitige Ereignisse bleiben paginierbar. Spät publizierte ältere Ereignisse werden hinten angehängt, niemals vor einen bereits ausgegebenen Cursor einsortiert. Keine globale chronologische Sortiergarantie über Publisher-Batches.
- `GET /api/event-subscriptions/{id}/events` liest höchstens 100 Zeilen. Zuerst Tenant-Ownership (404 bei fremder Subscription), danach Cursorvalidierung (400). Cursor sind keine Berechtigung.
- Optional `definitionId=<Guid>` filtert exakt auf die tenantgeprüfte `ProcessInstance.ProcessDefinitionId`, die bei Erstprojektion gespeichert wird. Dies ist die konkrete persistierte BPMN-Version, nicht ein erfundener Versionsstring oder eine Auflösung auf `latest`. Ohne passende Instanz/Definition bleibt die Metadatenbindung NULL und passt keinem Versionsfilter. Snapshot bei Projektion, **kein behaupteter Eventzeit-Snapshot vor einer Prozessmigration**. Release-ID-/Versionsbereichsfilter sind nicht Bestandteil dieses Checkpoints.
- Ungefilterte Cursor bleiben `v1:<subscription-N>:<position>`. Gefilterte Cursor sind `v2:<subscription-N>:<position>:<definition-N>`; Filterwechsel/Entfernen mit demselben Cursor: 400. Für einen anderen Filter ausdrücklich neu ohne Cursor lesen.
- Lesen verändert weder Subscription-ACK noch Consumerzustand. ACK-Endpunkt existiert nicht. Verbraucher speichern Read-Cursor selbst; gleiche Reads sind innerhalb der Retention wiederholbar.

## Retention und Grenzen

Payload-Retention: sieben Tage ab `RetainedAt` (Erstprojektion), unabhängig von verspätetem `OccurredAt`/Publisherabschluss. Bei jedem Projektionslauf wird der abgelaufene Positionspräfix redigiert (`Payload` leer) und `ExpiredThrough` monoton fortgeschrieben. Identitäts-Tombstones bleiben bis Subscription-Löschung erhalten, verhindern Wiederprojektion aus weiterhin vorhandener Outbox und sind **nicht zeitlich oder mengenmäßig begrenzt**. Dies ist begrenzte Payload-Historie, keine behauptete Obergrenze für gesamten DB-Speicher; Outbox-Retention bleibt separat. Bereits bestehende Projektionen erhalten beim Migrationsupgrade eine neue siebentägige Frist.

Cursorposition kleiner als `ExpiredThrough` (auch Replay ohne Cursor mit Position 0): HTTP **410**, Body `code=event_cursor_expired`, kein Ersatzcursor/automatisches Fast-forward. Exakt am Präfixende darf weitergelesen werden. Die Grenze gilt subscriptionsweit auch für gefilterte Reads konservativ. HTTP-Reads halten `FOR SHARE` während Grenzprüfung und Payloadabfrage; Retention kann nicht dazwischen unbemerkt Historie entfernen. Bereinigung erfolgt im nächsten erfolgreichen Projektionslauf; bei gestopptem Worker ist die physische Redigierung verzögert, kein harter Echtzeit-Lösch-SLA.

## Evidenz / offene Abnahme

Isoliertes migriertes PostgreSQL und eigener RabbitMQ, Secrets nur in privatem Scratch. Testprincipal ist ausschließlich HTTP-Vertragsevidenz, kein JWT/OIDC. Host-Neuerstellung ist In-Process-Lifecycle, kein echter Prozessrestart. Unique Projektion beweist keine unabhängige einmalige Fachwirkung.

Offen: JWT/OIDC und Rollen-Negativtests, echter Kill/Restart, unabhängige doppelte Fachwirkung, HTTP-Create/Verwaltung mit Policytests, ausführbarer HTTP-Katalog, Release-ID-Filter/Eventzeitbindung bei Migration, Betrieb/Last/Tombstone-Kompaktion. LC09B und vollständige AC13 bleiben offen.

RED/GREEN, Dependency-Builds, Regression und Fortschritt: `/home/azureuser/.hermes/cache/scratch/lc09-retention/`; vorheriger Publishercheckpoint: `/home/azureuser/.hermes/cache/scratch/lc09-recovery/`.
