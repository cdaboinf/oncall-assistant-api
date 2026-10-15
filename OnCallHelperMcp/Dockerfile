# Hosted MCP server on Lambda (HTTP mode is selected automatically inside Lambda).
# The Lambda runs on x86_64 (see Infra/mcp.tf: architectures = ["x86_64"]).
# Pin the platform so the image is always x86_64 even when built on an
# arm64 host (Apple Silicon); otherwise the Lambda fails to start.
FROM --platform=linux/amd64 mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY . .
RUN dotnet restore OnCallHelperMcp.csproj
RUN dotnet publish OnCallHelperMcp.csproj \
    -c Release \
    -o /app/publish

FROM --platform=linux/amd64 public.ecr.aws/lambda/dotnet:9
WORKDIR /var/task
COPY --from=build /app/publish .

# For executable-assembly Lambda ASP.NET Core hosting, use the assembly name
CMD ["OnCallHelperMcp"]
