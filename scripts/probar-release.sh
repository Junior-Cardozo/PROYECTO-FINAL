#!/usr/bin/env bash
# Prueba las 4 instancias del release.
#
# 1) Sin sesión: /health debe dar 200 y los reportes 401.
# 2) Con sesión: copia el valor de la cookie "MonitorServidoresSesion" desde el
#    navegador (F12 > Application > Cookies) y ejecuta:
#       COOKIE="valor_de_la_cookie" ./scripts/probar-release.sh
#    Los reportes deben dar 200 (o 403 si ese usuario no tiene el rol) en los 4 puertos.
set -u
PUERTOS=(5001 5002 5003 5004)
REPORTES=(servidores incidentes alertas)

for p in "${PUERTOS[@]}"; do
  echo "=== Puerto $p ==="
  echo -n "  /health            -> "; curl -s -o /dev/null -w "%{http_code}" "http://localhost:$p/health"
  echo "  $(curl -s "http://localhost:$p/health")"

  for r in "${REPORTES[@]}"; do
    echo -n "  /api/reportes/$r (sin sesión) -> "
    curl -s -o /dev/null -w "%{http_code}\n" "http://localhost:$p/api/reportes/$r"

    if [ -n "${COOKIE:-}" ]; then
      echo -n "  /api/reportes/$r (con sesión) -> "
      curl -s -o /dev/null -w "%{http_code}\n" -H "Cookie: MonitorServidoresSesion=$COOKIE" "http://localhost:$p/api/reportes/$r"
    fi
  done
done
