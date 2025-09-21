GitHub Copilot

Nachfolgend eine strukturierte, tiefgehende Gap‑Analyse deines aktuellen BpmnParser gegenüber dem BPMN 2.0 Standard (ISO/IEC 19510: Business Process Model and Notation). Ich beziehe mich auf öffentlich bekannte Standardbestandteile, ohne geschützte Formulierungen aus der PDF zu reproduzieren.

-------------------------------------------------------------------------------
1. Abgedeckte Funktionalität (Ist-Zustand)
-------------------------------------------------------------------------------
Dein Parser unterstützt aktuell (teilweise / rudimentär):
- Prozess-Ebene: Einzelnes <process>, Id + name.
- Ereignisse: startEvent, endEvent, boundaryEvent, intermediateCatchEvent, intermediateThrowEvent (Erkennung über LocalName).
- Event-Definition-Heuristik: timer/message/error/signal/compensate/escalation/conditional/link/cancel (nur als generischer String eventDefinitionType, ohne spezifische Objektstruktur).
- Tasks: Alle „*Task“ + callActivity über EndsWith("Task").
  - Spezifisch behandelt: scriptTask (script, scriptFormat, resultVariable), userTask (potentialOwner).
  - Vendor-Erweiterungen (camunda/flowable/activiti/zeebe/jbpm/osmanthus/cib/alfresco + generische properties).
- Subprocess: subProcess + adHocSubProcess; Flags: multiInstanceLoopCharacteristics (isSequential, cardinality), triggeredByEvent, transaction (rudimentär).
- Multi-Instance: loopCardinality + isSequential; keine collection-/elementVariable-Attribute.
- Gateways: Erkennung aller *Gateway; keine Semantik (keine Default Flow, keine Condition-Auswertung).
- SequenceFlows: id/sourceRef/targetRef + conditionExpression (geparst, einfach abgelegt).
- Kollaboration (collaboration): Participants + MessageFlows (rudimentär).
- Artefakte/Anhänge: Lanes (aber NUR lane direkt im Prozess, keine laneSet), DataObjects, Associations, TextAnnotations.
- ExtensionElements: Umfassende Harvesting-Logik für heterogene Namespaces -> Dictionary<String,String>.
- Serialisierung: Re-Konstruktion eines vereinfachten BPMN-XML mit eingeschränkter Wiedergabetreue (z.B. kein laneSet, keine Defaultflows, keine differenzierten Ereignisdefinitionen).
- Validierung: Minimal (ID vorhanden, boundaryEvent-AttachedRef existiert, grobe JSON-Struktur für vendor-spezifische Felder).

-------------------------------------------------------------------------------
2. Wichtige Fehlstellen (funktionale Lücken)
-------------------------------------------------------------------------------
2.1 Ereignisse (Events)
- Nicht differenziert nach Start / Intermediate (Catch vs Throw) / End spezifischer Semantik.
- Fehlende Unterstützung für:
  - Terminate End Event
  - Cancel End Event (Transaktionskontext)
  - Escalation (Throw/Catch) mit EscalationRef
  - Compensation Throw/Catch-Mechanismus (ActivityRef)
  - Link Events (Quell-/Ziel-Paarung)
  - Multiple Event Definitions in einem Event
  - Message-/Signal-/Error-/Escalation-Definition (messageRef, signalRef, errorRef, escalationRef) fehlt strukturell
  - TimerDetails (duration, date, cycle) fehlen komplett (nur boolesker Trigger über Existenz)
  - Conditional Start/Intermediate: conditionExpression wird nicht extrahiert (nur SequenceFlow-Bedingungen)
  - None- vs. Typed-StartEvents (Mehrfachstarts / Event-Subprocess-Szenarien) nicht sauber getrennt
  - Correlation-Properties, Message-Payload nicht vorhanden

2.2 Aktivitäten / Tasks
- Spezifische Tasks fehlen oder werden nur generisch als "serviceTask" o.Ä. erkannt:
  - sendTask, receiveTask, manualTask, businessRuleTask (nur rein durch Name -> kein separates Modell)
  - scriptTask-Parameter: Keine Unterstützung für Camunda/Zeebe Input/Output Mappings auf Task-Ebene (nur Zeebe generisch)
  - callActivity: Kein callElement (Ref) extrahiert
  - AdHoc Subprocess: kein AdHocOrdering, kein completionCondition
  - Transaction Subprocess: keine Cancel-/Compensation-Eventintegration
  - Event Subprocess: Keine interne Struktur/Containment (nur Flag)
  - Loop Characteristics (einfache Schleife, Standard-Loop) fehlt (loopCondition, loopMaximum)

