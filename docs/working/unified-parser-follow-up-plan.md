GitHub Copilot
Kompakter Umsetzungsplan (Fortsetzung nach Unified Parser)
Phase A – Lückenanalyse absichern
•	Task: Snapshot aktueller Unified Parser Funktionsumfang (Feature-Matrix gegen ursprüngliche Roadmap).
•	Deliverable: Markdown-Tabelle (docs/working/Unified-Gap-Matrix.md).
•	Tests: Kein Code, nur Bestandsaufnahme.
Phase B – Validierung vervollständigen
•	Fehlt: Multi‑Instance Konflikt (cardinality & collection), Cancel-/Terminate-Regeln (nur in Transaction), Boundary Compensation nur an kompensierbarer Activity/Subprocess, mindestens 1 ausgehender Flow bei Gateways, Link Throw/Catch Rollentrennung.
•	Änderungen: Erweiterung UnifiedBpmnParser (Validierungsblock).
•	Tests: Negative Tests je Regel (UnifiedValidationAndDataTests erweitern).
Phase C – Referenzauflösung (Semantic Enrichment)
•	MessageRef, SignalRef, ErrorRef, EscalationRef → Sammlungen (Lists) parsen (messages/signals/errors optional, falls im XML vorhanden).
•	Modell: Neue Records BpmnMessage, BpmnSignal, BpmnError, BpmnEscalation; Events verlinken mit Referenz-Objekten.
•	Tests: Parse + Assert binding; fehlende Referenz → Diagnostic.
Phase D – Vendor Extensions & Attributes
•	Wiederverwenden: Logik aus altem BpmnParser.ParseTasks (ExtensionElements-Auswertung).
•	Unified-Erweiterung: Neues Dictionary<string,string> ExtensionAttributes an Task, Event, Subprocess.
•	Preservation: Serializer ergänzt Namespaces (sammele verwendete Vendor-Namespace URIs).
•	Tests: Camunda formFields, Zeebe ioMapping, Flowable taskListeners Roundtrip.
Phase E – Erweiterte SequenceFlow Semantik
•	ConditionExpression bereits da, hinzufügen: IsDefault (ok), Priority (falls Attribut “vertex:priority” oder “camunda:priority”).
•	Tests: Priorität lesen & Roundtrip.
Phase F – Multi-Instance Feintuning
•	Zusätzliche Felder: Collection, ElementVariable, InputElement, OutputElement getrennt halten.
•	Modell: MultiInstanceLoopCharacteristics erweitern.
•	Parser: Bevorzugte Reihenfolge fest: camunda:elementVariable > zeebe:inputElement > zeebe:outputElement.
•	Tests: Alle Kombis; Konflikt -> Diagnostic.
Phase G – Containment / Hierarchie
•	Optional: Baumstruktur (Children) pro Subprocess (Events/Tasks/Gateways/Flows).
•	Modell: BpmnSubprocess: IReadOnlyList<string> ChildIds oder direkte Objektlisten.
•	Parser: Beim Walk sammeln; bestehende flache Listen behalten (Backwards kompatibel).
•	Tests: Nested Subprocess Assertions.
Phase H – Serializer Fidelity Upgrade
•	Ergänzen: conditionExpression (CDATA wird schon genutzt), multiInstanceLoopCharacteristics mit vendor Attributen (collection/elementVariable), StandardLoop.
•	Reinject: ExtensionElements (camunda:properties etc.) in korrekten Namespaces.
•	Option: Kommentar <!-- serialized-by: vertexbpmn X.Y -->.
•	Tests: Snapshot Roundtrip (Normalisierter XML-Vergleich).
Phase I – Performance & Optionen
•	ParserOptions (Interface): { bool StrictValidation, bool PreserveUnknownExtensions, int CacheSize, bool ParseDiagramInterchange }.
•	LRU Cache: XDocument Cache (ConcurrentDictionary + LinkedList).
•	Streaming Option: Optionaler XmlReader Pfad (nur für Event/Task/Flow-Sammeln).
•	Tests: Benchmark (optional), Funktional unverändert.
Phase J – Diagram Interchange (Optional)
•	BPMNShape, BPMNEdge (Positions + Waypoints).
•	Modell: BpmnShape(Id, BpmnElementId, x,y,w,h), BpmnEdge(Id, SourceRef, TargetRef, Waypoints[]).
•	Parser: Separater DI-Abschnitt.
•	Serializer: Optional (Flag).
•	Tests: Parse minimal DI + Roundtrip.
Phase K – Engine-Integration
•	Mapping UnifiedBpmnModel → Engine internes Modell (Adapter Layer).
•	Schrittweise Ersetzen alter Parser-Nutzung (Feature Flag).
•	Tests: Smoke Execution Tests gegen alte / neue Pipeline (gleiche Trace-Events).
Phase L – Aufräumen & Deprecation
•	Markieren: Alte Phase*-Parser [Obsolete].
•	Entfernen: Nach 2 Iterationen.
•	Dokumentation: docs/Unified-Parser-Upgrade.md.
Teststrategie (ergänzend)
•	Jede Phase: Tests zuerst (TDD).
•	UnifiedValidationAndDataTests aufsplitten in: UnifiedValidationTests, UnifiedDataTests, UnifiedExtensionTests, UnifiedRoundtripTests.
•	Snapshot Tests (vergleiche NormalizedXml(original) vs NormalizedXml(roundtrip)).
Risiken & Mitigation
•	Erweiterte Serializer-Logik: Gefahr von Namespace-Duplikaten → dedizierte Namespace-Sammlung.
•	Performance-Regress: Messung vor/nach Phase I mit >10 großen BPMN-Dateien.
•	Backward Kompatibilität: Flache Listen nicht entfernen bevor Engine angepasst wurde.
Priorisierte Reihenfolge (kurz) B → C → D → F → E → G → H → I → J → K → L
Nächster konkreter Schritt (wenn du willst): “Starte Phase B” → implementiere fehlende Validierungen + Tests.
Sag einfach, mit welcher Phase ich praktisch beginnen soll oder ob ein detaillierter Diff-Vorschlag für Phase B gewünscht ist.

