# Segunda aplicación (Portal de Soporte) para demostrar SSO:
# comparte la sesión del backend con la aplicación principal.
FROM nginx:1.27-alpine

COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY public/ /usr/share/nginx/html/

COPY docker/40-generar-config.sh /docker-entrypoint.d/40-generar-config.sh
RUN chmod +x /docker-entrypoint.d/40-generar-config.sh

EXPOSE 80
