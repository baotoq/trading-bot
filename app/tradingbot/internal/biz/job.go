package biz

import "context"

// Job describes a recurring task to be scheduled via Dapr Jobs.
// Schedule accepts cron or "@every <go duration>".
type Job struct {
	Name     string
	Schedule string
	Data     []byte
}

// JobScheduler upserts and removes Dapr jobs. It is implemented by data.
type JobScheduler interface {
	Schedule(ctx context.Context, job Job) error
	Delete(ctx context.Context, name string) error
}
