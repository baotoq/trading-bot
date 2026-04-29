# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

```bash
# Build
make build                          # outputs to ./bin/tradingbot

# Run
./bin/tradingbot -conf ./configs
go run ./cmd/tradingbot -conf ./configs

# Generate protobuf (API + HTTP + gRPC stubs)
make api                            # from api/**/*.proto
make config                         # from internal/**/*.proto (conf structs)

# Re-generate wire DI
cd cmd/tradingbot && wire

# Generate all
make all                            # api + config + go generate + go mod tidy

# Install code-gen tools
make init
```

Tests: `go test ./...` (no custom test runner).

## Architecture

go-kratos v2 layered monolith with Wire DI. Request flow:

```
proto (api/) → service → biz (usecase) → data (repo)
```

| Layer | Package | Responsibility |
|-------|---------|---------------|
| **server** | `internal/server` | HTTP (`:8000`) + gRPC (`:9000`) server setup, middleware |
| **service** | `internal/service` | Implements proto-generated server interfaces, delegates to biz |
| **biz** | `internal/biz` | Domain logic, defines repo interfaces (`GreeterRepo`), owns domain errors |
| **data** | `internal/data` | Implements biz repo interfaces, holds DB/Redis clients |
| **conf** | `internal/conf` | Config structs generated from `conf.proto`; loaded via `configs/config.yaml` |
| **api** | `api/` | Proto definitions; generated `.pb.go`, `_grpc.pb.go`, `_http.pb.go` |

### Key conventions

- **Dependency direction**: `server → service → biz ← data` (data depends on biz interfaces, not the other way).
- **Wire**: each layer exposes a `ProviderSet`. Add new constructors there when adding types. Regenerate with `wire` after changes to `wire.go`.
- **Errors**: define in `biz` using `errors.NotFound`/`errors.BadRequest` etc. from `go-kratos/kratos/v2/errors`. Map proto `ErrorReason` enums for typed errors.
- **Config**: add fields to `internal/conf/conf.proto`, run `make config` to regenerate, then read via `*conf.Data` or `*conf.Server` injected by Wire.
- **New API endpoints**: define in `api/**/*.proto`, run `make api`, implement in `internal/service`.
