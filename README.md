# Bond Breaking Changes

A .NET implementation of the Bond IDL compiler and toolchain.

## Quick Start

```bash
make install
```

Once installed, use the `bbc` command:

```bash
bbc parse schema.bond
bbc breaking schema.bond --against .git#branch=main --error-format=json
bbc breaking examples/catalog_v2.bond --against examples/catalog_v1.bond --error-format=json | jq .
bbc breaking schema.bond --against .git#branch=main --ignore-imports
bbc format schema.bond
bbc format schema.bond --check
```

Pre-commit hook example (format check for staged `.bond` files):

```bash
#!/usr/bin/env bash
set -euo pipefail

files=$(git diff --cached --name-only --diff-filter=ACM | grep -E '\.bond$' || true)
if [ -z "$files" ]; then
  exit 0
fi

for f in $files; do
  bbc format "$f" --check
done
```

PowerShell variant:

```powershell
$files = git diff --cached --name-only --diff-filter=ACM | Where-Object { $_ -match '\.bond$' }
if (-not $files) { exit 0 }
foreach ($f in $files) {
  bbc format $f --check
}
```
