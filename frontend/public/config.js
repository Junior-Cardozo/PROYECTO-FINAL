// Configuración por defecto para abrir index.html sin Docker.
// En Docker este archivo se regenera al arrancar (ver docker/40-generar-config.sh).
window.APP_CONFIG = {
  API_URL: "http://localhost:8080",
  FRONTEND_URL: "http://localhost:3000"
};