2.3 Gateways
- Exklusiv / Parallel / Inklusiv / EventBased erkannt, aber:
  - Keine Evaluation von Bedingungen (SequenceFlow conditionExpression wird nicht semantisch genutzt)
  - DefaultFlow für Exclusive/Inclusive nicht erkannt (kein default-Attribut am Gateway)
  - Komplexes Gateway: Kein Activation Condition / keine Kombination
  - Event Based Gateway: Keine Verknüpfung zu anschließendem Event (nur String-Liste)

2.4 Sequence Flow
- Fehlende Unterstützung:
  - default-Attribut
  - Priority (für bedingte Flüsse)
  - BPMN DI (Layout) komplett ignoriert (nicht kritisch für Ausführung, aber für Roundtrip)
  - Fehlende Validierung: sourceRef/targetRef existieren? (Du prüfst fälschlicherweise den Flow selbst statt source/target in ValidateBpmnModel: Du validierst flow.Id in flowNodeIds – logisch falsch)

2.5 Data / Artefakte
- Nur dataObject, keine dataObjectReference, kein dataStore, keine dataStoreReference
- Data Associations (dataInputAssociation, dataOutputAssociation) fehlen
- Properties (<process><property>…) ignoriert
- TextAnnotation / Associations: Kein Richtungstyp (AssociationDirection), keine AnnotationRef-Verknüpfung
- Keine Unterstützung für BPMN Data Inputs/Outputs an Aktivitäten/Subprozessen/Prozess

2.6 Swimlanes / Organisation
- laneSet fehlt (Mehrere LaneSets / Hierarchie)
- Keine Pools (Participants geparst, aber kein Mapping ProcessRef ↔ Modell-Knoten)
- Kein Mapping von FlowNodes zu Lanes (nur FlowNodeRef-Liste, keine Rückreferenz beim Node)
- Fehlende Choreography Aufgaben / Conversation / ConversationLink / SubConversation

2.7 Kollaboration / Choreographie
- Choreography Diagram Elemente (ChoreographyTask, CallChoreography, SubChoreography) nicht unterstützt
- Conversation Nodes / Conversation Association fehlen
- MessageFlow: Keine MessageRef / keine Validierung, dass Source/Target Pools sind

2.8 Subprocess & Event Subprocess
- Keine interne Hierarchie (Events/Tasks innerhalb Subprocess isoliert). Aktuell stehen alle Elemente flach in model.Events/Tasks etc.
- Event Subprocess Starttypen (Message, Timer, Signal, Error, Escalation, Conditional) nicht differenziert oder durch Trigger-Strategie interpretierbar
- Boundary Event-Triggerlogik rein statisch (ShouldTriggerBoundaryEvent = false) → unbrauchbar für Ausführung

2.9 Fehler / Ausnahmebehandlung / Transaktion
- Error Events: Nur Erkennung, kein errorRef → kein Mapping auf Fehlercodes
- Compensation: Nur isCompensation Flag (Element existiert), aber kein transformierender Link zu kompensierbarer Aktivität
- Cancel / Terminate End Events: Nicht erkannt

2.10 BPMN DI / Layout / Diagram Interchange
- Komplett fehlend (BPMNShape, BPMNEdge, Waypoints, Bounds)
- Verhindert Roundtrip-Fähigkeit (Layout geht verloren)

2.11 Erweiterte Metadaten / Modell-Vollständigkeit
- Documentation (<documentation>) ignoriert
- <extensionElements> bei Prozess / Subprozess / Events (nur bei Tasks teilweise)
- Fehlerhafte Validierung: Du prüfst flow.Id statt flow.SourceRef / flow.TargetRef gegen Node-Liste → führt zu stillschweigender Inkonsistenz
- Keine ID-Eindeutigkeitsprüfung über alle FlowNodes

