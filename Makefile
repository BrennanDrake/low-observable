# Convenience targets. Every target wraps a script in scripts/ so CI, the NAS and humans run the same thing.
.PHONY: lint test sim sim-runs unity web-image registry-tags clean

lint:
	@./scripts/pc/lint.sh

# Core unit tests (no Unity needed).
test:
	@./scripts/pc/test_core.sh

# Headless balance check: every autopilot policy on the MVP floor.
sim:
	@./scripts/pc/sim.sh --policy all --seeds 30

# Same, but also write per-run CSVs to experiments/runs for tools/plot_run.py.
sim-runs:
	@./scripts/pc/sim.sh --policy all --seeds 5 --out experiments/runs

# Unity WebGL build in batch mode (needs UNITY_PATH or Unity Hub's default install).
unity:
	@./scripts/pc/build_unity.sh webgl

# Wrap the WebGL build in nginx and push :<VERSION>, :git-<sha>, :latest to the NAS registry.
web-image:
	@./scripts/pc/build_web_image.sh

registry-tags:
	@./scripts/pc/registry_tags.sh

clean:
	@rm -rf build dotnet/*/bin dotnet/*/obj
