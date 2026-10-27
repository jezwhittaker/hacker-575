# OctoCAT Supply Chain Management - Makefile
# Cross-platform compatible (Linux, macOS, Windows with Git Bash or WSL)

# Detect OS for platform-specific commands
ifeq ($(OS),Windows_NT)
    DETECTED_OS := Windows
    RM_RF = powershell -Command "Remove-Item -Recurse -Force -ErrorAction SilentlyContinue"
    MKDIR_P = powershell -Command "New-Item -ItemType Directory -Force -Path"
else
    DETECTED_OS := $(shell uname -s)
    RM_RF = rm -rf
    MKDIR_P = mkdir -p
endif

API_CS_DIR := api-cs
API_CS_TEST_DIR := api-cs-tests
FRONTEND_DIR := frontend
PLAYWRIGHT_BROWSERS_PATH ?= $(HOME)/.cache/ms-playwright

# Resolve backend URL for frontend dev server.
# In Codespaces, browsers cannot reach container-local localhost, so use the
# forwarded host pattern: https://<codespace>-<port>.<domain>.
CODESPACES_DOMAIN := $(if $(GITHUB_CODESPACES_PORT_FORWARDING_DOMAIN),$(GITHUB_CODESPACES_PORT_FORWARDING_DOMAIN),app.github.dev)
define resolve_api_url
$(if $(strip $(CODESPACE_NAME)),https://$(CODESPACE_NAME)-$(1).$(CODESPACES_DOMAIN),http://localhost:$(1))
endef

CS_API_URL := $(call resolve_api_url,3000)

.DEFAULT_GOAL := help

##@ General

.PHONY: help
help: ## Display this help message
	@awk 'BEGIN {FS = ":.*##"; printf "\nUsage:\n  make \033[36m<target>\033[0m\n"} /^[a-zA-Z_-]+:.*?##/ { printf "  \033[36m%-20s\033[0m %s\n", $$1, $$2 } /^##@/ { printf "\n\033[1m%s\033[0m\n", substr($$0, 5) } ' $(MAKEFILE_LIST)

.PHONY: info
info: ## Show detected OS and directories
	@echo "Detected OS:        $(DETECTED_OS)"
	@echo "Frontend Directory: $(FRONTEND_DIR)"
	@echo "API:                $(API_CS_DIR)"

##@ Installation

.PHONY: install
install: install-cs install-frontend ## Install all dependencies

.PHONY: install-cs
install-cs: ## Restore C# API NuGet packages
	cd $(API_CS_DIR) && dotnet restore
	cd $(API_CS_TEST_DIR) && dotnet restore

.PHONY: install-frontend
install-frontend: ## Install frontend dependencies
	cd $(FRONTEND_DIR) && npm install

##@ Development — Full Stack (API + Frontend)

.PHONY: dev
dev: dev-cs ## Start C# API + frontend (default)

.PHONY: dev-cs
dev-cs: ## Start C# API (port 3000) + frontend
ifeq ($(OS),Windows_NT)
	npx concurrently --kill-others "cd $(API_CS_DIR) && set PORT=3000 && dotnet run" "cd $(FRONTEND_DIR) && set VITE_API_URL=$(CS_API_URL) && npm run dev"
else
	@trap 'kill 0' INT; \
	(cd $(API_CS_DIR) && PORT=3000 dotnet run) & \
	(cd $(FRONTEND_DIR) && VITE_API_URL=$(CS_API_URL) npm run dev) & \
	wait
endif

##@ Development — API Only

.PHONY: dev-api-cs
dev-api-cs: ## Start only the C# API (port 3000)
	cd $(API_CS_DIR) && PORT=3000 dotnet run

.PHONY: dev-frontend
dev-frontend: ## Start only the frontend
	cd $(FRONTEND_DIR) && npm run dev

##@ Building

.PHONY: build
build: build-cs build-frontend ## Build C# API + frontend

.PHONY: build-cs
build-cs: ## Build the C# API
	cd $(API_CS_DIR) && dotnet build -c Release

.PHONY: build-frontend
build-frontend: ## Build the frontend
	cd $(FRONTEND_DIR) && npm run build

##@ Testing

.PHONY: test
test: test-cs test-frontend ## Run C# API tests + frontend tests

.PHONY: test-cs
test-cs: ## Run C# API tests
	cd $(API_CS_TEST_DIR) && dotnet test

.PHONY: install-playwright-browsers
install-playwright-browsers: ## Install Playwright browser binaries used by frontend e2e tests
	@# Skip install if a Chromium binary already exists in the cache
	@if find "$(PLAYWRIGHT_BROWSERS_PATH)" -path '*/chrome-linux/chrome' -print -quit 2>/dev/null | grep -q .; then \
		echo "Playwright Chromium already installed, skipping."; \
	else \
		$(MKDIR_P) "$(PLAYWRIGHT_BROWSERS_PATH)"; \
		cd $(FRONTEND_DIR) && PLAYWRIGHT_BROWSERS_PATH="$(PLAYWRIGHT_BROWSERS_PATH)" npx playwright install chromium; \
	fi

.PHONY: test-frontend
test-frontend: install-playwright-browsers ## Run frontend tests
	cd $(FRONTEND_DIR) && PLAYWRIGHT_BROWSERS_PATH="$(PLAYWRIGHT_BROWSERS_PATH)" npm run test

.PHONY: test-e2e
test-e2e: install-playwright-browsers ## Run end-to-end tests
	cd $(FRONTEND_DIR) && PLAYWRIGHT_BROWSERS_PATH="$(PLAYWRIGHT_BROWSERS_PATH)" npm run test:e2e

##@ Linting

.PHONY: lint
lint: ## Lint the frontend
	cd $(FRONTEND_DIR) && npm run lint

.PHONY: lint-fix
lint-fix: ## Lint and auto-fix frontend issues
	cd $(FRONTEND_DIR) && npm run lint -- --fix

.PHONY: format
format: ## Format frontend code with prettier
	npx prettier --write "$(FRONTEND_DIR)/**/*.{ts,tsx}"

##@ Docker

.PHONY: docker-build
docker-build: ## Build Docker images
	docker-compose build

.PHONY: docker-up
docker-up: ## Start Docker containers
	docker-compose up

.PHONY: docker-down
docker-down: ## Stop Docker containers
	docker-compose down

##@ Cleaning

.PHONY: clean
clean: ## Clean build artifacts and node_modules
	$(RM_RF) $(API_CS_DIR)/bin $(API_CS_DIR)/obj $(API_CS_DIR)/data
	$(RM_RF) $(API_CS_TEST_DIR)/bin $(API_CS_TEST_DIR)/obj
	$(RM_RF) $(FRONTEND_DIR)/node_modules $(FRONTEND_DIR)/dist
