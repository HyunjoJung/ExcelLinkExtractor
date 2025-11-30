# build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project file and restore
COPY ExcelLinkExtractorWeb/*.csproj ./ExcelLinkExtractorWeb/
RUN dotnet restore ./ExcelLinkExtractorWeb/ExcelLinkExtractorWeb.csproj

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
