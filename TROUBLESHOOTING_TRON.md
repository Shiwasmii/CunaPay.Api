# 🔧 Solución de Problemas - Conexión con API de Tron

## 🐛 Problema: La API no se conecta al microservicio de Tron en Render

Si tu API de CunaPay está desplegada en Render pero no puede conectarse al microservicio de Tron (también en Render), sigue estos pasos:

---

## ✅ Verificaciones Paso a Paso

### 1. Verificar la URL del Microservicio de Tron

**En Render (Variables de Entorno):**
```
Tron__ApiUrl=https://tu-tron-api.onrender.com
```

**Importante:**
- ✅ Debe empezar con `https://` (no `http://`)
- ✅ No debe terminar con `/`
- ✅ Debe ser la URL completa de Render (ej: `https://tron-api-xxxx.onrender.com`)

**Probar la URL manualmente:**
```bash
curl https://tu-tron-api.onrender.com/
```

Deberías recibir una respuesta del microservicio.

---

### 2. Verificar el Access Token

**En Render (Variables de Entorno):**
```
Tron__AccessToken=@TokeAccessCun4P4y123654!
```

**Verificar en Postman:**
1. Abre Postman
2. Haz una petición a tu API de Tron
3. Agrega el header: `x-api-key: @TokeAccessCun4P4y123654!`
4. Si funciona en Postman pero no desde CunaPay, el problema es la configuración

---

### 3. Revisar los Logs en Render

**Pasos:**
1. Ve a tu servicio de CunaPay en Render
2. Pestaña **"Logs"**
3. Busca errores relacionados con Tron:
   - `HTTP error calling Tron API`
   - `Timeout calling Tron API`
   - `Failed to connect to Tron API`

**Errores comunes:**

#### Error: "HTTP error: Connection refused"
- **Causa**: La URL del microservicio es incorrecta o el servicio está caído
- **Solución**: Verifica que el microservicio de Tron esté "Live" en Render

#### Error: "Request timed out"
- **Causa**: El microservicio tarda mucho en responder
- **Solución**: 
  - Verifica que el microservicio de Tron esté funcionando
  - Puede ser que esté "dormido" (servicios gratuitos de Render se suspenden después de 15 min)

#### Error: "Unauthorized" o "401"
- **Causa**: El AccessToken es incorrecto
- **Solución**: Verifica que `Tron__AccessToken` coincida con el del microservicio

---

### 4. Verificar que Ambos Servicios Estén Activos

**En Render:**
1. Verifica que **ambos servicios** estén en estado **"Live"** (verde)
2. Si el microservicio de Tron está "Suspended", haz clic en "Manual Deploy" para reactivarlo

**Nota:** Los servicios gratuitos de Render se suspenden después de 15 minutos de inactividad.

---

### 5. Probar la Conexión Manualmente

**Desde tu computadora:**
```bash
# Probar el microservicio de Tron
curl https://tu-tron-api.onrender.com/wallet/create

# Deberías recibir una respuesta JSON con address y privateKey
```

**Si funciona desde tu PC pero no desde Render:**
- Puede ser un problema de red entre servicios de Render
- Verifica que no haya restricciones de firewall

---

### 6. Verificar Variables de Entorno en Render

**Asegúrate de que estas variables estén configuradas:**

```
Tron__ApiUrl=https://tu-tron-api.onrender.com
Tron__AccessToken=@TokeAccessCun4P4y123654!
```

**Importante:** 
- Usa doble guion bajo `__` para separar secciones (no un solo `_`)
- No dejes espacios antes o después del `=`

---

### 7. Verificar el Formato de la URL

**❌ Incorrecto:**
```
Tron__ApiUrl=https://tu-tron-api.onrender.com/
Tron__ApiUrl=http://tu-tron-api.onrender.com
Tron__ApiUrl=tu-tron-api.onrender.com
```

**✅ Correcto:**
```
Tron__ApiUrl=https://tu-tron-api.onrender.com
```

---

### 8. Probar con Logging Mejorado

Los cambios que hice agregan logging detallado. Después de hacer push, revisa los logs:

**Busca estos mensajes:**
```
TronService initialized. API URL: https://...
Calling Tron API: https://...
HTTP error calling Tron API: ...
```

Estos logs te dirán exactamente qué está fallando.

---

## 🔍 Debugging Avanzado

### Agregar un Endpoint de Prueba

Puedes agregar temporalmente un endpoint para probar la conexión:

```csharp
[HttpGet("test-tron")]
public async Task<IActionResult> TestTron()
{
    try
    {
        var (address, pk) = await _tronService.CreateWalletAsync();
        return Ok(new { ok = true, address, message = "Tron API is working!" });
    }
    catch (Exception ex)
    {
        return StatusCode(500, new { ok = false, error = ex.Message });
    }
}
```

Luego prueba:
```bash
curl https://tu-cunapay-api.onrender.com/api/test-tron
```

---

## 🚨 Problemas Comunes y Soluciones

### Problema: "Service Unavailable" o "503"

**Causa:** El microservicio de Tron está suspendido (servicios gratuitos)

**Solución:**
1. Ve al microservicio de Tron en Render
2. Haz clic en "Manual Deploy" → "Deploy latest commit"
3. Espera a que esté "Live"
4. Prueba de nuevo

---

### Problema: "Connection timeout"

**Causa:** El microservicio tarda mucho en responder

**Solución:**
1. Verifica que el microservicio de Tron esté funcionando
2. Prueba directamente en Postman
3. Si funciona en Postman, puede ser un problema de red entre servicios

---

### Problema: "Unauthorized" o "403"

**Causa:** El AccessToken no coincide

**Solución:**
1. Verifica que `Tron__AccessToken` en CunaPay sea igual al que espera el microservicio de Tron
2. Verifica que el header se esté enviando correctamente (debería ser `x-api-key`)

---

## 📝 Checklist de Verificación

- [ ] URL del microservicio de Tron es correcta (empieza con `https://`)
- [ ] AccessToken está configurado correctamente
- [ ] Ambos servicios están "Live" en Render
- [ ] El microservicio de Tron funciona cuando lo pruebas directamente
- [ ] Variables de entorno usan doble guion bajo `__`
- [ ] Revisaste los logs en Render para ver errores específicos

---

## 💡 Tips

1. **Mantén ambos servicios activos**: Si ambos están en el plan gratuito, considera usar un servicio de "ping" para mantenerlos despiertos

2. **Usa el mismo AccessToken**: Asegúrate de que el token en CunaPay sea exactamente el mismo que espera el microservicio de Tron

3. **Revisa los logs primero**: Los logs te dirán exactamente qué está fallando

4. **Prueba incrementalmente**: Prueba primero el endpoint más simple (como `CreateWallet`) antes de probar los más complejos

---

Si después de seguir estos pasos aún no funciona, comparte los logs específicos de Render y te ayudo a identificar el problema exacto.

