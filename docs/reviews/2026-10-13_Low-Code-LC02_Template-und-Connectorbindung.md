# LC02 — Template- und Connectorbindung (Fortschritt)

Stand: 2026-10-13. Paket **LC02 (M)** gemäß Low-Code-Plan §5. Ziel: ein im Studio
eingefügter Connector führt mit genau den konfigurierten Werten aus
(Methode/Endpoint/**Header**/**Body**/**Timeout** einheitlich von OpenAPI-Import bis
Ausführung).

Status: **offen — Teilimplementierung lokal geprüft; NICHT abnahmefähig.** Ein
unabhängiger Review (fail-closed) hat mehrere offene Abnahmelücken identifiziert;
in diesem Stand ist LC02 nicht als abgeschlossen zu werten und wird nicht als
„fertig“ committet.

## Was sicher umgesetzt und lokal verifiziert ist

| Datei | Änderung | Verifiziert |
| --- | --- | --- |
| `ConnectorTemplateProperty.cs` | `BindingVersion` (additiv, legacy-kompatibel) | Build grün |
| `ConnectorBindingVersion.cs` (neu) | Versionen 1/2, `IsSupported` | Unit-Test |
| `OpenApiConnectorTemplateImporter.cs` | Header als `vertex:connector.header.*`, Kern-Bindings V2, `bindingVersion`-Attribut | Unit-Test |
| `HttpConnectorExecutor.cs` | Header/Timeout aus Binding; **Sperrliste** (Authorization/Host/u. a.), **CRLF-Ablehnung**, **Set-Cookie nicht exportiert** | 3 neue Tests |
| `ConnectorValueResolver.cs` (neu) | Literal vs. Variable vs. Konfiguration, Fail-fast | 2 Tests |
| `VertexConnectorServiceTaskHandler.cs` | `AssertSupportedBindingVersion` (unbekannte Versionen hart abgelehnt) | 1 Test |
| `Http/connectors-and-mapping.http` (neu) | Import/List/Create/403 | Routen/DTOs aus Code |

Aktuell verifiziert: Build **0 Errors, 0 Warnings**, Connector-/Importer-Tests
**58/58 grün**, 0 fehlgeschlagen, 0 übersprungen. Die Sicherheits-Härtung und
Binding-Grundlage sind als Teilfortschritt in `e115823c` lokal committet, nicht gepusht.

Option A ist lokal implementiert: Der Handler normalisiert
`vertex:ioMapping.input.vertex:connector.*` zu `vertex:connector.*`, bevor er
Endpoint, Version, Credentials und Transport-Kontext verarbeitet. Ein neuer
Handler-Test prüft Endpoint/Methode/Header/Timeout im Runtime-Kontext. Parser,
Serializer und Studio wurden dabei nicht verändert. Das ist **kein** ausgeführter
Studio-/XML-Roundtrip und **kein** Nachweis eines echten HTTP-Requests.

## Offene Abnahmelücken (Review-Befunde, verbleibend)

1. **Import → Studio → Runtime inkompatibel:** Importer schreibt `vertex:connector.*`,
   Studio schreibt Importe aber als **IO-Inputs** (`vertex:ioMapping.input.vertex:connector.*`),
   Parser reicht diese so durch, Handler erwartet `vertex:connector.*`. Body bleibt der
   rohe Schlüssel `body`; Query-Transport fehlt. Studio-DTO/Moddle kennen keine
   `BindingVersion`. → End-to-End nicht durchgängig.
2. **V2 nur Metadaten:** getrennte Runtime-Config/Input/Output/Credential-Bindings
   existieren noch nicht; `IsSupported` wird jetzt zwar im Handler geprüft, aber die
   V2-Durchgängigkeit (Serializer/Studio) fehlt.
3. **Resolver nicht verdrahtet:** `ConnectorValueResolver` wird im Ausführungspfad
   noch nicht auf Endpoint/Header/Body angewendet; gemeinsame IO-Auswertung vor/nach
   Ausführung fehlt.
4. **Timeout-Vorrang unvollständig:** im echten Pfad kann das kürzere Retry-Timeout
   das Binding-Timeout überstimmen.
5. **Kein echter Laufzeitnachweis:** kein ausgeführter HTTP-Request (Import → Deploy →
   Start → Request → fachlicher Folgeschritt) und kein Studio-Roundtrip — beides laut
   Plan §5 LC02 / LC-AC-01 Pflicht.
6. **`.http`-Katalog unvollständig:** Deploy/Start/Fachwirkung fehlen; Request 03 ist
   nur eine Listenwiederholung; importierte Spec hat keinen Server.

## Nächste Schritte (Reihenfolge)

- Vertragsdurchgängigkeit schließen: Importer ↔ Studio/Moddle ↔ Parser ↔ Serializer ↔
  Handler auf **demselben** Vertrag (Adressierung des Befunds 1).
- Resolver in den Ausführungspfad verdrahten (Befund 3), Timeout-Vorrang real klären (4).
- Echten Import→Deploy→Start→HTTP-Request→Folgeschritt gegen isolierte API nachweisen
  und in `connectors-and-mapping.http` abbilden (5/6).

## Entscheidung

Dieses Teilinkrement (Sicherheits-Härtung Executor/Handler/Importer + versionierte
Binding-Metadaten + Resolver-Grundlage) ist lokal grün, aber **nicht** der
abnahmefähige LC02-Baustein. Es wird nicht als „LC02 fertig“ ausgegeben.
