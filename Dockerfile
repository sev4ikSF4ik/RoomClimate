FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/RoomClimate.Api -c Release -o /out/api \
 && dotnet publish src/RoomClimate.Simulator -c Release -o /out/sim

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS api
WORKDIR /app
COPY --from=build /out/api .
ENV Database__Path=/data/roomclimate.db
VOLUME /data
EXPOSE 5080
ENTRYPOINT ["dotnet", "RoomClimate.Api.dll"]

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS simulator
WORKDIR /app
COPY --from=build /out/sim .
ENTRYPOINT ["dotnet", "RoomClimate.Simulator.dll"]
