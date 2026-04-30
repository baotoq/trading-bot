package biz

import (
	"context"
	"errors"
	"testing"

	"github.com/stretchr/testify/assert"
	"github.com/stretchr/testify/require"
)

type mockGreeterRepo struct {
	saved    *Greeter
	saveErr  error
	findByID map[int64]*Greeter
	findErr  error
}

func (m *mockGreeterRepo) Save(_ context.Context, g *Greeter) (*Greeter, error) {
	if m.saveErr != nil {
		return nil, m.saveErr
	}
	g.ID = 42
	m.saved = g
	return g, nil
}

func (m *mockGreeterRepo) Update(_ context.Context, g *Greeter) (*Greeter, error) {
	return g, nil
}

func (m *mockGreeterRepo) FindByID(_ context.Context, id int64) (*Greeter, error) {
	if m.findErr != nil {
		return nil, m.findErr
	}
	if g, ok := m.findByID[id]; ok {
		return g, nil
	}
	return nil, ErrUserNotFound
}

func (m *mockGreeterRepo) ListByHello(context.Context, string) ([]*Greeter, error) {
	return nil, nil
}

func (m *mockGreeterRepo) ListAll(context.Context) ([]*Greeter, error) {
	return nil, nil
}

func TestCreateGreeter(t *testing.T) {
	repo := &mockGreeterRepo{}
	uc := NewGreeterUsecase(repo)

	got, err := uc.CreateGreeter(context.Background(), &Greeter{Hello: "world"})
	require.NoError(t, err)
	assert.Equal(t, "world", got.Hello)
	assert.Equal(t, 42, got.ID)
	require.NotNil(t, repo.saved)
	assert.Equal(t, "world", repo.saved.Hello)
}

func TestCreateGreeter_RepoError(t *testing.T) {
	wantErr := errors.New("db down")
	uc := NewGreeterUsecase(&mockGreeterRepo{saveErr: wantErr})

	_, err := uc.CreateGreeter(context.Background(), &Greeter{Hello: "x"})
	assert.ErrorIs(t, err, wantErr)
}

func TestGetGreeter(t *testing.T) {
	repo := &mockGreeterRepo{
		findByID: map[int64]*Greeter{
			7: {ID: 7, Hello: "found"},
		},
	}
	uc := NewGreeterUsecase(repo)

	got, err := uc.GetGreeter(context.Background(), 7)
	require.NoError(t, err)
	assert.Equal(t, 7, got.ID)
	assert.Equal(t, "found", got.Hello)
}

func TestGetGreeter_NotFound(t *testing.T) {
	uc := NewGreeterUsecase(&mockGreeterRepo{findByID: map[int64]*Greeter{}})

	_, err := uc.GetGreeter(context.Background(), 999)
	assert.ErrorIs(t, err, ErrUserNotFound)
}
