# Tipps und Tricks

## BPMN-Modelle klein beginnen

Starte mit Start Event, einer Aktivität und End Event. Füge Gateways, Timer und Subprozesse erst hinzu, nachdem der einfache Pfad deploybar und ausführbar ist.

## Prozessschlüssel stabil halten

Der Prozessschlüssel wird zum Starten verwendet. Ändere ihn nicht bei jeder Modelländerung. Nutze stattdessen Versionen der Prozessdefinition.

## Idempotente Aufrufer bauen

Nach einem Timeout kann ein Aufruf bereits erfolgreich auf dem Server angekommen sein. Verwende eine Business-Key-Strategie und prüfe bestehende Instanzen, bevor ein Aufrufer blind erneut startet.

## Mandanten konsequent setzen

Wenn Multi-Tenancy aktiviert ist, muss `tenantId` beim Deployen, Starten und Abfragen konsistent verwendet werden.

## Erst Health, dann Fachaufruf

Bei Deployments und Startups zuerst `GET /api/Health` prüfen. Für Kubernetes sollten Readiness und Liveness nicht mit einem fachlichen Prozessaufruf simuliert werden.

## Swagger als Vertrag nutzen

Bei API-Änderungen Controller, DTO und OpenAPI-Ausgabe gemeinsam prüfen. Client- und Serverversion sollten aus demselben Release stammen.

## Logs und Auditdaten

Mutierende HTTP-Aufrufe werden auditiert. In Logs und Prozessvariablen gehören keine Passwörter, Tokens oder vollständigen personenbezogenen Dokumente.

## Studio für visuelle Kontrolle

Das Studio ist hilfreich für BPMN-XML und Form-Schemas. Für reproduzierbare Deployments bleibt die versionierte BPMN-Datei in Git die führende Quelle.
