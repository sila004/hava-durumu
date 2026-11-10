# Build context: InternRepository
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY Intern.API/Intern.API.csproj Intern.API/
COPY Intern.Core/Intern.Core.csproj Intern.Core/
COPY Intern.Data/Intern.Data.csproj Intern.Data/
COPY Intern.Entities/Intern.Entities.csproj Intern.Entities/
COPY Intern.Service/Intern.Service.csproj Intern.Service/
COPY Intern.Shared/Intern.Shared.csproj Intern.Shared/
RUN dotnet restore Intern.API/Intern.API.csproj
COPY Intern.API/ Intern.API/
COPY Intern.Core/ Intern.Core/
COPY Intern.Data/ Intern.Data/
COPY Intern.Entities/ Intern.Entities/
COPY Intern.Service/ Intern.Service/
COPY Intern.Shared/ Intern.Shared/
RUN dotnet publish Intern.API/Intern.API.csproj -c Release --no-restore -o /out /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_HTTP_PORTS=10000
EXPOSE 10000
COPY --from=build --chown=app:app /out .
RUN mkdir -p /app/Logs && chown app:app /app/Logs
USER app
ENTRYPOINT ["dotnet", "Intern.API.dll"]
