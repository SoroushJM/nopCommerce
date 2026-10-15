# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
RUN apk add --no-cache nodejs npm
WORKDIR /source
ARG RUNTIME_IDENTIFIER=linux-musl-x64
ARG STOREFRONT_THEME=Madadrang
RUN case "$STOREFRONT_THEME" in Madadrang|MishaShop|Boutique) ;; *) echo "STOREFRONT_THEME must be Madadrang or MishaShop" >&2; exit 1 ;; esac
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
# Restore metadata first: source edits do not invalidate the NuGet layer.
COPY global.json ./
COPY src/Directory.Build.props src/Directory.Build.targets src/Storefront.Build.props src/Docker.slnx src/
COPY src/Storefront/Storefront.Integration.targets src/Storefront/
COPY src/Libraries/Nop.Core/Nop.Core.csproj src/Libraries/Nop.Core/
COPY src/Libraries/Nop.Data/Nop.Data.csproj src/Libraries/Nop.Data/
COPY src/Libraries/Nop.Services/Nop.Services.csproj src/Libraries/Nop.Services/
COPY src/Presentation/Nop.Web.Framework/Nop.Web.Framework.csproj src/Presentation/Nop.Web.Framework/
COPY src/Presentation/Nop.Web/Nop.Web.csproj src/Presentation/Nop.Web/
COPY src/Presentation/Nop.Docker.Views/Nop.Docker.Views.csproj src/Presentation/Nop.Docker.Views/
COPY src/Storefront/Storefront.UI.Shared/Storefront.UI.Shared.csproj src/Storefront/Storefront.UI.Shared/
COPY src/Storefront/Storefront.UI.Madadrang/Storefront.UI.Madadrang.csproj src/Storefront/Storefront.UI.Madadrang/
COPY src/Storefront/Storefront.UI.MishaShop/Storefront.UI.MishaShop.csproj src/Storefront/Storefront.UI.MishaShop/
COPY src/Storefront/Storefront.Client/Storefront.Client.csproj src/Storefront/Storefront.Client/
COPY src/Plugins/Nop.Plugin.Misc.PersianStorefront/Nop.Plugin.Misc.PersianStorefront.csproj src/Plugins/Nop.Plugin.Misc.PersianStorefront/
COPY src/Plugins/Nop.Plugin.Payments.CheckMoneyOrder/Nop.Plugin.Payments.CheckMoneyOrder.csproj src/Plugins/Nop.Plugin.Payments.CheckMoneyOrder/
COPY src/Plugins/Nop.Plugin.Shipping.FixedByWeightByTotal/Nop.Plugin.Shipping.FixedByWeightByTotal.csproj src/Plugins/Nop.Plugin.Shipping.FixedByWeightByTotal/
COPY src/Plugins/Nop.Plugin.Tax.FixedOrByCountryStateZip/Nop.Plugin.Tax.FixedOrByCountryStateZip.csproj src/Plugins/Nop.Plugin.Tax.FixedOrByCountryStateZip/
COPY src/Plugins/Nop.Plugin.Widgets.Swiper/Nop.Plugin.Widgets.Swiper.csproj src/Plugins/Nop.Plugin.Widgets.Swiper/
COPY src/Build/src/ClearPluginAssemblies/ClearPluginAssemblies.csproj src/Build/src/ClearPluginAssemblies/
RUN --mount=type=cache,id=nop-nuget,target=/root/.nuget/packages,sharing=locked \
    dotnet restore src/Storefront/Storefront.Client/Storefront.Client.csproj -p:StorefrontTheme="$STOREFRONT_THEME" && \
    dotnet restore src/Docker.slnx -p:StorefrontTheme="$STOREFRONT_THEME" -p:DockerRuntimeIdentifier="$RUNTIME_IDENTIFIER" && \
    dotnet restore src/Build/src/ClearPluginAssemblies/ClearPluginAssemblies.csproj
