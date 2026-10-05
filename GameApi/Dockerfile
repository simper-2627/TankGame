FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish GameFrontend -c Release -o /frontend
RUN dotnet publish GameApi -c Release -o /app
RUN cp -r /frontend/wwwroot /app/wwwroot

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "GameApi.dll"]
