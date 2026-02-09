# Spec – REST Coverage

Muss
- Einheitliche Query-Parameter: `firstResult`, `maxResults`, `sortBy`, `sortOrder`
- Filter: idIn, keyLike, businessKey, variable(name, op, value), state, tenantId
- Zeitformat: ISO 8601 UTC (`yyyy-MM-ddTHH:mm:ss.fffZ`)
- Fehler: `{ code, message, details, traceId }`
- Konsistenz: IDs sind strings (ULID/UUID), keine Longs

Abnahme
- Contract-Tests decken ≥95% der Routen ab
- Beispielqueries geben erwartete Slices zurück
