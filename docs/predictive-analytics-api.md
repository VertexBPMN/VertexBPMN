# VertexBPMN: Predictive Analytics API

## Prozessdauer-Vorhersage (Demo)
- Endpoint: `POST /api/analytics/predict-duration`
- Request-Body:
  ```json
  {
    "traceLengths": [5, 7, 6, 8, 7]
  }
  ```
- Response: Erwartete mittlere Dauer und Standardabweichung
  ```json
  {
    "mean": 6.6,
    "stdDev": 1.0198
  }
  ```

## Anwendungsfälle
- Vorhersage von Durchlaufzeiten auf Basis historischer Prozessdaten
- Grundlage für weitere Analytics- und Machine-Learning-Integrationen

## Hinweise
- Die aktuelle Implementierung ist ein Demo-Stub (Mittelwert, Standardabweichung)
- Erweiterbar für echte ML-Modelle und komplexe Analytics

---
*Letztes Update: August 2025*
