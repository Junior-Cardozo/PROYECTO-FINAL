# Frontend estático servido por Nginx.
FROM nginx:1.27-alpine

COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY public/ /usr/share/nginx/html/

# Genera config.js al arrancar a partir de las variables de entorno
COPY docker/40-generar-config.sh /docker-entrypoint.d/40-generar-config.sh
RUN chmod +x /docker-entrypoint.d/40-generar-config.sh

EXPOSE 80
