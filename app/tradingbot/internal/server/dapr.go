package server

import (
	"context"
	"encoding/json"
	"io"
	"net/http"
	"reflect"
	"strings"

	khttp "github.com/go-kratos/kratos/v2/transport/http"
)

type daprSubscription struct {
	PubsubName string `json:"pubsubname"`
	Topic      string `json:"topic"`
	Route      string `json:"route"`
}

type daprRoute struct {
	path    string
	handler http.HandlerFunc
}

// DaprSubscriber builds the /dapr/subscribe manifest and matching routes.
type DaprSubscriber struct {
	pubsub string
	subs   []daprSubscription
	routes []daprRoute
}

func NewDaprSubscriber(pubsub string) *DaprSubscriber {
	return &DaprSubscriber{pubsub: pubsub}
}

// topicFromType derives a topic name from a type: TradingEvent → "tradingevent".
func topicFromType[T any]() string {
	return strings.ToLower(reflect.TypeOf((*T)(nil)).Elem().Name())
}

// Subscribe registers a typed handler for T. Topic and route are derived from
// the type name (TradingEvent → topic "trading", route "pubsub/handle/trading").
// The CloudEvent envelope is unwrapped; T receives only the "data" field.
func Subscribe[T any](s *DaprSubscriber, handler func(ctx context.Context, e T) error) {
	topic := topicFromType[T]()
	route := "pubsub/handle/" + topic
	s.subs = append(s.subs, daprSubscription{
		PubsubName: s.pubsub,
		Topic:      topic,
		Route:      route,
	})
	s.routes = append(s.routes, daprRoute{
		path: "/" + route,
		handler: func(w http.ResponseWriter, r *http.Request) {
			body, _ := io.ReadAll(r.Body)
			var envelope struct {
				Data T `json:"data"`
			}
			if len(body) > 0 {
				_ = json.Unmarshal(body, &envelope)
			}
			if err := handler(r.Context(), envelope.Data); err != nil {
				w.WriteHeader(http.StatusInternalServerError)
				return
			}
			w.WriteHeader(http.StatusOK)
		},
	})
}

// Mount registers /dapr/subscribe and all topic routes onto the HTTP server.
func (s *DaprSubscriber) Mount(hs *khttp.Server) {
	subsJSON, _ := json.Marshal(s.subs)
	hs.HandleFunc("/dapr/subscribe", func(w http.ResponseWriter, r *http.Request) {
		w.Header().Set("Content-Type", "application/json")
		w.Write(subsJSON)
	})
	for _, r := range s.routes {
		hs.HandleFunc(r.path, r.handler)
	}
}
