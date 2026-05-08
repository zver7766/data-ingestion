FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
ARG APP_UID=10001
RUN useradd --uid $APP_UID --create-home appuser
USER appuser
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["DataIngestion/DataIngestion.csproj", "DataIngestion/"]
COPY ["DataIngestion.Tests/DataIngestion.Tests.csproj", "DataIngestion.Tests/"]
RUN dotnet restore "DataIngestion/DataIngestion.csproj"
RUN dotnet restore "DataIngestion.Tests/DataIngestion.Tests.csproj"
COPY . .

# Run unit tests as part of the build. Image build fails if tests fail.
RUN dotnet test "DataIngestion.Tests/DataIngestion.Tests.csproj" -c $BUILD_CONFIGURATION --no-restore

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
WORKDIR "/src/DataIngestion"
RUN dotnet publish "DataIngestion.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "DataIngestion.dll"]
