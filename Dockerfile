FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["DGVisionStudio.Domain/DGVisionStudio.Domain.csproj", "DGVisionStudio.Domain/"]
COPY ["DGVisionStudio.Infrastructure/DGVisionStudio.Infrastructure.csproj", "DGVisionStudio.Infrastructure/"]
COPY ["DGVisionStudio.Api/DGVisionStudio.Api.csproj", "DGVisionStudio.Api/"]
RUN dotnet restore "DGVisionStudio.Api/DGVisionStudio.Api.csproj"
COPY . .
WORKDIR "/src/DGVisionStudio.Api"
RUN dotnet publish "DGVisionStudio.Api.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:10000
EXPOSE 10000
ENTRYPOINT ["dotnet", "DGVisionStudio.Api.dll"]
