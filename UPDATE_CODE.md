# 🔄 Cómo Actualizar el Código en Render

Cuando haces cambios en tu código, Render los despliega automáticamente si tienes **Auto-Deploy** habilitado (está activado por defecto).

---

## ⚡ Proceso Rápido (3 pasos)

### 1. Agregar los cambios a Git

```bash
git add .
```

O si solo quieres agregar un archivo específico:
```bash
git add nombre-del-archivo.cs
```

### 2. Hacer commit con un mensaje descriptivo

```bash
git commit -m "Descripción de tu cambio"
```

Ejemplos:
```bash
git commit -m "Fix: Corregir validación de email"
git commit -m "Feat: Agregar nuevo endpoint de estadísticas"
git commit -m "Update: Cambiar tasa de staking"
```

### 3. Subir a GitHub

```bash
git push origin main
```

**¡Eso es todo!** Render detectará automáticamente el cambio y comenzará a reconstruir y redesplegar tu aplicación.

---

## 📊 Monitorear el Despliegue

1. Ve a tu dashboard de Render: https://dashboard.render.com
2. Selecciona tu servicio `cunapay-api`
3. Ve a la pestaña **"Events"** o **"Logs"**
4. Verás el proceso de build en tiempo real

El proceso tarda aproximadamente:
- **Build**: 3-5 minutos
- **Deploy**: 1-2 minutos
- **Total**: ~5-7 minutos

---

## ✅ Verificar que se Actualizó

Una vez que el deploy termine (verás "Live" en verde), prueba tu API:

```bash
curl https://tu-url.onrender.com/
```

O prueba el endpoint que modificaste.

---

## 🔧 Si Auto-Deploy NO está Habilitado

Si por alguna razón el auto-deploy está deshabilitado:

1. Ve a tu servicio en Render
2. Pestaña **"Settings"**
3. Sección **"Build & Deploy"**
4. Asegúrate de que **"Auto-Deploy"** esté en **"Yes"**
5. Si estaba deshabilitado, habilítalo y haz clic en **"Save Changes"**

O puedes hacer deploy manual:
1. En la pestaña **"Manual Deploy"**
2. Haz clic en **"Deploy latest commit"**

---

## 🐛 Si el Deploy Falla

1. Ve a la pestaña **"Logs"** en Render
2. Revisa los errores (generalmente son claros)
3. Corrige el error en tu código
4. Repite los pasos 1-3 de arriba

Errores comunes:
- **Build failed**: Error de compilación, revisa la sintaxis
- **Docker error**: Problema con el Dockerfile
- **Dependency error**: Falta algún paquete NuGet

---

## 💡 Tips

- **Commits pequeños**: Es mejor hacer commits frecuentes con cambios pequeños
- **Mensajes claros**: Describe bien qué cambiaste en el commit
- **Probar localmente**: Antes de hacer push, prueba que compile: `dotnet build`
- **Revisar logs**: Siempre revisa los logs si algo falla

---

## 📝 Ejemplo Completo

```bash
# 1. Ver qué archivos cambiaron
git status

# 2. Agregar los cambios
git add .

# 3. Hacer commit
git commit -m "Fix: Corregir cálculo de recompensas en staking"

# 4. Subir a GitHub
git push origin main

# 5. Esperar 5-7 minutos y verificar en Render
```

---

¡Listo! 🎉 Tu código se actualizará automáticamente en Render.

