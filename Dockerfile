# build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy solution and project files
COPY *.sln ./
COPY ExcelLinkExtractorWeb/*.csproj ./ExcelLinkExtractorWeb/
COPY ExcelLinkExtractor.Tests/*.csproj ./ExcelLinkExtractor.Tests/
COPY ExcelLinkExtractorWeb.E2ETests/*.csproj ./ExcelLinkExtractorWeb.E2ETests/

# Restore dependencies
RUN dotnet restore

# Copy everything else and build
COPY . .
RUN dotnet publish ExcelLinkExtractorWeb/ExcelLinkExtractorWeb.csproj -c Release -o /app/publish

# runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 5050
ENV ASPNETCORE_URLS=http://+:5050
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "ExcelLinkExtractorWeb.dll"]
