#!/bin/bash
set -e

cat > .git/hooks/pre-commit << 'EOF'
#!/bin/bash
./scripts/validate-version.sh
exit $?
EOF

chmod +x .git/hooks/pre-commit
