Neuer, vollständiger Umsetzungsplan (Schwerpunkt: 100% verlustfreier Strict-Roundtrip)
PHASE A – Modell- und Infrastruktur-Erweiterungen
1.	NamespaceContext
•	Struktur: geordnete Liste (List<(string Prefix,string Uri,bool FromOriginal)>)
•	Aufnahme: Reihenfolge aus <definitions> (inkl. Standard-/Default-NS) + zusätzliche später verwendete.
•	Speicherung in BpmnRawMetadata.NamespaceContext.
•	Akzeptanz: Strict-Serialize gibt exakt gleiche Präfix-Deklarationsreihenfolge aus.
2.	ElementMetadata
•	Pro FlowNode + SequenceFlow + globale Artefakte: OrderIndex (laufend beim Parsing).
•	OriginalName (lokaler Name), OriginalAttributes (Dictionary<string,string> unverändert, einschließlich name, attachedToRef usw.).
•	Speicherung in BpmnRawMetadata.FlowNodeAttributes (jetzt erweitern statt überschreiben).
•	Akzeptanz: Reihenfolge Strict-Ausgabe == Input (Test).
3.	ExtensionContainer
•	Ersatz für RawExtensionElements: RawExtensions[elementId] = exakt geklonter XElement (kein Rekursions-Abbau).
•	Abwärtskompatibel: Alte flache Map weiter befüllen bis Migration abgeschlossen.
4.	Neue Raw-Container
•	RawGlobalElements: Liste<XElement> für alle messages, signals, errors, escalations, itemDefinitions, (später ggf. imports, interfaces).
•	RawArtifacts: textAnnotation, association, group.
•	RawLanes: laneSet + lane (inkl. flowNodeRef Reihenfolge).
•	RawDocumentation: pro Prozess und pro FlowNode (List<string RawXml> oder List<XElement>).
•	RawDiRoot: kompletter <BPMNDiagram> bzw. mehrere falls vorhanden.
•	RawMultiInstance[elementId]: original <multiInstanceLoopCharacteristics> / <standardLoopCharacteristics> Node.
•	PrioritySourceNamespace[flowId]: ursprüngliches Namespace der priority-Attributquelle.
5.	Multi-Instance Flags
•	HadCamundaCollection / HadZeebeInputCollection / HadCardinality / HadCamundaElementVar / HadZeebeInOutNodes; abgespeichert in Meta.
6.	Boundary Events
•	attachedToRef, cancelActivity, isInterrupting exakt erfassen; RawEventDefinitions enthält auch solche, die nicht mit *EventDefinition enden (Vendor).
7.	Unbekannte EventDefinitions
•	ALLES unter event, das *EventDefinition enthält ODER vendor-namespaced ist, ungefiltert in RawEventDefinitions.
8.	Name-Attribute
•	Für jeden FlowNode + SequenceFlow + DataObject etc. OriginalAttributes erfassen; Task.Name-Property optional weiter pflegen.
PHASE B – Parser-Anpassungen
1.	Refactor ParseAsync:
•	Erste Schleife: Definitions-/Namespace-Kontext & OrderIndex zählen.
•	Zweite Schleife: FlowNodes/Gateways/Tasks/Subprocesses/Events, inklusive:
•	Name
•	raw documentation
•	attributes dictionary
•	Dritte Schleife: Globale Elemente & Artifacts & Lanes & DI.
2.	SequenceFlow
•	Speichere Original IsDefault (DefaultRefRelation), ConditionRaw, PriorityNamespace.
3.	Fehlerfälle
•	Fallback bei fehlender id (Warnung -> Diagnostics).
4.	Konfig-Schalter
•	ParserOptions.CaptureDiRaw (default true in Strict).
•	ParserOptions.CaptureArtifacts.
5.	Memory
•	Wenn OptimizeStrictMemory=true → Raw-Listen, die exakt 0 sind, auf null setzen.
PHASE C – Serializer Strict
1.	NamespaceContext
•	Exakte Reihenfolge, unveränderte Präfixe. Fehlende neue Prefixe (von ExtensionElements) am Ende deklarieren.
2.	Definitions/Process
•	Alle OriginalAttributes in gleicher Reihenfolge (Attributreihenfolge testbar optional).
•	Documentation-Knoten unverändert einfügen (wenn mehrere).
3.	Element-Reihenfolge
•	Sortierung ausschließlich per OrderIndex; keinerlei typbasierte Gruppierung.
4.	Raw vs Mutiert
•	Wenn Raw verfügbar & nicht RoundtripDirty & keine gezielte Mutation (prüfbar via Dirty Flags) → rohen XElement deep-clonen und nur id/name differenzen injizieren falls geändert.
5.	FlowNodes
•	Name-Attribut nur ausgeben, wenn im Original vorhanden.
6.	Boundary Events
•	attachedToRef, cancelActivity, isInterrupting original schreiben.
7.	EventDefinitions
•	Nur Raw kopieren. Keine Regeneration, außer wenn keine RawEventDefinitions existieren.
8.	SequenceFlow
•	Prioritätsattribut mit ursprünglichem Namespace; kein erzwingender vertex-Namespace.
•	Condition: exakt originaler Text + CDATA Zustand.
9.	ExtensionElements
•	1:1 Raw copy; keine Rekombination aus Flatten-Map.
10.	MultiInstance
•	Originalknoten 1:1 verwenden; keine Normalisierung.
11.	Globale Elemente, Artifacts, Lanes, DI
•	RawGlobalElements, RawArtifacts, RawLanes, RawDiRoot exakt ausgeben (Reihenfolge wie Input).
12.	Incoming/Outgoing
•	Nur falls im Original vorhanden (Option: PreserveGeneratedIfMissing=true für rekonstruierte).
13.	Fallback
•	Wenn beliebiger Raw-Baustein fehlt → logische Rekonstruktion + Diagnostics Hinweis “RT-Fallback:<category>”.
PHASE D – Dirty-Tracking & Mutations
1.	Hilfsmethoden
•	MarkDirtyOnAnyChange(BpmnModel, elementId).
•	Mutations-API (internal): ApplyAttributeChange(elementId, key, value).
2.	Serializer Strict
•	Wenn RoundtripDirty → Hard-Fallback Normalized + Warning.
3.	Partielles Dirty
•	Optional: Nur einzelnes Element Dirty -> Versuch gemischte Ausgabe (Phase 2.5 optional, sonst überspringen).
PHASE E – Tests (Erweitert)
1.	Golden Suite
•	Mindestens 5 MIWG Beispiele (verschiedene Patterns: Gateways, Events, MI, Lanes, Data, Artifacts).
•	Test: Parse Strict → Serialize Strict → String.CompareOrdinal == true.
2.	Strukturtests
•	DeepStructuralCompare(xmlA, xmlB): gleiche Element-/Attribut-Sets, Reihenfolge mitverglichen.
3.	Edge Cases
•	CDATA vs Plain.
•	Misch-Namespace-Prefix Reihenfolge.
•	Mehrere laneSets, verschachtelte Subprocesses.
•	Verschiedene priority-Namespace Quellen (camunda, vertex, none).
•	Proprietäre / unbekannte EventDefinition.
4.	Mutation Tests
•	Änder Name eines Tasks → RoundtripDirty true → Strict fällt zurück.
5.	Performance/Memory
•	Benchmark (micro): Parse Normalized vs Strict (Ziel < +15%).
•	Memory Snapshot: GC.GetTotalAllocatedBytes vs Baseline.
6.	Fallback Tests
•	Entferne RawExtensions manuell → Serializer diagnostiziert Fallback & produziert gültiges BPMN.
PHASE F – Performance & Cleanup
1.	ID Pool Hard Cap (z.B. 10k) → danach keine neuen Intern-Einträge.
2.	XML Cloning Minimierung
•	Raw XElements als read-only; nur beim Strict-Serialize klonen.
3.	Obsolete Attribute Flatten
•	Attribute [Obsolete] an altes Flatten-Konstrukt, aber Beibehaltung bis Migration.
4.	README Update
•	Abschnitt Roundtrip Modes (Normalized vs Strict).
5.	Logging Hook (optional)
•	IBpmnRoundtripLogger für Fallback Events.
PHASE G – Abnahme & Definition of Done Checkliste automatisiert per Test:
•	5+ Dateien Byte-identisch.
•	Alle Edge Tests grün.
•	Fallback-/Dirty-Szenarien korrekt.
•	PriorityNamespace Test grün.
•	Memory Overhead Report (<25%).
•	Benchmark Zeitdifferenz <15%.
MIGRATIONS-ABFOLGE (Pull Requests) PR1: NamespaceContext + RawAttributes + OrderIndex. PR2: RawExtensions vollständiger Baum + RawEventDefinitions Erweiterung. PR3: RawGlobal/Artifacts/Lanes/DI + Serializer Anteile. PR4: Multi-Instance Raw + PriorityNamespace + Boundary Attribute. PR5: Tests Golden + Edge + Fallback. PR6: Performance (Interning Cap, Lazy Clones) + README. PR7: Optional: Partial Dirty Handling / Logging.
AKZEPTANZKRITERIEN (Kurz)
•	Kein Datenverlust mehr: beliebiges Vendor-XML bleibt funktional und textgleich zurückspielbar.
•	Strict Serializer modifiziert nichts außer bewusst geänderten ID/Trace-Fällen.
•	Normalized bleibt unverändert (Backward Compatibility).
.   
Risiken & Mitigation
•	Risiko: Speicheranstieg → OptimizeStrictMemory default true + Null’ing leerer Strukturen.
•	Risiko: Performance Einbruch → Benchmark Gate vor Merge.
•	Risiko: Fehlende Prefix-Reihenfolge → eigener Test NamespaceContextOrderTest.
Bereit für Umsetzung. Sag Bescheid für Start mit PR1 (Phase A/B Teil 1).