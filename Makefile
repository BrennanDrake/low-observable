# Convenience targets. Every target wraps a script in scripts/ so CI, the NAS and humans run the same thing.
.PHONY: lint test sim sim-runs stages stage-gen unity-check unity web-image registry-tags clean

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

# Escape campaign: reachability of every level, then a bot balance table.
stages:
	@./scripts/pc/stage_check.sh

# Regenerate core/Runtime/Stages/BuiltInStages.cs from levels/stages/*.lvl.
stage-gen:
	@python3 scripts/pc/gen_stages.py

# Compile the Unity scripts against Unity's reference assemblies (no editor needed; uses nuget.org).
unity-check:
	@./scripts/pc/unity_check.sh

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
