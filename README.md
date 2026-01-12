# Bond Compiler

A .NET implementation of the Bond IDL compiler and toolchain.

## Quick Start

```bash
make install
```

Once installed, use the `bbc` command:

```bash
bbc parse schema.bond
bbc breaking schema.bond --against .git#branch=main --error-format=json
```
