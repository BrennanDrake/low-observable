# Playtest server: serves the Unity WebGL build from build/webgl with the headers Unity needs.
# Build from the repository root with scripts/pc/build_web_image.sh (adds version labels and tags).
FROM nginx:1.27-alpine

COPY docker/web/nginx.conf /etc/nginx/conf.d/default.conf
COPY build/webgl /usr/share/nginx/html

EXPOSE 80
