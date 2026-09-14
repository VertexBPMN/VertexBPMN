# A02 – lokaler Scheduling-Preview

Stand: 2026-09-13. Dieser Zwischenstand qualifiziert die persistente Erzeugung von Jobs/Waits, ausgewählte Boundary-/Scope-Abbruchpfade und reale PostgreSQL-Transaktionen. Worker-Protokoll, Completion und vollständige Abbruchsemantik sind noch nicht implementiert. Kein Produktionsprofil aktivieren. Aktuelle Nachweise und verbleibende Abnahmebedingungen stehen im [A02-Prüfbericht](2026-09-13_External-Agent_A02_Pruefbericht.md).

## Lokaler Katalog

Die Registrierung erfordert explizit `OperationalMode=Development` oder `Test` (alternativ `ASPNETCORE_ENVIRONMENT`). Unbekannte oder fehlende Umgebung wird beim aktivierten Preview abgewiesen. Der Preview ist standardmäßig aus. Beispiel für einen rein synthetischen lokalen Test:

```json
{
  "OperationalMode": "Development",
  "ExternalTasks": {
    "EnableSchedulingPreview": true,
    "Contracts": [
      {
        "TenantId": "test-tenant",
        "Topic": "test.work",
        "Enabled": true,
        "Version": "v1",
        "MaxAttempts": 1,
        "MaxDeadlineSeconds": 300,
        "Inputs": [
          { "Name": "text", "Type": "string", "Required": true,
            "MaxLength": 4096, "AllowExternalTransfer": true }
        ],
        "Outputs": []
      }
    ]
  }
}
```

`ConfiguredExternalTaskContractResolver` liest den aktuellen lokalen Konfigurationsstand je Aufruf. Exakt eine aktivierte Kombination aus Tenant, Topic und optionaler Agentprofilreferenz muss passen. Duplikate, Widerruf, unbekannte Kombinationen und überschrittene Attempt-/Deadlinegrenzen werden abgewiesen. Änderungen an Inhalt oder Limits erfordern operatorseitig eine neue Version. Bereits angelegte Jobs behalten ihren Snapshot; aktive Lease-/Completion-Prüfung auf spätere Policyänderungen folgt in A03/A04.

Felder: `string`, `integer` (Int64), `boolean`; höchstens 32 Felder, eindeutige Namen, Stringlimit 1–65536 Zeichen und zusätzlich 128 KiB Gesamtinput. Keine Arrays/Objekte, keine unbekannten Inputs. `AllowExternalTransfer=true` verlangt eine ausdrückliche operatorseitige Freigabe. Dies ist keine automatische Erkennung von Geheimnissen in einem String: der Betreiber muss das Mapping und die Datenklassifikation prüfen. Credentialreferenz-Objekte werden durch das eingeschränkte Schema abgewiesen.

Der Snapshot verwendet den ausdrücklich eingeschränkten Dialekt `vertex.scalar-contract.v1`. Er enthält Input-/Outputbeschreibung ohne URLs oder Credentials. Das ist kein allgemeiner JSON-Schema-Validator und unterstützt noch kein verschachteltes Vertragsanalyseergebnis. Der zukünftige Completionpfad muss den gespeicherten Dialekt kennen und unbekannte Dialekte abweisen.

## Runtime-Grenzen

Ein Service Task mit gültiger `vertex:externalTask`-Definition erzeugt atomar Job, Wait und Historie. Der normale Service-Task-Handler wird nicht aufgerufen. Eingaben kommen ausschließlich aus expliziten einfachen Variablenreferenzen; lokaler MI-Scope hat Vorrang. Jeder Durchlauf erhält eine eigene persistente ActivityExecutionId. Wiederholter Start mit derselben Idempotency-ID erzeugt keine weiteren Jobs.

Der Preview wird nach dem Scheduling warten: Es gibt noch keinen Worker, der diese Jobs abschließen kann. Ein Root-Terminate-End-Event cancelt die offenen Jobs, Waits und zugehörigen MI-Ausführungen. Timer-/Message-/Signal-Boundaries unterstützen die geprüften interrupting und non-interrupting Scheduling-Pfade. Eingebettete Subprozesse und Message-Event-Subprozesse erhalten eigene Scope-Identitäten. Parallele MI erzeugt getrennte Jobs; sequenzielle MI erzeugt zunächst nur den ersten Job. Standard-Loops berücksichtigen beim Eintritt testBefore und die Bedingung; Fortsetzung und weitere Iterationen fehlen noch. Öffentliche Delete-/Suspend-Operationen sind noch nicht als External-Task-Lebenszyklus abgenommen. Tests verwenden isolierte SQLite- und PostgreSQL-Datenbanken mit synthetischen Eingaben.

## Offene Integration

Lokaler Abbruchnachweis: acht Scheduling-/Runtime-Testfälle einschließlich geleaster einfacher/paralleler Jobs und angenommener Completion mit noch ausstehender Fortsetzung. Beim Terminate werden offene Attempts geschlossen und Pending-Continuations annulliert; bereits gespeicherte Ergebnisse/Receipts bleiben erhalten, ohne als Prozessvariablen übernommen zu werden. Lease/Completion sind in diesen Tests vorbereitete Datenbankzustände, keine Nachweise einer funktionierenden Worker-API oder konkurrierender Requests.

Die registrierte Application-Repository-Implementierung prüft beim Deployment Tenant/Topic/Profil, Limits und deklarierte Inputs vor dem ersten Repositoryzugriff. Vertragsfehler werden als BPMN-Validierungsdiagnostik zurückgegeben. Deaktivierte External Tasks in aufgerufenen Modellen werden vor vorangehenden Service-Handlern erkannt. Definitions-, Mapping-, Input- und Schemasnapshots werden mit SHA-256-Prüfsummen gespeichert. Vollständige Ausdrucks-/Outputmappings, Boundary/Completion-Rennen, Scope-/MI-Abschluss und belastbare Versionierungs-/Widerrufspolitik für laufende Jobs bleiben offen. Dieser Katalog ist ein lokaler Implementierungsbaustein und keine Freigabe des vollständigen Agent-Anwendungsfalls.

## Lokale Wiederholung

`VERTEXBPMN_TEST_POSTGRES_ADMIN` muss auf eine ausschließlich für lokale Tests bestimmte PostgreSQL-Instanz mit Datenbank-Erstellungsrechten zeigen. Anschließend `./scripts/test-external-tasks-local.ps1` ausführen; `-NoBuild` nur mit aktuellen Release-Artefakten verwenden. Der Test erstellt und entfernt eigene GUID-Datenbanken. Ohne PostgreSQL-Konfiguration bricht das Skript ab, statt die Datenbankfälle still zu überspringen. Keine CI-Erweiterung.
