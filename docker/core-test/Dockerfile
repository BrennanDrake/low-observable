# Runs the core unit tests and a short headless simulation in a clean container. Handy on the NAS or a
# Gitea runner without .NET installed:
#   docker build -f docker/core-test/Dockerfile -t low-observable-core-test . && docker run --rm low-observable-core-test
FROM mcr.microsoft.com/dotnet/sdk:8.0

WORKDIR /src
COPY LowObservable.sln ./
COPY core ./core
COPY dotnet ./dotnet
RUN dotnet restore LowObservable.sln

CMD ["bash", "-c", "dotnet test dotnet/LowObservable.Core.Tests -nologo && dotnet run -c Release --project dotnet/LowObservable.Sim -- --policy all --seeds 5"]
