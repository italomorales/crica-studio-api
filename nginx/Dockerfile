FROM nginx:1.27-alpine

COPY default.http.conf /etc/nginx/templates/default.http.conf
COPY default.https.conf /etc/nginx/templates/default.https.conf
COPY docker-entrypoint.sh /docker-entrypoint.d/40-select-site-config.sh
RUN chmod +x /docker-entrypoint.d/40-select-site-config.sh