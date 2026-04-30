package server

import (
	"encoding/json"
	"net/http"

	khttp "github.com/go-kratos/kratos/v2/transport/http"
)

type daprSubscription struct {
	PubsubName string `json:"pubsubname"`
	Topic      string `json:"topic"`
	Route      string `json:"route"`
}

// RegisterDaprHandlers mounts Dapr appcallback routes onto the kratos HTTP server.
// The sidecar calls /dapr/subscribe on startup to discover subscriptions, then
// POSTs events to the registered route.
func RegisterDaprHandlers(hs *khttp.Server) {
	subs := []daprSubscription{
		{PubsubName: "pubsub", Topic: "trading", Route: "pubsub/events/trading"},
	}

	subsJSON, _ := json.Marshal(subs)
	hs.HandleFunc("/dapr/subscribe", func(w http.ResponseWriter, r *http.Request) {
		w.Header().Set("Content-Type", "application/json")
		w.Write(subsJSON)
	})

	hs.HandleFunc("/pubsub/events/trading", func(w http.ResponseWriter, r *http.Request) {
		w.WriteHeader(http.StatusOK)
	})
}
