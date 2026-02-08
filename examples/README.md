```bash
# Parse and pretty-print the v1 schema
bbc parse examples/catalog_v1.bond

# Emit the AST as JSON
bbc parse examples/catalog_v1.bond --json | jq .

# Compare v2 against v1 for breaking changes (non-zero exit on breaking)
bbc breaking examples/catalog_v2.bond --against examples/catalog_v1.bond --error-format=json | jq .
```
