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
