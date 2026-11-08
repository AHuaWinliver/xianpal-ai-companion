#!/bin/sh
# SPDX-License-Identifier: Apache-2.0
set -eu
package_directory=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
exec /bin/sh "$package_directory/launch.sh" --install "$@"
