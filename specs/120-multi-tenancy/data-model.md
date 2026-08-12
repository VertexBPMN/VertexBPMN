# Data Model – Tenancy

Tenancy gilt als Partition Key:
- Spalten: tenant_id NOT NULL in allen Kern-Tabellen
- Composite-Indizes: (tenant_id, business_key), (tenant_id, key, version)
- Foreign Keys tragen tenant_id (FK-Konsistenz prüfen)

Migration
- ALTER TABLE … ADD COLUMN tenant_id TEXT NOT NULL DEFAULT 'default'
- Danach DEFAULT entfernen, Indizes anlegen
