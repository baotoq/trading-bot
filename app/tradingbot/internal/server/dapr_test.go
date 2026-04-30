package server_test

import (
	"context"
	"encoding/json"
	"io"
	"net/http"
	"net/http/httptest"
	"testing"

	"tradingbot/app/tradingbot/internal/server"

	transhttp "github.com/go-kratos/kratos/v2/transport/http"
	"github.com/stretchr/testify/assert"
	"github.com/stretchr/testify/require"
)

type tradingEvent struct {
	Value string `json:"value"`
}

func newDaprHandler() http.Handler {
	srv := transhttp.NewServer()
	sub := server.NewDaprSubscriber("pubsub")
	server.Subscribe(sub, func(_ context.Context, _ tradingEvent) error {
		return nil
	})
	sub.Mount(srv)
	return srv
}

func TestDaprSubscribe(t *testing.T) {
	// Arrange
	h := newDaprHandler()
	w := httptest.NewRecorder()
	r := httptest.NewRequest(http.MethodGet, "/dapr/subscribe", nil)

	// Act
	h.ServeHTTP(w, r)

	// Assert
	assert.Equal(t, http.StatusOK, w.Code)
	assert.Equal(t, "application/json", w.Header().Get("Content-Type"))

	body, err := io.ReadAll(w.Body)
	require.NoError(t, err)

	var subs []map[string]string
	require.NoError(t, json.Unmarshal(body, &subs))
	require.Len(t, subs, 1)
	assert.Equal(t, "pubsub", subs[0]["pubsubname"])
	assert.Equal(t, "tradingevent", subs[0]["topic"])
	assert.Equal(t, "pubsub/handle/tradingevent", subs[0]["route"])
}

func TestDaprTradingEvent(t *testing.T) {
	// Arrange
	h := newDaprHandler()
	w := httptest.NewRecorder()
	r := httptest.NewRequest(http.MethodPost, "/pubsub/handle/tradingevent", nil)

	// Act
	h.ServeHTTP(w, r)

	// Assert
	assert.Equal(t, http.StatusOK, w.Code)
}

func TestDaprJobHandler(t *testing.T) {
	// Arrange
	srv := transhttp.NewServer()
	sub := server.NewDaprSubscriber("pubsub")
	called := false
	server.RegisterJobHandler(sub, "strategy-tick-foo", func(_ context.Context, _ []byte) error {
		called = true
		return nil
	})
	sub.Mount(srv)
	w := httptest.NewRecorder()
	r := httptest.NewRequest(http.MethodPost, "/job/strategy-tick-foo", nil)

	// Act
	srv.ServeHTTP(w, r)

	// Assert
	assert.Equal(t, http.StatusOK, w.Code)
	assert.True(t, called)
}
