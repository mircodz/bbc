# Bond

A .NET implementation of the Bond IDL compiler and toolchain.

## Quick Start

```bash
make install
```

Once installed, use the `bondx` command:

```bash
bondx parse schema.bond
bondx breaking schema.bond --against .git#branch=main --error-format=json
bondx breaking examples/catalog_v2.bond --against examples/catalog_v1.bond --error-format=json | jq .
bondx breaking schema.bond --against .git#branch=main --ignore-imports
bondx format schema.bond
bondx format schema.bond --check
```
