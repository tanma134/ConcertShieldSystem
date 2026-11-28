FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG PROJECT
WORKDIR /src
COPY backend/ ./
RUN dotnet restore "${PROJECT}/${PROJECT}.csproj"
RUN dotnet publish "${PROJECT}/${PROJECT}.csproj" -c Release --no-restore -o /app/publish
COPY backend/GatewayAPI/ocelot.json /tmp/ocelot.json
RUN if [ "$PROJECT" = "GatewayAPI" ]; then cp /tmp/ocelot.json /app/publish/ocelot.json; fi \
    && rm /tmp/ocelot.json

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
ARG PROJECT
WORKDIR /app
COPY --from=build /app/publish .
ENV APP_DLL="${PROJECT}.dll"
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["sh", "-c", "dotnet \"$APP_DLL\""]
