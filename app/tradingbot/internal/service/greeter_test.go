package service_test

import (
	"context"
	"net/http/httptest"
	"sync"
	"testing"

	v1 "tradingbot/app/tradingbot/api/helloworld/v1"
	"tradingbot/app/tradingbot/internal/biz"
	"tradingbot/app/tradingbot/internal/service"

	kratoserrors "github.com/go-kratos/kratos/v2/errors"
	transhttp "github.com/go-kratos/kratos/v2/transport/http"
	"github.com/stretchr/testify/assert"
	"github.com/stretchr/testify/require"
)

type fakeGreeterRepo struct {
	mu    sync.Mutex
	store map[int64]*biz.Greeter
	next  int
}

func newFakeRepo() *fakeGreeterRepo {
	return &fakeGreeterRepo{store: make(map[int64]*biz.Greeter)}
}

func (r *fakeGreeterRepo) Save(_ context.Context, g *biz.Greeter) (*biz.Greeter, error) {
	r.mu.Lock()
	defer r.mu.Unlock()
	r.next++
	g.ID = r.next
	r.store[int64(g.ID)] = g
	return g, nil
}

func (r *fakeGreeterRepo) Update(_ context.Context, g *biz.Greeter) (*biz.Greeter, error) {
	return g, nil
}

func (r *fakeGreeterRepo) FindByID(_ context.Context, id int64) (*biz.Greeter, error) {
	r.mu.Lock()
	defer r.mu.Unlock()
	if g, ok := r.store[id]; ok {
		return g, nil
	}
	return nil, biz.ErrUserNotFound
}

func (r *fakeGreeterRepo) ListByHello(context.Context, string) ([]*biz.Greeter, error) {
	return nil, nil
}

func (r *fakeGreeterRepo) ListAll(context.Context) ([]*biz.Greeter, error) {
	return nil, nil
}

func newGreeterTestClient(t *testing.T) v1.GreeterHTTPClient {
	t.Helper()

	svc := service.NewGreeterService(biz.NewGreeterUsecase(newFakeRepo()))
	srv := transhttp.NewServer()
	v1.RegisterGreeterHTTPServer(srv, svc)

	ts := httptest.NewServer(srv)
	t.Cleanup(ts.Close)

	cli, err := transhttp.NewClient(context.Background(), transhttp.WithEndpoint(ts.Listener.Addr().String()))
	require.NoError(t, err)
	return v1.NewGreeterHTTPClient(cli)
}

func TestGreeterHTTP_SayHello(t *testing.T) {
	// Arrange
	client := newGreeterTestClient(t)

	// Act
	reply, err := client.SayHello(context.Background(), &v1.HelloRequest{Name: "kratos"})

	// Assert
	require.NoError(t, err)
	assert.Equal(t, "Hello kratos", reply.Message)
}

func TestGreeterHTTP_CreateAndGet(t *testing.T) {
	// Arrange
	client := newGreeterTestClient(t)
	ctx := context.Background()

	// Act
	created, err := client.CreateGreeter(ctx, &v1.CreateGreeterRequest{Hello: "alice"})
	require.NoError(t, err)

	got, err := client.GetGreeter(ctx, &v1.GetGreeterRequest{Id: created.Id})

	// Assert
	require.NoError(t, err)
	assert.NotZero(t, created.Id)
	assert.Equal(t, "alice", created.Hello)
	assert.Equal(t, created.Id, got.Id)
	assert.Equal(t, "alice", got.Hello)
}

func TestGreeterHTTP_NotFound(t *testing.T) {
	// Arrange
	client := newGreeterTestClient(t)

	// Act
	_, err := client.GetGreeter(context.Background(), &v1.GetGreeterRequest{Id: 9999})

	// Assert
	require.Error(t, err)
	assert.Equal(t, v1.ErrorReason_USER_NOT_FOUND.String(), kratoserrors.Reason(err))
}