COPY src/Storefront/Storefront.UI.Madadrang/package*.json src/Storefront/Storefront.UI.Madadrang/
COPY src/Storefront/Storefront.UI.MishaShop/package*.json src/Storefront/Storefront.UI.MishaShop/
RUN --mount=type=cache,id=nop-npm,target=/root/.npm,sharing=locked \
    theme="$STOREFRONT_THEME" && if [ "$theme" = Boutique ]; then theme=MishaShop; fi && cd "src/Storefront/Storefront.UI.$theme" && npm ci --no-audit --no-fund
COPY src/ src/
RUN --mount=type=cache,id=nop-nuget,target=/root/.nuget/packages,sharing=locked \
    dotnet build src/Build/src/ClearPluginAssemblies/ClearPluginAssemblies.csproj \
      -c Release --no-restore -o /tmp/clear-helper && \
    cp /tmp/clear-helper/ClearPluginAssemblies.* /source/src/Build/ && \
    dotnet build src/Docker.slnx -c Release --no-restore -p:StorefrontTheme="$STOREFRONT_THEME" -p:DockerRuntimeIdentifier="$RUNTIME_IDENTIFIER" -p:ShouldUnsetParentConfigurationAndPlatform=false -p:DebugType=None -p:DebugSymbols=false -p:UseSharedCompilation=false
RUN --mount=type=cache,id=nop-nuget,target=/root/.nuget/packages,sharing=locked \
    dotnet /source/src/Build/ClearPluginAssemblies.dll \
      "OutputPath=/source/src/Presentation/Nop.Web/bin/Release|PluginPath=/source/src/Presentation/Nop.Web/Plugins/Misc.PersianStorefront|SaveLocalesFolders=true" && \
    dotnet publish src/Presentation/Nop.Web/Nop.Web.csproj -c Release --no-build --no-restore -r "$RUNTIME_IDENTIFIER" --self-contained false \
      -p:StorefrontTheme="$STOREFRONT_THEME" -p:DockerRuntimeIdentifier="$RUNTIME_IDENTIFIER" -p:ShouldUnsetParentConfigurationAndPlatform=false -p:DebugType=None -p:DebugSymbols=false -p:UseSharedCompilation=false \
      -p:PreserveCompilationContext=false -p:PreserveCompilationReferences=false -o /app/published && \
    cp /source/src/Presentation/Nop.Docker.Views/bin/Release/net10.0/"$RUNTIME_IDENTIFIER"/Nop.Docker.Views.dll /app/published/ && \
    cp /source/src/Presentation/Nop.Docker.Views/bin/Release/net10.0/"$RUNTIME_IDENTIFIER"/PrecompiledViewPluginDependencies/*.dll /app/published/ && \
    cp /source/src/Presentation/Nop.Docker.Views/bin/Release/net10.0/"$RUNTIME_IDENTIFIER"/PrecompiledViewRuntimeDependencies/*.dll /app/published/ && \
    find /app/published -type f -name '*.pdb' -delete && \
    mkdir -p /app/published/logs /app/published/bin /app/published/App_Data/DataProtectionKeys
COPY docker/compact-publish.sh /tmp/compact-publish.sh
RUN sh /tmp/compact-publish.sh

# Small, one-shot certificate helper. Private keys never enter the app image.
FROM alpine:3.23 AS certificates
RUN apk add --no-cache openssl
COPY docker/create-certificate.sh /usr/local/bin/create-certificate
ENTRYPOINT ["sh", "/usr/local/bin/create-certificate"]

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime
RUN apk add --no-cache icu-libs icu-data-full tzdata gcompat libgdiplus tiff
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false \
    ASPNETCORE_HTTP_PORTS=8080 \
    CommonConfig__UsePrecompiledViews=true \
    DOTNET_GCServer=0 \
    DOTNET_GCHeapHardLimitPercent=0x1E \
    DOTNET_GCConserveMemory=9 \
    DOTNET_EnableDiagnostics=0
WORKDIR /app
COPY --from=build /app/published ./
EXPOSE 8080 8443
ENTRYPOINT ["dotnet", "Nop.Web.dll"]
