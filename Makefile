.PHONY: help build test clean pack install uninstall coverage format

# Variables
VERSION := $(shell cat version)
NUPKG_DIR := ./nupkgs
TOOL_NAME := bbc

help: ## Show this help message
	@echo 'Usage: make [target]'
	@echo ''
	@echo 'Available targets:'
	@grep -E '^[a-zA-Z_-]+:.*?## .*$$' $(MAKEFILE_LIST) | awk 'BEGIN {FS = ":.*?## "}; {printf "  %-20s %s\n", $$1, $$2}'

build: ## Build the project in Debug mode
	dotnet build

build-release: ## Build the project in Release mode
	dotnet build -c Release

test: ## Run all tests
	dotnet test Bond.Parser.Tests/Bond.Parser.Tests.csproj

coverage: ## Run tests with coverage report
	bash scripts/coverage.sh

clean: ## Clean build artifacts
	dotnet clean
	rm -rf $(NUPKG_DIR)
	rm -rf */bin */obj

pack: clean build-release ## Pack the CLI tool as a NuGet package
	dotnet pack Bond.Compiler.CLI/Bond.Compiler.CLI.csproj -c Release -o $(NUPKG_DIR)
	@echo ""
	@echo "Package created: $(NUPKG_DIR)/Bond.Compiler.CLI.$(VERSION).nupkg"

install: pack ## Install the tool globally
	dotnet tool uninstall -g $(TOOL_NAME).compiler.cli 2>/dev/null || true
	dotnet tool install --global --add-source $(NUPKG_DIR) BBC.Compiler.CLI --version $(VERSION)
	@echo ""
	@echo "Tool installed! You can now use: $(TOOL_NAME)"
	@echo "Note: You may need to add ~/.dotnet/tools to your PATH"
	@echo ""
	@echo "For zsh, run:"
	@echo "  echo 'export PATH=\"\$$PATH:\$$HOME/.dotnet/tools\"' >> ~/.zprofile"
	@echo "  source ~/.zprofile"

uninstall: ## Uninstall the tool
	dotnet tool uninstall -g $(TOOL_NAME).compiler.cli

reinstall: uninstall install ## Reinstall the tool (clean install)

# Setup
setup: ## Initial setup (restore packages)
	./scripts/setup-hooks.sh
	dotnet restore

# Release
bump-major: ## Bump major version (1.0.0 -> 2.0.0)
	@echo "Current version: $(VERSION)"
	@echo $(VERSION) | awk -F. '{print $$1+1".0.0"}' > version
	@echo "New version: $$(cat version)"

bump-minor: ## Bump minor version (1.0.0 -> 1.1.0)
	@echo "Current version: $(VERSION)"
	@echo $(VERSION) | awk -F. '{print $$1"."$$2+1".0"}' > version
	@echo "New version: $$(cat version)"

bump-patch: ## Bump patch version (1.0.0 -> 1.0.1)
	@echo "Current version: $(VERSION)"
	@echo $(VERSION) | awk -F. '{print $$1"."$$2"."$$3+1}' > version
	@echo "New version: $$(cat version)"

all: clean build test ## Clean, build, and test
