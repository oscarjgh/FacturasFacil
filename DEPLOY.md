# FacturasFacil — Guía de Deploy

## Opción A — Railway (recomendado, gratis)

### 1. Crear cuenta en Railway
Ve a https://railway.app y regístrate con GitHub.

### 2. Subir el código a GitHub
```bash
git init
git add .
git commit -m "FacturasFacil v1.0"
git remote add origin https://github.com/TU_USUARIO/facturasfacil.git
git push -u origin main
```

### 3. Crear proyecto en Railway
- New Project → Deploy from GitHub Repo → selecciona `facturasfacil`
- Railway detecta el Dockerfile automáticamente

### 4. Configurar variables de entorno en Railway
En el panel del proyecto → Variables → agregar:

| Variable | Valor |
|----------|-------|
| `Jwt__Key` | Una cadena aleatoria de 40+ caracteres |
| `Jwt__Issuer` | `FacturasFacil` |
| `Jwt__Audience` | `FacturasFacilUsers` |
| `Stripe__SecretKey` | `sk_test_...` (de tu cuenta Stripe) |
| `Stripe__WebhookSecret` | `whsec_...` (de tu cuenta Stripe) |
| `Stripe__SuccessUrl` | `https://TU-APP.railway.app/pago-exitoso.html` |
| `Stripe__CancelUrl` | `https://TU-APP.railway.app/planes` |

Railway inyecta `PORT` automáticamente, el Dockerfile ya lo usa.

### 5. Volumen persistente (para la DB y los Excels)
- En Railway: tu servicio → Volumes → Add Volume → Mount Path: `/data`

### 6. ¡Listo!
Railway te da una URL tipo `https://facturasfacil-production.up.railway.app`

---

## Opción B — Render (también gratis)

1. Ve a https://render.com → New Web Service → conecta tu repo de GitHub
2. Runtime: **Docker**
3. Agrega las mismas variables de entorno del paso 4 de Railway
4. Disk (para persistencia): Add Disk → `/data` → 1 GB (gratis en tier Starter)

**Nota**: El plan gratuito de Render "duerme" el servicio tras 15 min de inactividad.
El primer request tardará ~30 seg en despertar.

---

## Opción C — ngrok (demo instantáneo, sin cambios)

Si solo quieres compartir una demo rápida sin subir a internet:

```bash
# 1. Corre la app localmente
dotnet run --project src/FacturasFacil.Api --launch-profile http

# 2. En otra terminal, expón con ngrok (descarga en https://ngrok.com)
ngrok http 5000
```

ngrok te da una URL pública temporal como `https://abc123.ngrok.io`.

---

## Configurar Stripe para cobros reales

1. Crea cuenta en https://stripe.com
2. En el dashboard → Products → crea 2 productos:
   - **Contador**: $199 MXN/mes → copia el `price_ID`
   - **Despacho**: $499 MXN/mes → copia el `price_ID`
3. En `appsettings.json` reemplaza:
   ```json
   "StripePriceId": "price_CONTADOR_ID"   → el ID real
   "StripePriceId": "price_DESPACHO_ID"   → el ID real
   ```
   O configura como variables de entorno en Railway.
4. Configura el Webhook en Stripe Dashboard:
   - URL: `https://TU-APP.railway.app/api/pagos/webhook`
   - Eventos: `checkout.session.completed`, `customer.subscription.deleted`

---

## Variables de entorno — referencia completa

```
ConnectionStrings__Default=Data Source=/data/facturasfacil.db
Historial__CarpetaBase=/data/excels
Jwt__Key=CADENA_SECRETA_40_CHARS_MINIMO
Jwt__Issuer=FacturasFacil
Jwt__Audience=FacturasFacilUsers
Jwt__ExpiresHours=24
Stripe__SecretKey=sk_live_...
Stripe__WebhookSecret=whsec_...
Stripe__SuccessUrl=https://tuapp.railway.app/pago-exitoso.html
Stripe__CancelUrl=https://tuapp.railway.app/planes
```
