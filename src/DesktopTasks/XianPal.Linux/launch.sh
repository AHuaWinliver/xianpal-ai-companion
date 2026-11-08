#!/bin/sh
# SPDX-License-Identifier: Apache-2.0
set -eu
app_directory=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
if [ -d "$app_directory/runtime" ]; then
    LD_LIBRARY_PATH="$app_directory/runtime${LD_LIBRARY_PATH:+:$LD_LIBRARY_PATH}"
    export LD_LIBRARY_PATH
fi
exec "$app_directory/xianpal" "$@"
