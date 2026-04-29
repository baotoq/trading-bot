# Safety: prevent accidental deploy to non-local clusters
allow_k8s_contexts(['docker-desktop', 'minikube', 'kind-kind', 'rancher-desktop', 'k3d-k3d'])

# Build tradingbot image from Dockerfile
docker_build(
    'tradingbot',
    '.',
    dockerfile='Dockerfile',
)

# Load manifests via Kustomize
k8s_yaml(kustomize('deploy/k8s/overlays/local'))

# Infra resources
k8s_resource('mysql',
    port_forwards=['3306:3306'],
    labels=['infra'],
)

k8s_resource('redis',
    port_forwards=['6379:6379'],
    labels=['infra'],
)

# App resource — waits for infra before deploying
k8s_resource('tradingbot',
    port_forwards=['8000:8000', '9000:9000'],
    resource_deps=['mysql', 'redis'],
    labels=['app'],
)
