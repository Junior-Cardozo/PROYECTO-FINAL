# Monitoreo con Prometheus

## ¿Qué es cada pieza? (en simple)

| Pieza | Qué hace | Analogía |
|---|---|---|
| **Node Exporter** | Lee el sistema operativo Linux (CPU, RAM, disco, uptime) y lo publica en `http://node-exporter:9100/metrics` como texto. | El termómetro |
| **Prometheus** | Cada 15 s visita a Node Exporter, guarda los números con su fecha y permite consultarlos con un lenguaje llamado PromQL. | El cuaderno donde se anotan las lecturas |
| **Backend (ASP.NET)** | Pregunta a Prometheus, valida que el usuario tenga rol y entrega JSON limpio. | El recepcionista |
| **Frontend** | Dibuja medidores y gráficas con ese JSON. | La pantalla |

Flujo: `Linux → Node Exporter → Prometheus → API (/api/metricas/*) → navegador`.
El navegador nunca habla directo con Prometheus: así los permisos se validan en el backend.

## Cómo verlo

```bash
docker compose -f despliegue.yml up -d --build
```
1. Frontend: http://localhost:3000 → tarjeta **Monitoreo del servidor** (se refresca cada 10 s).
   Las gráficas necesitan 1–2 minutos para tener puntos suficientes.
2. Interfaz propia de Prometheus: http://localhost:9090
   - **Status → Targets**: `node-exporter` y `prometheus` deben estar en **UP**.
   - **Graph**: pega una consulta y presiona *Execute*.

## Consultas PromQL usadas

| Métrica | PromQL |
|---|---|
| CPU % | `100 - (avg(rate(node_cpu_seconds_total{mode="idle"}[1m])) * 100)` |
| RAM % | `(1 - sum(node_memory_MemAvailable_bytes) / sum(node_memory_MemTotal_bytes)) * 100` |
| Disco % | `1 - max(node_filesystem_avail_bytes{mountpoint="/"}) / max(node_filesystem_size_bytes{mountpoint="/"})` |
| Uptime (s) | `time() - max(node_boot_time_seconds)` |
| Disponibilidad | `up{job="node-exporter"}` (1 = en línea, 0 = caído) |

## Prueba de "disponibilidad"

```bash
docker stop monitor-node-exporter     # en ~30 s el frontend marca Caído / sin datos
docker start monitor-node-exporter    # vuelve a En línea
```

## Archivos

- `prometheus/prometheus.yml`: a quién visita Prometheus y cada cuánto.
- `despliegue.yml`: servicios `prometheus` y `node-exporter`, volumen `prometheus_data` (retención 7 días).
- `API/Controllers/MetricasController.cs`: consulta a Prometheus y aplica permisos.

## Opcional (no incluido)

cAdvisor para métricas por contenedor: agregar un servicio `gcr.io/cadvisor/cadvisor` y un `job_name` nuevo en `prometheus.yml`.
