#!/bin/sh
# Escribe la configuración que usará el navegador (segunda aplicación).
set -e

API_URL="${API_URL:-http://localhost:8080}"
FRONTEND_URL="${PORTAL2_URL:-http://localhost:3001}"
APP1_URL="${APP1_URL:-http://localhost:3000}"

cat > /usr/share/nginx/html/config.js <<JS
window.APP_CONFIG = {
  API_URL: "${API_URL}",
  FRONTEND_URL: "${FRONTEND_URL}",
  APP1_URL: "${APP1_URL}"
};
JS

echo "config.js generado: API_URL=${API_URL} FRONTEND_URL=${FRONTEND_URL} APP1_URL=${APP1_URL}"
