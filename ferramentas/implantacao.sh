#!/bin/sh
# Gera public/implantacao.js no deploy do cPanel, com o commit e a hora da implantacao.
# A pagina le esse arquivo e mostra a linha no rodape. O arquivo fica fora do git.
set -e
cd "$(dirname "$0")/.."
commit=$(git rev-parse --short HEAD)
data=$(date -u '+%Y-%m-%d %H:%M:%S +00:00')
printf 'window.MAPNET_IMPLANTACAO = { commit: "%s", data: "%s" };\n' "$commit" "$data" > public/implantacao.js
echo "implantacao.js: $commit, $data"
