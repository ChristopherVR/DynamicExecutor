#See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:7.0 AS base
WORKDIR /src
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src
COPY ["DynamicModule/DynamicModule.csproj", "DynamicModule/"]
COPY ["DynamicModule.UnitTests/DynamicModule.UnitTests.csproj", "DynamicModule.UnitTests/"]
RUN dotnet restore "DynamicModule.UnitTests/DynamicModule.UnitTests.csproj"
COPY . .
WORKDIR "/src/DynamicModule.UnitTests"
RUN dotnet build "DynamicModule.UnitTests.csproj" -c Release -o /src/build

FROM build AS publish
RUN dotnet publish "DynamicModule.UnitTests.csproj" -c Release -o /src/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /src
COPY --from=publish /src/publish .
ENTRYPOINT ["dotnet", "DynamicModule.UnitTests.dll"]