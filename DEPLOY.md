# 🚀 Guía de Despliegue - CunaPay API en Render

Esta guía te llevará paso a paso para desplegar tu API en Render, desde cero.

---

## 📋 Requisitos Previos

- ✅ Cuenta en GitHub
- ✅ Cuenta en Render (gratis en https://render.com)
- ✅ Cuenta en MongoDB Atlas (gratis en https://www.mongodb.com/cloud/atlas)
- ✅ Git instalado en tu computadora
- ✅ Docker instalado (opcional, para probar localmente)

---

## 📝 Paso 1: Configurar MongoDB Atlas

### 1.1 Crear cuenta en MongoDB Atlas

1. Ve a https://www.mongodb.com/cloud/atlas/register
2. Crea una cuenta gratuita (M0 - Free tier)
3. Completa el registro

### 1.2 Crear un Cluster

1. Una vez dentro, haz clic en **"Build a Database"**
2. Selecciona el plan **FREE (M0)**
3. Elige una región cercana a ti (ej: `us-east-1`)
4. Nombra tu cluster (ej: `CunaPay-Cluster`)
5. Haz clic en **"Create"** (puede tardar 3-5 minutos)

### 1.3 Configurar Acceso a la Base de Datos

1. En el menú lateral, ve a **"Database Access"**
2. Haz clic en **"Add New Database User"**
3. Elige **"Password"** como método de autenticación
4. Usuario: `cunapay-admin` (o el que prefieras)
5. Contraseña: Genera una contraseña segura (guárdala bien)
6. En "Database User Privileges", selecciona **"Read and write to any database"**
7. Haz clic en **"Add User"**

### 1.4 Configurar Acceso de Red

1. En el menú lateral, ve a **"Network Access"**
2. Haz clic en **"Add IP Address"**
3. Para desarrollo, haz clic en **"Allow Access from Anywhere"** (0.0.0.0/0)
   - ⚠️ En producción, deberías restringir esto a las IPs de Render
4. Haz clic en **"Confirm"**

### 1.5 Obtener la Cadena de Conexión

1. En el menú lateral, ve a **"Database"**
2. Haz clic en **"Connect"** en tu cluster
3. Selecciona **"Connect your application"**
4. Elige **".NET"** como driver
5. Copia la cadena de conexión, se verá así:
   ```
   mongodb+srv://<username>:<password>@cluster0.xxxxx.mongodb.net/?retryWrites=true&w=majority
   ```
6. **Reemplaza** `<username>` y `<password>` con tus credenciales:
   ```
   mongodb+srv://cunapay-admin:TU_PASSWORD@cluster0.xxxxx.mongodb.net/cunapay?retryWrites=true&w=majority
   ```
7. **Guarda esta cadena**, la necesitarás en Render

---

## 📝 Paso 2: Subir el Código a GitHub

### 2.1 Inicializar Git (si no lo has hecho)

Abre una terminal en la carpeta del proyecto y ejecuta:

```bash
# Verificar si ya es un repositorio Git
git status

# Si no es un repositorio, inicialízalo
git init
```

### 2.2 Crear Repositorio en GitHub

1. Ve a https://github.com/new
2. Nombre del repositorio: `CunaPay.Api` (o el que prefieras)
3. Descripción: "API de CunaPay para gestión de wallets"
4. Elige **Público** o **Privado**
5. **NO** marques "Initialize with README" (ya tienes archivos)
6. Haz clic en **"Create repository"**

### 2.3 Subir el Código

En la terminal, ejecuta estos comandos (reemplaza `TU_USUARIO` con tu usuario de GitHub):

```bash
# Agregar todos los archivos
git add .

# Hacer el primer commit
git commit -m "Initial commit: API lista para despliegue"

# Agregar el repositorio remoto (reemplaza TU_USUARIO)
git remote add origin https://github.com/TU_USUARIO/CunaPay.Api.git

# Subir el código
git branch -M main
git push -u origin main
```

Si te pide autenticación, usa un **Personal Access Token** de GitHub.

---

## 📝 Paso 3: Configurar Variables de Entorno en Render

### 3.1 Crear Servicio en Render

1. Ve a https://dashboard.render.com
2. Haz clic en **"New +"** → **"Web Service"**
3. Conecta tu cuenta de GitHub si no lo has hecho
4. Selecciona el repositorio `CunaPay.Api`
5. Configuración:
   - **Name**: `cunapay-api` (o el que prefieras)
   - **Environment**: `Docker`
   - **Region**: Elige la más cercana
   - **Branch**: `main`
   - **Root Directory**: (déjalo vacío)
   - **Docker Command**: (déjalo vacío, Render lo detectará automáticamente)
   - **Dockerfile Path**: `Dockerfile` (si no está en la raíz)

### 3.2 Configurar Variables de Entorno

Antes de hacer clic en "Create Web Service", ve a la sección **"Environment Variables"** y agrega estas variables:

#### Variables Obligatorias:

```
ASPNETCORE_ENVIRONMENT=Production
PORT=4000
```

#### Variables de MongoDB:

```
Mongo__Uri=mongodb+srv://cunapay-admin:TU_PASSWORD@cluster0.xxxxx.mongodb.net/cunapay?retryWrites=true&w=majority
Mongo__DbName=cunapay
```

#### Variables de JWT:

```
Jwt__Secret=TU_SECRET_JWT_MUY_SEGURO_MINIMO_32_CARACTERES_123456789
Jwt__ExpiresIn=24h
```

#### Variables de Crypto (para encriptar claves privadas):

```
Crypto__MasterKeyHex=TU_CLAVE_HEX_DE_64_CARACTERES_0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF
```

#### Variables de Tron (tu microservicio en Hostinger):

```
Tron__ApiUrl=https://tu-dominio-hostinger.com
Tron__AccessToken=@TokeAccessCun4P4y123654!
Tron__FullNode=https://api.nileex.io
Tron__SolidityNode=https://api.nileex.io
Tron__UsdtContract=TXYZopYRdj2D9XRtbG411XZZ3kM5VkAeBf
Tron__CustodyPrivateKey=TU_PRIVATE_KEY_DE_64_CARACTERES_HEX
Tron__TronGridBase=https://nile.trongrid.io
Tron__TronGridApiKey=tu-api-key-de-trongrid-si-la-tienes
```

#### Variables de Workers:

```
Workers__TxWatcherIntervalMs=8000
```

#### Variables de Staking:

```
Staking__DefaultDailyRateBp=10
Staking__MinAmountUsdt=10
Staking__MaxAmountUsdt=10000
```

### 3.3 Generar Valores Seguros

#### Para `Jwt__Secret`:
Usa un generador de contraseñas o ejecuta en PowerShell:
```powershell
-join ((65..90) + (97..122) + (48..57) | Get-Random -Count 32 | ForEach-Object {[char]$_})
```

#### Para `Crypto__MasterKeyHex`:
Genera una clave hexadecimal de 64 caracteres:
```powershell
-join ((48..57) + (65..70) | Get-Random -Count 64 | ForEach-Object {[char]$_})
```

### 3.4 Crear el Servicio

1. Revisa que todas las variables estén configuradas
2. Haz clic en **"Create Web Service"**
3. Render comenzará a construir y desplegar tu aplicación

---

## 📝 Paso 4: Verificar el Despliegue

### 4.1 Monitorear el Build

1. En Render, verás el log del build
2. Espera a que termine (puede tardar 5-10 minutos la primera vez)
3. Si hay errores, revísalos en los logs

### 4.2 Verificar que Funciona

1. Una vez desplegado, Render te dará una URL como: `https://cunapay-api.onrender.com`
2. Prueba el endpoint de health check:
   ```
   https://tu-url.onrender.com/
   ```
3. Deberías ver:
   ```json
   {
     "ok": true,
     "service": "cunapay",
     "env": "Production"
   }
   ```

### 4.3 Probar Endpoints

Usa Postman o curl para probar:

```bash
# Health check
curl https://tu-url.onrender.com/

# Registrar usuario (si tienes el endpoint)
curl -X POST https://tu-url.onrender.com/auth/register \
  -H "Content-Type: application/json" \
  -d '{"email":"test@test.com","password":"test123","firstName":"Test","lastName":"User"}'
```

---

## 🔧 Paso 5: Configuración Adicional

### 5.1 Configurar Dominio Personalizado (Opcional)

1. En Render, ve a tu servicio
2. Ve a la pestaña **"Settings"**
3. En **"Custom Domain"**, agrega tu dominio
4. Sigue las instrucciones para configurar DNS

### 5.2 Configurar Auto-Deploy

Por defecto, Render despliega automáticamente cuando haces push a `main`. Puedes configurarlo en:
- Settings → Build & Deploy → Auto-Deploy

### 5.3 Ver Logs

1. En Render, ve a tu servicio
2. Pestaña **"Logs"** para ver logs en tiempo real
3. Útil para debugging

---

## 🐛 Solución de Problemas

### Error: "Build failed"

- Revisa los logs en Render
- Verifica que el Dockerfile esté correcto
- Asegúrate de que todas las dependencias estén en `.csproj`

### Error: "Cannot connect to MongoDB"

- Verifica que la cadena de conexión esté correcta
- Asegúrate de que la IP de Render esté permitida en MongoDB Atlas
- Revisa que el usuario y contraseña sean correctos

### Error: "Port already in use"

- Render asigna el puerto automáticamente
- Asegúrate de que `PORT` esté en las variables de entorno
- El código debe usar `Environment.GetEnvironmentVariable("PORT")` o la configuración de Render

### La aplicación se cae después de unos minutos

- Render suspende servicios gratuitos después de 15 minutos de inactividad
- Considera usar un servicio de "ping" para mantenerlo activo
- O actualiza a un plan de pago

---

## 📚 Recursos Adicionales

- [Documentación de Render](https://render.com/docs)
- [MongoDB Atlas Documentation](https://docs.atlas.mongodb.com/)
- [.NET Docker Documentation](https://docs.microsoft.com/en-us/dotnet/core/docker/)

---

## ✅ Checklist Final

- [ ] MongoDB Atlas configurado y funcionando
- [ ] Código subido a GitHub
- [ ] Servicio creado en Render
- [ ] Todas las variables de entorno configuradas
- [ ] Build exitoso en Render
- [ ] Health check responde correctamente
- [ ] Endpoints funcionando

---

¡Felicitaciones! 🎉 Tu API debería estar desplegada y funcionando en Render.

