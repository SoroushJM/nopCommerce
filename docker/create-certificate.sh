#!/bin/sh
set -eu
if [ -s /https/nopcommerce.pfx ]; then
    exit 0
fi
umask 077
openssl req -x509 -newkey rsa:2048 -nodes -days 365 \
    -keyout /tmp/server.key -out /tmp/server.crt \
    -subj '/CN=localhost' \
    -addext 'subjectAltName=DNS:localhost,IP:127.0.0.1'
openssl pkcs12 -export -out /https/nopcommerce.pfx \
    -inkey /tmp/server.key -in /tmp/server.crt \
    -passout env:HTTPS_CERT_PASSWORD
