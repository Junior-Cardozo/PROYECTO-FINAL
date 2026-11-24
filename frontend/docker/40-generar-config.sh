#!/bin/sh
# Escribe la configuración que usará el navegador.
#   API_URL      : dirección pública del backend (la que abre el navegador)
#   FRONTEND_URL : dirección pública de este frontend (URL de retorno del login)
set -e

API_URL="${API_URL:-http://localhost:8080}"
FRONTEND_URL="${FRONTEND_URL:-http://localhost:3000}"

cat > /usr/share/nginx/html/config.js <<JS
window.APP_CONFIG = {
  API_URL: "${API_URL}",
  FRONTEND_URL: "${FRONTEND_URL}"
};
JS

echo "config.js generado: API_URL=${API_URL} FRONTEND_URL=${FRONTEND_URL}"
