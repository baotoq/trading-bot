load('ext://restart_process', 'docker_build_with_restart')
load('ext://helm_resource', 'helm_resource', 'helm_repo')
allow_k8s_contexts(['docker-desktop', 'orbstack'])

entrypoint = ['/app/entrypoint.sh']

compile_cmd = 'mkdir -p dist && GOOS=linux GOARCH=arm64 CGO_ENABLED=0 go build -gcflags="all=-N -l" -ldflags "-X main.Version=dev" -o ./dist/tradingbot ./app/tradingbot/cmd/server'

# Ensure binary exists at load time so docker_build doesn't race on first tilt up.
local('mkdir -p dist && [ -f dist/tradingbot ] || ' + compile_cmd, quiet=True)

# Compile locally on every Go source change.
# Result is synced into the running container — no full image rebuild needed.
local_resource('compile',
    cmd=compile_cmd,
    deps=['./app', './api', 'go.mod', 'go.sum'],
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

helm_repo('dapr-repo', 'https://dapr.github.io/helm-charts/', labels=['infra'])
helm_resource(
    'dapr',
    'dapr-repo/dapr',
    namespace='dapr-system',
    flags=[
        '--version=1.17.6',
        '--create-namespace',
        '--set=global.ha.enabled=false',
    ],
    resource_deps=['dapr-repo'],
    labels=['infra'],
)

# Tilt-optimised Dockerfile contains no Go toolchain — it just copies ./dist/tradingbot.
# only=['./dist'] means docker_build watches *only* that dir, so Go source file
# changes never trigger an image rebuild; they go through compile → sync instead.
docker_build_with_restart(
    'tradingbot',
    '.',
    entrypoint=entrypoint,
    dockerfile='app/tradingbot/Dockerfile.dev.debug',
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

k8s_yaml(kustomize('deploy/k8s/overlays/debug', flags=['--load-restrictor=LoadRestrictionsNone']))
watch_file('app/tradingbot/configs/config.yaml')

k8s_resource('postgres', port_forwards=['5432:5432'], labels=['infra'])
k8s_resource('redis',    port_forwards=['6379:6379'], labels=['infra'])
k8s_resource('pgadmin',  port_forwards=['5050:80'],   labels=['infra'], resource_deps=['postgres'])

k8s_resource(
    objects=[
        'pubsub:Component',
        'secretstore:Component',
    ],
    new_name='dapr-components',
    resource_deps=['dapr'],
    labels=['infra'],
)

k8s_resource('tradingbot',
    port_forwards=['8000:8000', '9000:9000', '2345:2345'],
    resource_deps=['postgres', 'redis', 'compile', 'dapr-components'],
    labels=['app'],
)
