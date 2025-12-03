# ⚡ Inicio Rápido - Despliegue en Render

## 🎯 Resumen de Pasos

1. **MongoDB Atlas** → Crear cluster y obtener cadena de conexión
2. **GitHub** → Subir código
3. **Render** → Crear servicio y configurar variables
4. **¡Listo!** → Tu API estará funcionando

---

## 📝 Checklist Rápido

### ✅ Paso 1: MongoDB Atlas (10 minutos)

- [ ] Crear cuenta en https://www.mongodb.com/cloud/atlas
- [ ] Crear cluster M0 (gratis)
- [ ] Crear usuario de base de datos
- [ ] Permitir acceso desde cualquier IP (0.0.0.0/0)
- [ ] Obtener cadena de conexión
- [ ] **Guardar**: `mongodb+srv://usuario:password@cluster...`

### ✅ Paso 2: Generar Claves (2 minutos)

Ejecuta en PowerShell:
```powershell
.\generate-keys.ps1
```

Copia y guarda:
- `Jwt__Secret`
- `Crypto__MasterKeyHex`
- `Tron__CustodyPrivateKey`

### ✅ Paso 3: GitHub (5 minutos)

```bash
git add .
git commit -m "Preparado para despliegue"
git push origin main
```

### ✅ Paso 4: Render (15 minutos)

1. Ve a https://dashboard.render.com
2. **New +** → **Web Service**
3. Conecta GitHub y selecciona el repositorio
4. Configuración:
   - Name: `cunapay-api`
   - Environment: `Docker`
   - Branch: `main`
5. **Environment Variables** → Agrega todas las variables (ver abajo)
6. **Create Web Service**

---

## 🔑 Variables de Entorno para Render

Copia y pega estas variables en Render (reemplaza los valores):

```bash
ASPNETCORE_ENVIRONMENT=Production
PORT=4000

# MongoDB (reemplaza con tu cadena de conexión)
Mongo__Uri=mongodb+srv://usuario:password@cluster0.xxxxx.mongodb.net/cunapay?retryWrites=true&w=majority
Mongo__DbName=cunapay

# JWT (usa el generado por generate-keys.ps1)
Jwt__Secret=TU_JWT_SECRET_AQUI
Jwt__ExpiresIn=24h

# Crypto (usa el generado por generate-keys.ps1)
Crypto__MasterKeyHex=TU_CRYPTO_KEY_AQUI

# Tron (tu microservicio en Hostinger)
Tron__ApiUrl=https://tu-dominio-hostinger.com
Tron__AccessToken=@TokeAccessCun4P4y123654!
Tron__FullNode=https://api.nileex.io
Tron__SolidityNode=https://api.nileex.io
Tron__UsdtContract=TXYZopYRdj2D9XRtbG411XZZ3kM5VkAeBf
Tron__CustodyPrivateKey=TU_TRON_KEY_AQUI
Tron__TronGridBase=https://nile.trongrid.io

# Workers
Workers__TxWatcherIntervalMs=8000

# Staking
Staking__DefaultDailyRateBp=10
Staking__MinAmountUsdt=10
Staking__MaxAmountUsdt=10000
```

---

## 🧪 Probar que Funciona

Una vez desplegado, prueba:

```bash
curl https://tu-url.onrender.com/
```

Deberías ver:
```json
{
  "ok": true,
  "service": "cunapay",
  "env": "Production"
}
```

---

## 🆘 Problemas Comunes

### ❌ Build falla
- Verifica que el Dockerfile esté en la raíz
- Revisa los logs en Render

### ❌ No conecta a MongoDB
- Verifica la cadena de conexión
- Asegúrate de que la IP esté permitida en Atlas

### ❌ Puerto no funciona
- Render asigna el puerto automáticamente
- La variable `PORT` debe estar configurada

---

## 📚 Documentación Completa

Para más detalles, ve a [DEPLOY.md](./DEPLOY.md)

---

¡Éxito! 🎉

