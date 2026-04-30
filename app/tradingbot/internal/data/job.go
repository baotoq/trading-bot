package data

import (
	"context"
	"fmt"

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

// Schedule upserts a recurring job, replacing any existing job with the same name.
func (d *daprJobScheduler) Schedule(ctx context.Context, j biz.Job) error {
	job := dapr.NewJob(j.Name, dapr.WithJobSchedule(j.Schedule))
	job.Overwrite = true
	if len(j.Data) > 0 {
		data, err := anypb.New(wrapperspb.Bytes(j.Data))
		if err != nil {
			return fmt.Errorf("marshal job data: %w", err)
		}
		job.Data = data
	}
	return d.client.ScheduleJobAlpha1(ctx, job)
}

func (d *daprJobScheduler) Delete(ctx context.Context, name string) error {
	return d.client.DeleteJobAlpha1(ctx, name)
}