2.12 Serialisierung
- Verlust:
  - Multi-Attribute (Default Flow, EventDefinition Details, DataAssociations, Input/Output Parameter)
  - Keine vendor extensions re-namespace-transparenz (Generische Keys werden zu generischen Namespaces flatten, aber Roundtrip kann Collisions erzeugen)
  - Reihenfolge der Knoten nicht garantiert (Out-of-order kann Tools irritieren)
  - Fehlende generische Attributes beim SequenceFlow (conditionExpression wird nicht serialisiert)
  - Keine CDATA Behandlung bei Expressions

2.13 Performance / Architektur
- Document Cache keyed auf gesamten XML-String → hohes Memory-Risiko bei vielen großen Modellen / Keinerlei Eviction
- Kein Streaming / Sax-basierter Ansatz (für große Modelle ineffizient)
- Keine Parser-Konfig (z.B. StrictMode, VendorFlags)
- Keine Separierung in Phasen (Lex/Parse/Validate/Normalize) → schwer test- & erweiterbar

-------------------------------------------------------------------------------
3. Architektur- / Modellierungs-Lücken
-------------------------------------------------------------------------------
Empfohlene Modellobjekte (fehlen oder sind unvollständig):
- EventDefinition Basisklasse + abgeleitete:
  - MessageEventDefinition { MessageRef, CorrelationKeys }
  - TimerEventDefinition { TimeDate, TimeDuration, TimeCycle }
  - SignalEventDefinition { SignalRef }
  - ErrorEventDefinition { ErrorRef }
  - EscalationEventDefinition { EscalationRef }
  - LinkEventDefinition { Name, Target }
  - ConditionalEventDefinition { ConditionExpression }
  - CompensationEventDefinition { ActivityRef }
  - CancelEventDefinition (für Transaktion)
  - TerminateEventDefinition (Flag)
- ActivityLoopCharacteristics:
  - StandardLoopCharacteristics { loopCondition, testBefore, loopMaximum }
  - MultiInstanceLoopCharacteristics { loopCardinality, collection, elementVariable, completionCondition, isSequential }
- CallActivity: { CalledElement, CalledElementVersion? }
- DataAssociation / DataStore / DataObjectReference / Property
- Gateway verfeinert: defaultFlow, ActivationCondition (complex), EventGatewayTargets
- Subprocess: interner Scope Container (Events/Tasks/Flows) statt globaler Listen + Flattening

-------------------------------------------------------------------------------
4. Validierungslücken
-------------------------------------------------------------------------------
Aktuell minimal. Notwendig:
- ID uniqueness global
- StartEvent Regeln (mindestens ein none start if executable / multiple allowed only if Event-basiert)
- Boundary Event muss an Activity/Subprocess hängen (OK) + Typ semantisch stimmig (Cancel nur in Transaction, Compensation nur für kompensierbare)
- SequenceFlow: Source/Target existieren, kein Self-Loop (oder erlauben + Warnung)
- Default Flow Validierung (muss outgoing sein, darf keine Condition haben)
- MultiInstance: loopCardinality XOR collection; completionCondition optional
- Event Subprocess: triggeredByEvent=true + StartEvent mit EventDefinitionType (keine endedless none-start)
- CallActivity: CalledElement referenziert bekannten Prozess (falls Registry)
- MessageFlow: Source/Target müssen Pools (Participants) sein (keine reinen FlowNodes)

-------------------------------------------------------------------------------
5. Serialisierung – Roundtrip Fidelity
-------------------------------------------------------------------------------
Fehlende Wiedergabe:
- EventDefinition Elemente (Timer, Message usw.) jenseits reiner <timerEventDefinition/>
- ConditionExpression in sequenceFlow
- DefaultFlow an Gateways
- multiInstanceLoopCharacteristics innerer Parameter
- extensionElements vollständig (Reihenfolge, Namespaces)
- LaneSet-Struktur
- Kollaboration optional, aber falls vorhanden: Nicht 100% identisch wiederhergestellt

-------------------------------------------------------------------------------
6. Test-Abdeckungserweiterung (Backlog Vorschlag)
-------------------------------------------------------------------------------
Empfehlung: Matrixbasierte Tests, z.B.:

