load('ext://restart_process', 'docker_build_with_restart')

allow_k8s_contexts(['docker-desktop', 'orbstack'])

# Usage:
#   tilt up                  normal build
#   tilt up -- --debug       build with Delve, attach VS Code on :2345
#   tilt args -- --debug     toggle without restarting Tilt
config.define_bool('debug')
cfg = config.parse()
debug = cfg.get('debug', False)

entrypoint = ['/app/entrypoint.sh']

if debug:
    compile_cmd = 'mkdir -p dist && GOOS=linux GOARCH=arm64 CGO_ENABLED=0 go build -gcflags="all=-N -l" -ldflags "-X main.Version=dev" -o ./dist/tradingbot ./cmd/tradingbot'
else:
    compile_cmd = 'mkdir -p dist && GOOS=linux GOARCH=arm64 CGO_ENABLED=0 go build -ldflags "-X main.Version=$(git describe --tags --always 2>/dev/null || echo dev)" -o ./dist/tradingbot ./cmd/tradingbot'

# Ensure binary exists at load time so docker_build doesn't race on first tilt up.
local('mkdir -p dist && [ -f dist/tradingbot ] || ' + compile_cmd, quiet=True)

# Compile locally on every Go source change.
# Result is synced into the running container — no full image rebuild needed.
local_resource('compile',
    cmd=compile_cmd,
    deps=['./cmd', './internal', './api', 'go.mod', 'go.sum'],
    labels=['build'],
    allow_parallel=True,
)

# Ensure Helm chart tarballs exist at load time (helm() is called synchronously below).
# The local_resource re-runs when Chart.yaml changes during a live session.
local('[ -d deploy/helm/charts ] || helm dependency update deploy/helm', quiet=True)
local_resource('helm-deps',
    cmd='helm dependency update deploy/helm',
    deps=['deploy/helm/Chart.yaml'],
    labels=['build'],
)

# Tilt-optimised Dockerfiles contain no Go toolchain — they just copy ./dist/tradingbot.
# only=['./dist'] means docker_build watches *only* that dir, so Go source file
# changes never trigger an image rebuild; they go through compile → sync instead.
docker_build_with_restart(
    'tradingbot',
    '.',
    entrypoint=entrypoint,
    dockerfile='Dockerfile.dev.debug' if debug else 'Dockerfile.dev',
    only=['./dist'],
    live_update=[
        sync('./dist/tradingbot', '/app/tradingbot'),
    ],
)

k8s_yaml(helm(
    'deploy/helm',
    name='deps',
    namespace='trading-bot',
    values=['deploy/helm/values.yaml'],
))

overlay = 'deploy/k8s/overlays/debug' if debug else 'deploy/k8s/overlays/local'
k8s_yaml(local('kubectl kustomize --load-restrictor LoadRestrictionsNone ' + overlay, quiet=True))
watch_file('configs/config.yaml')

k8s_resource('postgres', port_forwards=['5432:5432'], labels=['infra'])
k8s_resource('redis',    port_forwards=['6379:6379'], labels=['infra'])
k8s_resource('pgadmin',  port_forwards=['5050:80'],   labels=['infra'], resource_deps=['postgres'])

app_ports = ['8000:8000', '9000:9000']
if debug:
    app_ports.append('2345:2345')

k8s_resource('tradingbot',
    port_forwards=app_ports,
    resource_deps=['postgres', 'redis', 'compile'],
    labels=['app'],
)
