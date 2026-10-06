FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY PromptHub.Web/PromptHub.Web.csproj PromptHub.Web/
RUN dotnet restore PromptHub.Web/PromptHub.Web.csproj
COPY PromptHub.Web/ PromptHub.Web/
RUN dotnet publish PromptHub.Web/PromptHub.Web.csproj \
    -c Release --no-restore -o /out /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080
COPY --from=build --chown=app:app /out/ ./
RUN mkdir -p /app/App_Data /home/app/.aspnet/DataProtection-Keys \
    && chown -R app:app /app/App_Data /home/app/.aspnet
USER app
EXPOSE 8080
ENTRYPOINT ["dotnet", "PromptHub.Web.dll"]