Kategorie | Szenario | Status
--------- | -------- | ------
Events | Alle Start-Varianten (Message/Timer/Signal/Error/Escalation/Conditional/None) | fehlt
Events | Boundary (Interrupting vs Non-Interrupting) + Kombination an Task + Subprocess | rudimentär
Events | Link (Throw/Catch Paar) | fehlt
Events | Terminate End | fehlt
Gateways | Exclusive + default + bedingte Flüsse | fehlt
Gateways | Inclusive + mehrere Bedingungen | fehlt
Gateways | Event Based + konkurrierende CatchEvents | rudimentär (kein echtes Warten)
Activities | MultiInstance (parallel, sequential, completionCondition) | teilw.
Activities | AdHoc Subprocess | fehlt
Activities | Call Activity (Auflösung CalledElement) | fehlt
Data | DataStore + DataAssociations | fehlt
Collab | MessageFlow Referenzen validieren | rudimentär
Serialize | Roundtrip komplexes Modell | unvollständig
Extensions | Camunda Input/Output Parameter, TaskListeners (Komplex) | teilweise

-------------------------------------------------------------------------------
7. Priorisierte Umsetzungs-Roadmap
-------------------------------------------------------------------------------
Stufe 1 (Basis-Korrekturen)
- Fix ValidateBpmnModel (SequenceFlow check).
- EventDefinition Modell-Hierarchie einführen, Parser refaktorisieren (Factory).
- SequenceFlow: default, conditionExpression serialisieren.
- Gateway: defaultFlow erfassen.

Stufe 2 (Ausführungsrelevante Semantik)
- MultiInstanceLoopCharacteristics erweitern (collection, elementVariable).
- LoopCharacteristics (Standard) ergänzen.
- Event Subprocess Struktur (Containment) + referenzierte StartEvents.
- Boundary Events Triggerlogik abstrahieren (Strategy / Dispatcher).

Stufe 3 (Erweiterte Ereignisse)
- Timer (TimeDate, TimeDuration, TimeCycle).
- Message (messageRef + optional correlationKey).
- Signal (signalRef).
- Error/Escalation (Ref + Code).
- Link (Paare validieren).
- Terminate, Cancel End Event, Compensation ActivityRef.

Stufe 4 (Daten + Kollaboration)
- DataObjectReference vs DataObject, DataStore + Reference.
- DataInput/Output + Associations.
- Participants ↔ Prozess mapping, MessageFlow Validierung.
- Properties (<process><property>).

Stufe 5 (Fidelity / Tooling)
- BPMN DI (Shapes/Edges) optional (nur wenn Roundtrip wichtig).
- Complete ExtensionElements Roundtrip (Namespace sauber erhalten).
- Roundtrip Tests mit Snapshot‑Vergleich (XML normalisiert).

Stufe 6 (Performance & Architektur)
- Parser in Phasen: Load → Structural Parse → Semantic Enrichment → Validation.
- Austauschbare EventDefinitionParser (Strategy).
- Caching ersetzen durch LRU oder optional deaktivierbar.
- Streaming-Option (XmlReader) für große Modelle.

-------------------------------------------------------------------------------
8. Konkrete Code-Verbesserungsvorschläge (Kurz)
-------------------------------------------------------------------------------
a) Neue Klassen:
```csharp
abstract record EventDefinition(string Kind);
record TimerEventDefinition(string? TimeDate, string? TimeDuration, string? TimeCycle) : EventDefinition(\"timer\");
record MessageEventDefinition(string MessageRef, string? CorrelationKey) : EventDefinition(\"message\");
record SignalEventDefinition(string SignalRef) : EventDefinition(\"signal\");
record ErrorEventDefinition(string ErrorRef) : EventDefinition(\"error\");
record EscalationEventDefinition(string EscalationRef) : EventDefinition(\"escalation\");
record LinkEventDefinition(string Name, string? Target) : EventDefinition(\"link\");
record ConditionalEventDefinition(string Condition) : EventDefinition(\"conditional\");
record CompensationEventDefinition(string? ActivityRef) : EventDefinition(\"compensation\");
record TerminateEventDefinition() : EventDefinition(\"terminate\");
record CancelEventDefinition() : EventDefinition(\"cancel\");
```

