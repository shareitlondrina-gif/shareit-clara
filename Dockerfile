FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN apt-get update \
    && apt-get install -y --no-install-recommends zip \
    && rm -rf /var/lib/apt/lists/*

RUN dotnet restore Clara.sln
RUN test -f /src/Ferramentas/Instalacao/INSTALAR-AGENTE.cmd \
    && test -f /src/Ferramentas/Instalacao/Instalar-Agente.ps1 \
    && test -f /src/Ferramentas/Diagnostico/DIAGNOSTICAR-AGENTE.cmd \
    && test -f /src/Ferramentas/Diagnostico/Diagnosticar-Agente.ps1

RUN VERSION=$(cat VERSAO.txt | tr -d '\r\n') \
    && dotnet publish src/Clara.Agente/Clara.Agente.csproj -c Release -r win-x64 --self-contained true -o /out/pacote/Agente -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:Version=$VERSION \
    && dotnet publish src/Clara.Sessao/Clara.Sessao.csproj -c Release -r win-x64 --self-contained true -o /out/pacote/Sessao -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:Version=$VERSION \
    && dotnet publish src/Clara.Atualizador/Clara.Atualizador.csproj -c Release -r win-x64 --self-contained true -o /out/pacote/Atualizador -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:Version=$VERSION \
    && dotnet publish src/Clara.NavegadorHost/Clara.NavegadorHost.csproj -c Release -r win-x64 --self-contained true -o /out/pacote/NavegadorHost -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:Version=$VERSION \
    && mkdir -p /out/pacote/NavegadorExtensao \
    && cp -R src/Clara.NavegadorExtensao/. /out/pacote/NavegadorExtensao/ \
    && dotnet publish src/Clara.Web/Clara.Web.csproj -c Release -o /out/web -p:Version=$VERSION \
    && cd /out/pacote \
    && zip -qr /out/Clara-Agent-v${VERSION}.zip . \
    && SHA=$(sha256sum /out/Clara-Agent-v${VERSION}.zip | awk '{print $1}') \
    && mkdir -p /out/web/atualizacoes/agente/${VERSION} \
    && cp /out/Clara-Agent-v${VERSION}.zip /out/web/atualizacoes/agente/${VERSION}/ \
    && mkdir -p /out/web/atualizacoes/agente \
    && printf '{\n  "versao": "%s",\n  "urlPacote": "/api/v1/agentes/atualizacao/pacote/%s",\n  "sha256": "%s",\n  "obrigatoria": false,\n  "publicadaEmUtc": "%s"\n}\n' "$VERSION" "$VERSION" "$SHA" "$(date -u +%Y-%m-%dT%H:%M:%SZ)" > /out/web/atualizacoes/agente/manifesto.json \
    && mkdir -p /out/instalador/Pacote \
    && cp -R /out/pacote/. /out/instalador/Pacote/ \
    && cp /src/Ferramentas/Instalacao/INSTALAR-AGENTE.cmd /src/Ferramentas/Instalacao/Instalar-Agente.ps1 /out/instalador/ \
    && cp /src/Ferramentas/Diagnostico/DIAGNOSTICAR-AGENTE.cmd /src/Ferramentas/Diagnostico/Diagnosticar-Agente.ps1 /out/instalador/ \
    && cd /out/instalador \
    && zip -qr /out/Clara-Instalador-v${VERSION}.zip . \
    && mkdir -p /out/web/instaladores/agente \
    && cp /out/Clara-Instalador-v${VERSION}.zip /out/web/instaladores/agente/

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
RUN apt-get update \
    && apt-get install -y --no-install-recommends libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /out/web .
ENV ASPNETCORE_URLS=http://0.0.0.0:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Clara.Web.dll"]
