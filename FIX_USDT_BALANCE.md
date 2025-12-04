# 🔧 Solución: Error al Obtener Balance USDT

## 🐛 Problema Identificado

El microservicio de Tron está devolviendo un error 500 cuando se intenta obtener el balance USDT:

```json
{
  "ok": false,
  "error": "Error al obtener balance USDT: undefined"
}
```

El error "undefined" indica que **falta alguna configuración en el microservicio de Tron**.

---

## ✅ Solución

### 1. Verificar Configuración del Contrato USDT

El microservicio de Tron necesita tener configurado el **contrato USDT** para poder obtener balances.

**En el microservicio de Tron, verifica:**

1. **Variable de entorno o configuración:**
   ```
   USDT_CONTRACT=TXYZopYRdj2D9XRtbG411XZZ3kM5VkAeBf
   ```
   (Este es el contrato de USDT en la red de prueba Nile)

2. **Para Mainnet, el contrato es:**
   ```
   USDT_CONTRACT=TR7NHqjeKQxGTCi8q8ZY4pL8otSzgjLj6t
   ```

### 2. Verificar que el Endpoint Exista

Asegúrate de que el endpoint `/wallet/usdt/{address}` esté implementado correctamente en el microservicio de Tron.

**Prueba directamente en Postman:**
```
GET https://tu-tron-api.onrender.com/wallet/usdt/TRZbnro7WsNQtJ3B7tGjutpTsSwkx4Wd5i
```

**Debería devolver:**
```json
{
  "ok": true,
  "balance": 100.5
}
```

O:
```json
{
  "balance": 100.5
}
```

### 3. Verificar Logs del Microservicio de Tron

En Render, ve a los logs del microservicio de Tron y busca:
- Errores relacionados con "undefined"
- Errores relacionados con el contrato USDT
- Errores de configuración

---

## 🔍 Diagnóstico

### Si funciona en Postman pero no desde CunaPay:

1. **Verifica el header de autenticación:**
   - CunaPay envía: `x-api-key: @TokeAccessCun4P4y123654!`
   - Asegúrate de que el microservicio acepte este header

2. **Verifica la URL:**
   - CunaPay llama: `https://tron-api.onrender.com/wallet/usdt/{address}`
   - Debe coincidir exactamente con la ruta del microservicio

### Si no funciona ni en Postman:

El problema está en el microservicio de Tron. Revisa:

1. **Configuración del contrato USDT:**
   ```javascript
   // Ejemplo en Node.js
   const USDT_CONTRACT = process.env.USDT_CONTRACT || 'TXYZopYRdj2D9XRtbG411XZZ3kM5VkAeBf';
   ```

2. **Implementación del endpoint:**
   ```javascript
   // El endpoint debe obtener el balance del contrato TRC20
   app.get('/wallet/usdt/:address', async (req, res) => {
     const { address } = req.params;
     try {
       const balance = await getUsdtBalance(address, USDT_CONTRACT);
       res.json({ ok: true, balance });
     } catch (error) {
       res.status(500).json({ ok: false, error: error.message });
     }
   });
   ```

---

## 📝 Checklist para el Microservicio de Tron

- [ ] Variable `USDT_CONTRACT` configurada en Render
- [ ] El contrato USDT es correcto para la red (Nile testnet o Mainnet)
- [ ] El endpoint `/wallet/usdt/{address}` está implementado
- [ ] El endpoint maneja errores correctamente
- [ ] Los logs del microservicio no muestran errores de "undefined"

---

## 🚀 Próximos Pasos

1. **Revisa el código del microservicio de Tron** donde se obtiene el balance USDT
2. **Verifica que el contrato USDT esté configurado**
3. **Prueba el endpoint directamente en Postman**
4. **Revisa los logs del microservicio en Render**

Una vez que el microservicio esté funcionando correctamente, CunaPay debería poder obtener los balances USDT sin problemas.

---

## 💡 Nota

El código de CunaPay ya está preparado para manejar estos errores y retornar `0` cuando hay un error, pero el problema real está en el microservicio de Tron que necesita ser corregido.

