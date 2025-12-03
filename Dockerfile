# Usar la imagen base de .NET 8.0 SDK para compilar
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copiar archivos del proyecto
COPY ["CunaPay.Api.csproj", "./"]
RUN dotnet restore "CunaPay.Api.csproj"

# Copiar el resto del código
COPY . .
WORKDIR "/src"
RUN dotnet build "CunaPay.Api.csproj" -c Release -o /app/build

# Publicar la aplicación
FROM build AS publish
RUN dotnet publish "CunaPay.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Imagen final de runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Copiar los archivos publicados
COPY --from=publish /app/publish .

# Exponer el puerto 4000 (o el que configures en Render)
EXPOSE 4000

# Establecer la variable de entorno para el puerto
ENV ASPNETCORE_URLS=http://+:4000

# Ejecutar la aplicación
ENTRYPOINT ["dotnet", "CunaPay.Api.dll"]

