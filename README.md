# 💰 CunaPay API

API REST para gestión de wallets de criptomonedas (TRX/USDT) en la red Tron, con funcionalidades de staking, compras y retiros.

## 🚀 Características

- ✅ Gestión de usuarios y autenticación JWT
- ✅ Wallets de Tron (TRX y USDT)
- ✅ Sistema de staking con recompensas
- ✅ Compras y retiros de USDT
- ✅ Integración con microservicio de Tron
- ✅ Roles de Usuario y Admin
- ✅ Rate limiting e idempotencia
- ✅ Monitoreo de transacciones en background

## 📋 Requisitos

- .NET 8.0 SDK
- MongoDB (local o Atlas)
- Microservicio de Tron desplegado

## 🛠️ Instalación Local

1. Clona el repositorio:
```bash
git clone https://github.com/TU_USUARIO/CunaPay.Api.git
cd CunaPay.Api
```

2. Configura las variables de entorno (copia `env.example` a `.env` o configura en `appsettings.json`)

3. Restaura las dependencias:
```bash
dotnet restore
```

4. Ejecuta la aplicación:
```bash
dotnet run
```

La API estará disponible en `http://localhost:4000`

## 🐳 Docker

### Construir la imagen:
```bash
docker build -t cunapay-api .
```

### Ejecutar el contenedor:
```bash
docker run -p 4000:4000 --env-file .env cunapay-api
```

## 📚 Documentación

- [Guía de Despliegue en Render](./DEPLOY.md) - Instrucciones paso a paso para desplegar en Render
- [Postman Collections](./docs/) - Colecciones de Postman para probar la API

## 🔧 Configuración

Las variables de entorno principales son:

- `Mongo__Uri`: Cadena de conexión a MongoDB
- `Jwt__Secret`: Secret para JWT (mínimo 32 caracteres)
- `Crypto__MasterKeyHex`: Clave para encriptar claves privadas (64 caracteres hex)
- `Tron__ApiUrl`: URL del microservicio de Tron

Ver `env.example` para todas las variables.

## 📝 Endpoints Principales

### Autenticación
- `POST /auth/register` - Registrar usuario
- `POST /auth/login` - Iniciar sesión
- `POST /auth/change-password` - Cambiar contraseña

### Usuario
- `GET /api/me` - Información del usuario
- `GET /api/me/wallet` - Wallet del usuario
- `GET /api/me/balance` - Balance (TRX, USDT)
- `POST /api/me/send` - Enviar USDT
- `GET /api/me/transactions` - Listar transacciones

### Staking
- `POST /api/me/stakes` - Crear stake
- `GET /api/me/stakes` - Listar stakes
- `POST /api/me/stakes/{id}/close` - Cerrar stake

### Admin
- `GET /api/admin/users` - Listar usuarios
- `GET /api/admin/withdrawals` - Listar retiros
- `POST /api/admin/withdrawals/{id}/approve` - Aprobar retiro

## 🚀 Despliegue

Para desplegar en Render, sigue la [Guía de Despliegue](./DEPLOY.md).

## 📄 Licencia

Este proyecto es privado.

