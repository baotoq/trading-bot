package server_test

import (
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

func newDaprHandler() http.Handler {
	srv := transhttp.NewServer()
	server.RegisterDaprHandlers(srv)
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
	assert.Equal(t, "trading", subs[0]["topic"])
	assert.Equal(t, "pubsub/events/trading", subs[0]["route"])
}

func TestDaprTradingEvent(t *testing.T) {
	// Arrange
	h := newDaprHandler()
	w := httptest.NewRecorder()
	r := httptest.NewRequest(http.MethodPost, "/pubsub/events/trading", nil)

	// Act
	h.ServeHTTP(w, r)

	// Assert
	assert.Equal(t, http.StatusOK, w.Code)
}
