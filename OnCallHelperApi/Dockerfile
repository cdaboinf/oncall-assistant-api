FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY . .
RUN dotnet restore OnCallHelperApi.csproj
RUN dotnet publish OnCallHelperApi.csproj \
    -c Release \
    -o /app/publish

FROM public.ecr.aws/lambda/dotnet:9
WORKDIR /var/task
COPY --from=build /app/publish .

# For executable-assembly Lambda ASP.NET Core hosting, use the assembly name
CMD ["OnCallHelperApi"]