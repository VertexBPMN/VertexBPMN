# Git GS07: SSH-Authentifizierungsdateien und Studio-Reads

Stand: 13. Oktober 2026, Entwicklungsbranch `codex/git-generic-servers`. Fortsetzung von [SSH-Read-Worker und Ergebnissen](2026-10-13_Git-GS07_SSH-Read-Worker-und-Ergebnisse.md).

## Implementierter Umfang

Private SSH-Authentifizierungsdateien erhalten vor dem Schreiben des privaten Schlüssels einen geschützten Besitznachweis. Er enthält nur Version, Erstellungszeit und Leaseablauf und ist mit Data Protection an den vollständig aufgelösten Verzeichnispfad gebunden. Der Writer fordert persistentes Flushen des Nachweises an, bevor Schlüsselbytes geschrieben werden. Der normale Lebenszyklus entfernt auch den Nachweis. Ein fehlgeschlagener Verzeichnisaufbau berechtigt das Objekt nicht zur Bereinigung eines fremden Verzeichnisses.

`GitSshAuthenticationFileRecovery` löscht nach Leaseablauf plus einer Minute Grace nur Verzeichnisse mit gültigem eigenem Nachweis. Voraussetzungen: direktes GUID-Verzeichnis `vertex-ssh-{guid}` im Temp-Verzeichnis des Dienstkontos, keine Links in den Vorfahren, private Rechte und ausschließlich die vier bekannten Authentifizierungsdateien. Marker werden begrenzt auf 4 KiB gelesen. Unmarkierte alte Verzeichnisse, falsche Schlüssel, unbekannte Formate, wiederverwendete Marker, zusätzliche Dateien und Links autorisieren keine automatische Löschung. Es gibt keine rekursive Löschung.

Der Cleanup-Dienst startet unabhängig vom Enabled-Schalter außerhalb des Testmodus, läuft nach Startup und minütlich und benötigt keine Auftragsdatenbank, Git- oder SSH-Tools. Pro Durchlauf gelten zehn Sekunden Budget und höchstens 32 erfolgreiche Bereinigungen. Die Bereinigung liest keine privaten Schlüssel und protokolliert keine Pfade, Marker oder Providerexception. Persistente Data-Protection-Schlüssel des Dienstkontos bleiben Voraussetzung für eine Wiederaufnahme nach Prozessneustart. Ältere unmarkierte Reste müssen operativ geprüft werden; sie werden nicht allein aufgrund ihres Namens oder Alters entfernt.

Die synchronen profilgebundenen Studio-/Providerreads routen nun SSH auf denselben geschützten Version-4-Auftrag mit Readrunner und dauerhaften Ergebnissen. Die Repository-Verfügbarkeit prüft administrative SSH-Regeln und einen vorhandenen konfigurierten Client und meldet zunächst ausschließlich `Read`. Commit, Push und Hostingaktionen werden dabei nicht angekündigt. Die Git-Version wird im nativen Ablauf geprüft; die SSH-Authentifizierung erfolgt über den administrativ festgelegten nativen Client; ein vorhandener Dateipfad ist kein Server-Abnahmenachweis.

## Grenzen

Reale Windows-/Linux-SSH-Anmeldung, Browser-/HTTP-/Serverläufe und echte Prozess-/Host-/Powerlossabnahmen bleiben unperformiert. Die Unitprüfungen verwenden synthetische Schlüssel und kontrollierte Zeitpunkte beziehungsweise eigene ephemeral Data-Protection-Schlüssel. Sie prüfen keine sichere physische Datenträgerlöschung. Schlüsselpassphrase sowie SSH-Commit/Push mit Ref-CAS und Reconciliation bleiben offen. Allgemeine Anbieter- oder Produktionsfreigabe folgt aus diesem Paket nicht.

## Prüfungen

Vollständiger Release-Build mit unveränderten Editorassets: null Warnungen und Fehler (2:28,05). 345 gezielte Tests erfolgreich, keine Fehler/Skips (65,933 Sekunden). Davon 14 neue Fälle für Ablauf/Grace, Neustart des Recoverydienstes mit gleichen Schlüsseln, fehlende/manipulierte/replayte/zu große Nachweise, zusätzliche Dateien, Aufruferabbruch, Cleanup bei deaktiviertem Plug-in sowie nur-Lesen-Verfügbarkeit und Studio-Readrouting unter gemeinsamer Jobquote. Die SshExecutablePath-Verfügbarkeitstests nutzen einen vorhandenen Testprozesspfad nur für die Existenzprüfung und starten keinen SSH-Client.

Infrastruktur- und Testdateien wurden check-only whitespacegeprüft. Die gemeldeten Leerraumstellen wurden nur in eigenen Dateien geändert. D1–D7 und manuelle Prüfung aller acht Clean-Code-Regeln: 13 C#-Dateien/geänderte Mitglieder plus die kohäsive DI-Registrierung, keine bestätigten Befunde. Gemeinsame Lifecycle-/Leaseports bleiben erhalten. Optionale Recovery-/SSH-Leseserviceparameter erhalten interne Legacy-/Testkonstruktoren; der native SSH-Dateiwriter verweigert einen fehlenden Recoverydienst ausdrücklich. Der synchrone flushToDisk-Aufruf hat keinen entsprechenden Async-Overload und wird vor Schlüsselanlage vollständig abgewartet über Task.Run ausgeführt; kein Analyzer wurde dafür abgeschaltet. Die verzögerte Bereinigung verwendet eine feste readonly Dateinamenliste, keine freie oder rekursive Pfadoperation.

| Regel | Ergebnis im eigenen Umfang |
| --- | --- |
| Async/Cancellation | Token durch Marker- und Sweep-I/O; Flush vor Keyanlage abgewartet; Callerabbruch bleibt erhalten |
| Collections | Unveränderliche geordnete Liste der vier eigenen Dateien; Sweep lazy und zeitbegrenzt |
| Boolean-Modi | Besitz- und Availabilityzustand; keine neuen unklaren bool-Parameter |
| Namen | Authentifizierungsdateien, Pfadpolicy, Recovery und Hosted Cleanup nach Verantwortung getrennt |
| Regions/Struktur | Keine Regions; lange bestehende DI-/Availabilitymethoden durch kohäsive Registrierungs-/SSH-Availabilitymethoden geteilt |
| Interfaces | Framework-/bestehende Ports erhalten; keine neuen Interfaces |
| Vererbung | Nur Framework-BackgroundService/IAsyncDisposable; keine neue eigene Hierarchie |
| Lesbarkeit | Besitzbeweis vor Secrets, feste Pfadgrenzen und Fehlerbehandlung manuell geprüft; synthetische Daten als Testausnahmen |

Native Infrastrukturprüfung: Windows-sshd nicht vorhanden; Debian-WSL besitzt Git 2.47.3 und ssh-keygen, aber auch keinen /usr/sbin/sshd. Keine Tools installiert und kein Server provisioniert. Keine private Benutzerdatei oder echte Credential verwendet. Windows-Gitclient und OpenSSH-Client ersetzen keine Serverabnahme.

Logs unter `artifacts/git-generic-servers/ssh-orphan-*.log` werden nicht versioniert. CI-safe mit den unveränderten Ausschlüssen: 1.988 Erfolge, acht bestehende externe Skips, keine Fehler, insgesamt 1.996 Fälle (2:34,319). Skips bleiben unperformierte externe Abnahmen; der grüne Lauf beweist keine Behebung der historischen C.8.0-FEEL-Instabilität.
