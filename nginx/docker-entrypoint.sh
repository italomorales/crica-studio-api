#!/bin/sh
set -eu

certificate=/etc/letsencrypt/live/api.cricastudio.com/fullchain.pem
if [ -f "$certificate" ]; then
  cp /etc/nginx/templates/default.https.conf /etc/nginx/conf.d/default.conf
else
  cp /etc/nginx/templates/default.http.conf /etc/nginx/conf.d/default.conf
fi