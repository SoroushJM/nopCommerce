FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble
RUN apt-get update && apt-get install -y --no-install-recommends libfontconfig1 libfreetype6 libgdiplus curl && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY app/ ./
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Nop.Web.dll"]
