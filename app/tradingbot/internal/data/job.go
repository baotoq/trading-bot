package data

import (
	"context"

	"tradingbot/app/tradingbot/internal/biz"

	dapr "github.com/dapr/go-sdk/client"
	"google.golang.org/protobuf/types/known/anypb"
	"google.golang.org/protobuf/types/known/wrapperspb"
)

type daprJobScheduler struct {
	client dapr.Client
}

func NewJobScheduler(client dapr.Client) biz.JobScheduler {
	return &daprJobScheduler{client: client}
}

func (s *daprJobScheduler) Schedule(ctx context.Context, j biz.Job) error {
	job := dapr.NewJob(j.Name, dapr.WithJobSchedule(j.Schedule))
	job.Overwrite = true
	if len(j.Data) > 0 {
		data, err := anypb.New(wrapperspb.Bytes(j.Data))
		if err != nil {
			return err
		}
		job.Data = data
	}
	return s.client.ScheduleJobAlpha1(ctx, job)
}

func (s *daprJobScheduler) Delete(ctx context.Context, name string) error {
	return s.client.DeleteJobAlpha1(ctx, name)
}
