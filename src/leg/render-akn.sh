#!/usr/bin/env bash
#
# Render a leg AKN file to HTML through src/leg/akn2html.xsl.
#
# The stylesheet is XSLT 2.0 — deliberately, because it mirrors the XSLT 2
# pipeline legislation.gov.uk renders with — so the .NET BCL cannot run it and
# it needs Saxon. This script depends on a JRE and two jars, and nothing else.
# In particular it does NOT depend on Oxygen: Oxygen's layout differs per
# platform and per version (on macOS the bundled JRE is under
# .install4j/jre.bundle/, not jre/bin/), which is why HtmlBuilder.IsAvailable()
# reports false on machines that have both Java and Saxon.
#
# The stylesheet inlines associated-docs.css via unparsed-text(), resolved
# against its own location, so the HTML is self-contained and viewable as-is.
#
# Exercised on macOS 15 (arm64, Amazon Corretto 25) only. Nothing here is
# macOS-specific by intent — it is Bash plus curl and a checksum tool — but it
# has not been run on Linux or in CI, so treat that as untested rather than
# supported.
#
# Usage:
#   src/leg/render-akn.sh --fetch                    download the jars (once)
#   src/leg/render-akn.sh FILE.akn [-o OUT.html]     render one file
#   src/leg/render-akn.sh FILE.akn --compare REV     also render FILE as of git
#                                                  REV, and diff the two
# Options:
#   --image-base VAL   value for the stylesheet's image-base param (default "")
#   -d DIR             output directory (default: a temp dir)
#   --force            allow writing over a git-tracked file
#
# Output defaults to a temp directory, never next to the input: the committed
# HTML goldens sit beside the .akn fixtures as test/leg/*/NAME.html, so a
# default of NAME.akn -> NAME.html would silently overwrite a snapshot.
# Writing over any tracked file requires --force.
#
# Environment:
#   SAXON_CP         override the classpath entirely
#   JAVA_HOME        preferred JRE; otherwise `java` from PATH
#   LEG_RENDER_LIB   where the jars live (default:
#                    ${XDG_CACHE_HOME:-~/.cache}/tna-leg-render)

set -euo pipefail

SAXON_VERSION=12.10
XMLRESOLVER_VERSION=5.3.3          # required (non-optional) by Saxon-HE 12.10;
                                   # without it Saxon throws NoClassDefFoundError
                                   # org/xmlresolver/Resolver at startup
MAVEN=https://repo1.maven.org/maven2

# Pinning a version gives reproducibility, not integrity: it says nothing about
# what the host actually serves. Maven publishes .sha1 sidecars, but a checksum
# fetched from the host it is checking is no defence against that host being
# compromised — so the expected digests are recorded here instead. What they
# protect is every later fetch; they cannot vouch for the first one, and
# re-downloading to the same value shows only that the value is stable.
SAXON_SHA256=b571af282f25d7301059f788b9a149aab8b5cdc14ef3d212dc5425d3dcbb9a97
XMLRESOLVER_SHA256=1fe4d5b92f708dcdb82dbce12919e0171e6b5ca62c6dca6220483625098feb5f

die() { printf 'render-akn: %s\n' "$*" >&2; exit 1; }

REPO=$(git rev-parse --show-toplevel 2>/dev/null) || die "not inside a git repository"

# The jars are a tool cache, not source, so they live outside the working tree:
# nothing to gitignore, nothing to clean up, and one download shared across
# checkouts and branches. Override with LEG_RENDER_LIB if you want them local.
LIB="${LEG_RENDER_LIB:-${XDG_CACHE_HOME:-$HOME/.cache}/tna-leg-render}"
XSL="$REPO/src/leg/akn2html.xsl"

resolve_java() {
    if [ -n "${JAVA_HOME:-}" ] && [ -x "$JAVA_HOME/bin/java" ]; then
        printf '%s' "$JAVA_HOME/bin/java"
    elif command -v java >/dev/null 2>&1; then
        command -v java
    else
        die "no JRE found. Set JAVA_HOME or put java on PATH."
    fi
}

