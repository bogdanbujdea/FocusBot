FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/FocusBot.WebAPI/FocusBot.WebAPI.csproj src/FocusBot.WebAPI/
RUN dotnet restore src/FocusBot.WebAPI/FocusBot.WebAPI.csproj

COPY src/FocusBot.WebAPI/ src/FocusBot.WebAPI/
RUN dotnet publish src/FocusBot.WebAPI/FocusBot.WebAPI.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
EXPOSE 8080
ENTRYPOINT ["dotnet", "FocusBot.WebAPI.dll"]