b) BpmnEvent erweitern:
```csharp
public record BpmnEvent(
  string Id,
  string Type,
  string? AttachedToRef = null,
  bool CancelActivity = true,
  IReadOnlyList<EventDefinition>? Definitions = null,
  IDictionary<string, object>? Attributes = null);
```

c) Separate Parser-Schritte:
```csharp
ParseCoreElements();
ParseEventDefinitions();
EnrichSubprocessContainment();
ValidateModel();
```

d) Validation fix (SequenceFlow):
```csharp
foreach (var flow in model.SequenceFlows)
{
    if (!nodeIds.Contains(flow.SourceRef) || !nodeIds.Contains(flow.TargetRef))
        throw new BpmnParseException($"SequenceFlow {flow.Id} has invalid endpoints {flow.SourceRef}->{flow.TargetRef}");
}
```

e) Default Flow:
Beim Gateway:
```csharp
var defaultId = gatewayElement.Attribute(\"default\")?.Value;
```
Und am SequenceFlow Flag `IsDefault = (flow.Id == defaultId)`.

f) ConditionExpressions serialisieren:
Beim Erstellen der sequenceFlow XElement:
```csharp
if (flow.Attributes.TryGetValue(\"conditionExpression\", out var cond))
    xFlow.Add(new XElement(ns + \"conditionExpression\", new XCData(cond.ToString())));
```

-------------------------------------------------------------------------------
9. Risikoanalyse / technische Schulden
-------------------------------------------------------------------------------
- Fehlende Semantik → spätere Engine-Änderungen teurer.
- Fehlende strukturierte EventDefinition → Spaghetti-IF-Ketten in Ausführung.
- Kein Containment → Event Subprocess / Scope-bezogene Validierungen schwer.
- Serialisierung ohne DI / Layout → Externer Modeler-Roundtrip unzuverlässig.
- Zunehmende Vendor-Extensions im gleichen Dictionary erhöht Kollisionsrisiko.

-------------------------------------------------------------------------------
10. Empfohlene kurzfristige Quick Wins
-------------------------------------------------------------------------------
1. Validation-Bug SequenceFlow fixen.
2. ConditionExpression Roundtrip ergänzen.
3. Default Flow Gateway + Serialisierung.
4. EventDefinition-Struktur beginnen (mindestens Timer/Message/Signal/Error).
5. Interne Hilfsklasse für ExtensionElements -> Normalisierung (Namespace + lokaler Name + Key).
6. Unit Tests für alle Start-/End-/Intermediate-Event-Typen anlegen (Stub-XML pro Typ).
7. MultiInstance-Erweiterung (collection + elementVariable) parsen (auch wenn Engine es noch nicht nutzt).

-------------------------------------------------------------------------------
11. Langfristige Richtung
-------------------------------------------------------------------------------
- Trennung von Syntaxmodell (Raw Parse Tree) und Semantikmodell (Enriched Graph).
- Graph Transformation (Konsolidierung im Engine-spezifischen Format).
- Erweiterbar für CMMN/DMN Verknüpfungen (CallActivity DMN Decision Task alias BusinessRuleTask).
- Verwendung einer Normalisierungs-Pipeline (Visitor Pattern).
- Optionale Validierungsprofile (Strict vs Lenient).
- Partial Parse (nur bestimmte Elemente neu interpretieren bei XML-Änderung).

-------------------------------------------------------------------------------
12. Zusammenfassung
-------------------------------------------------------------------------------
Der aktuelle Parser ist ein guter MVP / „Annotation Harvester“ für Tasks + Vendor Extensions, aber weit entfernt von standardkonformer BPMN-Abdeckung. Hauptdefizite liegen in (a) strukturierten EventDefinitionen, (b) fehlendem Scoping/Containment, (c) Daten-/Gateway-Semantik, (d) Roundtrip-Fidelity und (e) Validierungstiefe. Eine modulare Parser-Architektur (Phasenmodell + Strukturierte Domänenobjekte) ist der entscheidende nächste Reife-Sprung.

Wenn du möchtest, erstelle ich dir im nächsten Schritt einen konkreten Refactoring-Start (Ordner Parser/Phases + neue Records). Gib einfach „Refactoring starten“ an.

Möchtest du zuerst nur EventDefinition-Hierarchie implementieren oder gleich die gesamte Phase‑Architektur? Sag kurz Bescheid.