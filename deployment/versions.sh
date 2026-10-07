#!/bin/sh
# Keeps the version-picker registry: a ConfigMap in the prod namespace with one JSON key per live preview.
# Prod mounts it as files, so previews appear/disappear in the picker without redeploying prod.
#   versions.sh ensure
#   versions.sh register <namespace> <branch> <sha>
#   versions.sh unregister <namespace>
set -eu

PROD_NS=tankgame
CM=tankgame-versions

ensure() {
  kubectl get namespace "$PROD_NS" >/dev/null 2>&1 || kubectl create namespace "$PROD_NS" >/dev/null 2>&1 || true
  kubectl -n "$PROD_NS" get configmap "$CM" >/dev/null 2>&1 \
    || kubectl -n "$PROD_NS" create configmap "$CM" >/dev/null 2>&1 \
    || kubectl -n "$PROD_NS" get configmap "$CM" >/dev/null
}

json_escape() {
  printf '%s' "$1" | sed -e 's/\\/\\\\/g' -e 's/"/\\"/g'
}

case "${1:-}" in
  ensure)
    ensure
    ;;
  register)
    ns=$2 branch=$3 sha=$4
    host=""
    tries=0
    while [ "$tries" -lt 30 ]; do
      host=$(kubectl -n "$ns" get ingress tankgame -o jsonpath='{.status.loadBalancer.ingress[0].hostname}' 2>/dev/null || true)
      [ -n "$host" ] && break
      tries=$((tries + 1))
      sleep 5
    done
    if [ -z "$host" ]; then
      echo "::warning::No ingress hostname for $ns yet, not added to the version picker"
      exit 0
    fi
    ensure
    entry=$(printf '{"id":"%s","name":"%s","url":"https://%s","sha":"%s","updatedAt":"%s"}' \
      "$(json_escape "$ns")" "$(json_escape "$branch")" "$(json_escape "$host")" "$(json_escape "$sha")" "$(date -u +%Y-%m-%dT%H:%M:%SZ)")
    patch=$(printf '{"data":{"%s.json":"%s"}}' "$ns" "$(json_escape "$entry")")
    kubectl -n "$PROD_NS" patch configmap "$CM" --type merge -p "$patch"
    ;;
  unregister)
    ns=$2
    ensure
    kubectl -n "$PROD_NS" patch configmap "$CM" --type merge -p "$(printf '{"data":{"%s.json":null}}' "$ns")"
    ;;
  *)
    echo "usage: $0 ensure | register <namespace> <branch> <sha> | unregister <namespace>" >&2
    exit 2
    ;;
esac