resolve_classpath() {
    if [ -n "${SAXON_CP:-}" ]; then
        printf '%s' "$SAXON_CP"
        return
    fi
    local saxon="$LIB/Saxon-HE-$SAXON_VERSION.jar"
    local resolver="$LIB/xmlresolver-$XMLRESOLVER_VERSION.jar"
    [ -f "$saxon" ] && [ -f "$resolver" ] \
        || die "Saxon jars not present. Run: src/leg/render-akn.sh --fetch"
    printf '%s:%s' "$saxon" "$resolver"
}

sha256_of() {
    if command -v shasum >/dev/null 2>&1; then
        shasum -a 256 "$1" | cut -d' ' -f1
    elif command -v sha256sum >/dev/null 2>&1; then
        sha256sum "$1" | cut -d' ' -f1
    else
        die "need shasum or sha256sum to verify downloads"
    fi
}

# fetch_jar <url> <destination> <expected sha256>
# Downloads beside the destination and only moves it into place once the digest
# matches, so a failed check cannot leave a jar behind for the next run to load.
fetch_jar() {
    local url=$1 dest=$2 want=$3 tmp got
    if [ -f "$dest" ]; then
        [ "$(sha256_of "$dest")" = "$want" ] && return 0
        printf 'render-akn: %s does not match its pinned digest; re-fetching\n' \
            "$(basename "$dest")" >&2
    fi
    tmp=$(mktemp "$dest.XXXXXX")
    curl -fsSL --retry 3 -o "$tmp" "$url" || { rm -f "$tmp"; die "download failed: $url"; }
    got=$(sha256_of "$tmp")
    if [ "$got" != "$want" ]; then
        rm -f "$tmp"
        die "checksum mismatch for $url
  expected $want
  got      $got"
    fi
    mv "$tmp" "$dest"
}

fetch() {
    mkdir -p "$LIB"
    fetch_jar "$MAVEN/net/sf/saxon/Saxon-HE/$SAXON_VERSION/Saxon-HE-$SAXON_VERSION.jar" \
              "$LIB/Saxon-HE-$SAXON_VERSION.jar" "$SAXON_SHA256"
    fetch_jar "$MAVEN/org/xmlresolver/xmlresolver/$XMLRESOLVER_VERSION/xmlresolver-$XMLRESOLVER_VERSION.jar" \
              "$LIB/xmlresolver-$XMLRESOLVER_VERSION.jar" "$XMLRESOLVER_SHA256"
    printf 'Saxon-HE %s and xmlresolver %s verified in %s\n' \
        "$SAXON_VERSION" "$XMLRESOLVER_VERSION" "$LIB"
}

# render <input.akn> <output.html>
render() {
    "$JAVA" -cp "$CP" net.sf.saxon.Transform \
        "-xsl:$XSL" "-s:$1" "-o:$2" "image-base=$IMAGE_BASE"
}

# census <file.html> — "count<TAB>class" for each section class in the render
census() {
    # NB the stylesheet emits id before class, so the id attribute must be
    # allowed for here or every section with an eId is missed.
    grep -o '<section[^>]*class="[^"]*"' "$1" \
        | sed 's/.*class="//; s/"$//' \
        | sort | uniq -c \
        | awk '{ c = $1; $1 = ""; sub(/^ /, ""); print c "\t" $0 }'
}

# compare_census <before.tsv> <after.tsv> — side-by-side table.
# A plain diff of two count-sorted lists misaligns as soon as a count changes
# rank, which makes a demotion look like an unrelated pair of edits.
compare_census() {
    awk -F'\t' '
        NR == FNR { before[$2] = $1; keys[$2] = 1; next }
                  { after[$2]  = $1; keys[$2] = 1 }
        END {
            for (k in keys) {
                b = (k in before) ? before[k] : 0
                a = (k in after)  ? after[k]  : 0
                printf "  %-24s %7d %7d %+7d\n", k, b, a, a - b
            }
        }' "$1" "$2" | sort
}

