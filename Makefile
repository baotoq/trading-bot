.PHONY: install test test-net lint clean

install:
	uv venv
	uv pip install -e ".[dev]"

test:
	pytest

test-net:
	pytest -m testnet

clean:
	rm -rf .pytest_cache build dist *.egg-info
