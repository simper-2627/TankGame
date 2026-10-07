#!/bin/sh
slug=$(echo "$1" | tr '[:upper:]' '[:lower:]' | sed -E 's/[^a-z0-9-]+/-/g' | cut -c1-50 | sed -E 's/^-+|-+$//g')
echo "tankgame-$slug"
