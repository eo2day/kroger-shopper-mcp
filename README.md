# kroger-shopper-mcp

`kroger-shopper-mcp` is a local C# service for Kroger shopper automation. It handles OAuth, store-aware product search, cart add flows, and local cart tracking for higher-level agent or MCP-style integrations.

> [!NOTE]
> This project was developed in collaboration with a personal agentic AI workflow to accelerate local automation, rapid iteration, and hands-on integration work.

## Overview

The service is designed to sit between a Kroger shopper account and local automation tooling. It provides a lightweight HTTP API for:

- generating shopper authorization URLs
- exchanging and refreshing Kroger OAuth tokens
- searching products by store
- tracking a local cart view
- adding items to a Kroger cart with stock-aware safety checks

## Features

- OAuth authorization code flow for Kroger shopper accounts
- app-managed login gate for the authorize page with first-run credential setup
- local SQLite storage for token metadata, pending OAuth state, settings, and tracked cart items
- store search and default-store selection
- product search scoped to a Kroger location
- cart add support with:
  - dry-run preview mode
  - out-of-stock blocking
  - optional override for `unknown_stock` cases
- local tracked-cart inspection with live stock revalidation
- staged cart composition before live Kroger commits
- purchased-item history with quantities and timestamps
- named saved carts backed by SQLite for quick reuse

## Configuration

The service reads its runtime configuration from environment variables.

Required variables:

- `KROGER_CLIENT_ID`
- `KROGER_CLIENT_SECRET`
- `KROGER_REDIRECT_URI`
- `KROGER_BANNER`
- `KROGER_TOKEN_URL`
- `KROGER_AUTHORIZE_URL`
- `KROGER_DB_PATH`

Optional variables:

- `KROGER_SERVICE_URL`
- `KROGER_PUBLIC_BASE_URL`
- `KROGER_ENV_FILE`
- `KROGER_ALLOW_INSECURE_ENV_FILE`

## Security

- Secrets are not stored in tracked source files.
- OAuth credentials should be provided through environment variables or an external env file.
- The web login password is stored as a salted PBKDF2 hash in the local SQLite database; it is not kept in source control.
- A sample local configuration file is provided at `.env.example`.
- The env loader prefers an explicit `KROGER_ENV_FILE` path or repo-local env files, rather than walking arbitrary parent workspace paths.
- On Unix-like systems, env files with group/other permissions are rejected by default unless `KROGER_ALLOW_INSECURE_ENV_FILE=true` is explicitly set.
- Token values are stored in the local SQLite database, but API responses expose only token metadata such as expiry and stored length.
- The local SQLite database file is hardened to owner-only permissions on Unix-like systems on a best-effort basis.
- Runtime state should remain outside the repository.

## Development

From the repository root:

```bash
cp .env.example .env
dotnet restore
dotnet build
```

Initialize the local database:

```bash
dotnet run -- init-db
```

Generate a fresh authorization URL:

```bash
dotnet run -- auth-url
```

Exchange a returned authorization code:

```bash
dotnet run -- exchange-code --code "PASTE_CODE_HERE"
```

Show stored token metadata without printing secret values:

```bash
dotnet run -- show-token
```

Refresh the stored access token:

```bash
dotnet run -- refresh-token
```

Run the local HTTP service:

```bash
dotnet run -- serve
```

## HTTP API

Common local endpoints:

- `GET /healthz`
- `GET /api/status`
- `POST /api/refresh`
- `GET /api/search-products`
- `GET /api/search-locations`
- `POST /api/set-default-store`
- `GET /api/cart-info`
- `POST /api/add-to-cart`
- `GET /api/staged-cart`
- `POST /api/add-to-staged-cart`
- `POST /api/remove-staged-cart-item`
- `POST /api/clear-staged-cart`
- `POST /api/remove-tracked-cart-item`
- `POST /api/mark-purchased`
- `POST /api/clear-tracked-cart`
- `GET /api/purchased-items`
- `POST /api/save-cart`
- `POST /api/save-staged-cart`
- `GET /api/saved-carts`
- `POST /api/load-saved-cart-to-staged`
- `POST /api/commit-staged-cart`
- `POST /api/apply-saved-cart`
- `POST /api/command`

Example:

```bash
curl -s http://127.0.0.1:5092/api/status
```

## Storage and Runtime

This repository contains application source only.

Typical runtime dependencies live outside the repo, for example:

- env file for local deployment
- SQLite database file
- reverse proxy configuration
- user service/unit configuration

## Example Service Setup

One straightforward deployment pattern is a user-level `systemd` service that points at a separate env file and runs the built application from the repository checkout.

Example unit:

```ini
[Unit]
Description=Kroger Shopper MCP
After=network-online.target
Wants=network-online.target

[Service]
Type=simple
WorkingDirectory=/path/to/kroger-shopper-mcp
Environment=KROGER_ENV_FILE=/path/to/runtime/kroger.env
ExecStart=/usr/bin/dotnet /path/to/kroger-shopper-mcp/bin/Debug/net10.0/KrogerCs.dll serve
Restart=always
RestartSec=2

[Install]
WantedBy=default.target
```

Typical setup flow:

```bash
cp .env.example /path/to/runtime/kroger.env
chmod 600 /path/to/runtime/kroger.env
dotnet build
mkdir -p ~/.config/systemd/user
$EDITOR ~/.config/systemd/user/kroger-shopper-mcp.service
systemctl --user daemon-reload
systemctl --user enable --now kroger-shopper-mcp.service
systemctl --user status kroger-shopper-mcp.service
```

For internet-facing deployments, place a reverse proxy in front of the local service and keep authentication, TLS termination, and any public callback routes in that outer layer.

## Status

The current implementation is a local HTTP service that can support MCP-style orchestration, but it is not yet packaged as a formal MCP server.
