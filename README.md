# Bond

A .NET implementation of the Bond IDL compiler and toolchain.

## Quick Start

```bash
make install
```

Once installed, use the `bond` command:

```bash
bond parse schema.bond
bond breaking schema.bond --against .git#branch=main --error-format=json
bond breaking examples/catalog_v2.bond --against examples/catalog_v1.bond --error-format=json | jq .
bond breaking schema.bond --against .git#branch=main --ignore-imports
bond format schema.bond
bond format schema.bond --check
```
