FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN apt-get update \
    && apt-get install -y --no-install-recommends zip \
    && rm -rf /var/lib/apt/lists/*

RUN dotnet restore ShareIT.Clara.sln
RUN VERSION=$(cat VERSAO.txt | tr -d '\r\n') \
    && dotnet publish src/ShareIT.Clara.Agente/ShareIT.Clara.Agente.csproj -c Release -r win-x64 --self-contained true -o /out/pacote/Agente -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:Version=$VERSION \
    && dotnet publish src/ShareIT.Clara.Sessao/ShareIT.Clara.Sessao.csproj -c Release -r win-x64 --self-contained true -o /out/pacote/Sessao -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:Version=$VERSION \
    && dotnet publish src/ShareIT.Clara.Atualizador/ShareIT.Clara.Atualizador.csproj -c Release -r win-x64 --self-contained true -o /out/pacote/Atualizador -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:Version=$VERSION \
    && dotnet publish src/ShareIT.Clara.Web/ShareIT.Clara.Web.csproj -c Release -o /out/web -p:Version=$VERSION \
    && cd /out/pacote \
    && zip -qr /out/Clara-Agent-v${VERSION}.zip . \
    && SHA=$(sha256sum /out/Clara-Agent-v${VERSION}.zip | awk '{print $1}') \
    && mkdir -p /out/web/atualizacoes/agente/${VERSION} \
    && cp /out/Clara-Agent-v${VERSION}.zip /out/web/atualizacoes/agente/${VERSION}/ \
    && mkdir -p /out/web/atualizacoes/agente \
    && printf '{\n  "versao": "%s",\n  "urlPacote": "/api/v1/agentes/atualizacao/pacote/%s",\n  "sha256": "%s",\n  "obrigatoria": false,\n  "publicadaEmUtc": "%s"\n}\n' "$VERSION" "$VERSION" "$SHA" "$(date -u +%Y-%m-%dT%H:%M:%SZ)" > /out/web/atualizacoes/agente/manifesto.json

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /out/web .
ENV ASPNETCORE_URLS=http://0.0.0.0:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "ShareIT.Clara.Web.dll"]