# refuse_tracked <path> — never clobber a committed golden by accident
refuse_tracked() {
    [ -n "$FORCE" ] && return 0
    git -C "$REPO" ls-files --error-unmatch "$1" >/dev/null 2>&1 \
        && die "$1 is tracked by git (a committed golden?). Use -o/-d, or --force."
    return 0
}

IMAGE_BASE=""
INPUT=""
OUTPUT=""
COMPARE=""
OUTDIR=""
FORCE=""

while [ $# -gt 0 ]; do
    case "$1" in
        --fetch)      fetch; exit 0 ;;
        --compare)    COMPARE="${2:?--compare needs a git revision}"; shift 2 ;;
        --image-base) IMAGE_BASE="${2:?--image-base needs a value}"; shift 2 ;;
        -o)           OUTPUT="${2:?-o needs a path}"; shift 2 ;;
        -d)           OUTDIR="${2:?-d needs a path}"; shift 2 ;;
        --force)      FORCE=1; shift ;;
        -h|--help)    sed -n '3,40p' "$0"; exit 0 ;;
        -*)           die "unknown option: $1" ;;
        *)            [ -z "$INPUT" ] || die "only one input file"; INPUT="$1"; shift ;;
    esac
done

[ -n "$INPUT" ] || die "no input file (try --help)"
[ -f "$XSL" ]   || die "stylesheet not found at $XSL"

JAVA=$(resolve_java)
CP=$(resolve_classpath)

if [ -z "$COMPARE" ]; then
    [ -f "$INPUT" ] || die "no such file: $INPUT"
    if [ -z "$OUTPUT" ]; then
        [ -n "$OUTDIR" ] || OUTDIR=$(mktemp -d "${TMPDIR:-/tmp}/render-akn.XXXXXX")
        mkdir -p "$OUTDIR"
        OUTPUT="$OUTDIR/$(basename "${INPUT%.akn}").html"
    fi
    refuse_tracked "$OUTPUT"
    render "$INPUT" "$OUTPUT"
    printf '%s\n' "$OUTPUT"
    exit 0
fi

# --compare: render the working-tree version and the version at REV, then diff.
REL=$(git -C "$REPO" ls-files --full-name --error-unmatch "$INPUT" 2>/dev/null) \
    || die "--compare needs a tracked file; $INPUT is not in the index"
git -C "$REPO" cat-file -e "$COMPARE:$REL" 2>/dev/null \
    || die "$REL does not exist at revision $COMPARE"

if [ -n "$OUTDIR" ]; then
    mkdir -p "$OUTDIR"
else
    OUTDIR=$(mktemp -d "${TMPDIR:-/tmp}/render-akn.XXXXXX")
fi

BASE=$(basename "${REL%.akn}")
git -C "$REPO" show "$COMPARE:$REL" > "$OUTDIR/$BASE.before.akn"
render "$OUTDIR/$BASE.before.akn" "$OUTDIR/$BASE.before.html"
render "$REPO/$REL"               "$OUTDIR/$BASE.after.html"

printf '\n%s\n  before  %s\n  after   working tree\n\n' "$REL" "$COMPARE"
census "$OUTDIR/$BASE.before.html" > "$OUTDIR/$BASE.before.census"
census "$OUTDIR/$BASE.after.html"  > "$OUTDIR/$BASE.after.census"
printf '  %-24s %7s %7s %7s\n' "section class" "before" "after" "delta"
compare_census "$OUTDIR/$BASE.before.census" "$OUTDIR/$BASE.after.census"
printf '\nhtml\n  %s\n  %s\n' "$OUTDIR/$BASE.before.html" "$OUTDIR/$BASE.after.html"
