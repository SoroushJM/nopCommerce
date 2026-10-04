#!/bin/sh
set -eu
root=/app/published
# Native libraries shared with the app do not need another copy per plugin.
for shared in "$root"/*.so; do
    [ -f "$shared" ] || continue
    name=${shared##*/}
    for duplicate in "$root"/Plugins/*/"$name"; do
        [ -f "$duplicate" ] || continue
        if cmp -s "$shared" "$duplicate"; then
            rm "$duplicate"
        fi
    done
done
# Keep reference assemblies for Razor compilation; only remove identical copies.
for shared in "$root"/refs/*.dll; do
    [ -f "$shared" ] || continue
    name=${shared##*/}
    for duplicate in "$root"/Plugins/*/refs/"$name"; do
        [ -f "$duplicate" ] || continue
        if cmp -s "$shared" "$duplicate"; then
            rm "$duplicate"
        fi
    done
done
