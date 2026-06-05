<div align="center">

<img src="Assets/logo.png" width="120" alt="SportsMax logo"/>

# SportsMax

**Agenda deportiva en vivo · reproductor multifuente para Windows**

[![Plataforma](https://img.shields.io/badge/plataforma-Windows%2010%2F11-0078D6)](#requisitos)
[![.NET](https://img.shields.io/badge/.NET-8.0%20WPF-512BD4)](#tecnologías)
[![Licencia](https://img.shields.io/badge/licencia-MIT-green)](LICENSE)

</div>

---

> [!IMPORTANT]
> **Descargo de responsabilidad.** SportsMax es un **agregador y reproductor**. **No aloja,
> no produce y no distribuye** ningún contenido audiovisual: únicamente muestra una agenda
> pública de eventos y abre enlaces de **terceros** que el usuario elige. La disponibilidad
> y la legalidad de ese contenido dependen de los proveedores externos y de la legislación
> de cada país. **El uso de los enlaces es responsabilidad exclusiva del usuario final.**
> Consulta [PRIVACY.md](PRIVACY.md) y [LICENSE](LICENSE).

---

## ✨ Características

- 📡 **Agenda multifuente** — combina y deduplica eventos de varias fuentes públicas de
  programación deportiva.
- 🕐 **Estados en tiempo real** — `EN VIVO` / `PRONTO` / `FIN` calculados según la **hora
  local** del equipo; los eventos terminados hace más de 2 h 30 min se ocultan solos.
- 🎬 **Doble motor de reproducción** — navegador embebido **WebView2** (para las páginas de
  los canales) y **VLC** (para flujos directos `.m3u8`), con conmutación automática.
- 🔘 **Canales como botones** — todas las opciones del evento visibles; cambia de canal con
  un clic.
- 🚫 **Bloqueador de anuncios y ventanas emergentes** integrado, con *bypass* de CORS para
  que los streams alojados en CDNs externos carguen sin errores.
- ⛶ **Pantalla completa real** (`F11` / `Esc`) con barra que se auto-oculta a los 4 s.
- 🖼 **Ventana flotante (PiP)** siempre visible sobre otras aplicaciones, arrastrable y
  redimensionable.
- 🎨 **Interfaz oscura** moderna con búsqueda y filtros por categoría/estado.

---

## 🖥️ Requisitos

| Componente | Detalle |
|------------|---------|
| **Sistema** | Windows 10 / 11 (64 bits) |
| **Microsoft Edge WebView2 Runtime** | Incluido en Windows 10/11 actualizados. Si falta, el instalador lo instala automáticamente. |
| **.NET** | No requiere instalación: el build publicado es *self-contained* (incluye el runtime). |

---

## 📥 Instalación (usuario final)

1. Descarga `SportsMax-Setup-1.0.0.exe` desde la sección [**Releases**](../../releases).
2. Ejecútalo y sigue el asistente **Siguiente → Siguiente → Instalar**.
3. Acepta el aviso legal y de privacidad para continuar.
4. Al finalizar, inicia **SportsMax** desde el menú Inicio o el acceso del escritorio.

> El instalador verifica e instala el componente WebView2 si tu equipo no lo tiene.

---

## 🚀 Uso

1. La agenda del día se carga al abrir (botón **↻ Actualizar** para recargar).
2. Busca o filtra por **categoría** y **estado**.
3. Haz clic en un evento → se muestran sus **canales** como botones y se posiciona en el
   primero.
4. Pulsa el botón de un canal para reproducir. Si uno falla, prueba otro.
5. Usa **⛶ Pantalla completa** o **🖼 Flotante** según prefieras.

Selector **Motor**: `Automático` (recomendado), `Navegador` (WebView2) o `VLC`.

---

## 🛠️ Compilar desde el código fuente

### Requisitos de desarrollo
- [.NET SDK 8.0](https://dotnet.microsoft.com/download) o superior
- Windows (WPF requiere Windows)

### Ejecutar en modo desarrollo
```bash
dotnet run -c Release
```

### Publicar (build self-contained, sin dependencia de .NET)
```bash
dotnet publish SportsMax.csproj -c Release -r win-x64 --self-contained true -o publish
```
El proyecto adelgaza automáticamente el publish (elimina libVLC x86 y plugins no usados).


---

## 📦 Generar el instalador

1. Instala [**Inno Setup 6**](https://jrsoftware.org/isdl.php).
2. Publica la app (paso anterior) en la carpeta `publish`.
3. *(Opcional)* Coloca el bootstrapper de WebView2 en `installer/redist/`
   (`MicrosoftEdgeWebview2Setup.exe`, desde
   [aquí](https://go.microsoft.com/fwlink/p/?LinkId=2124703)) para que el instalador
   pueda instalarlo si falta.
4. Compila el script:
   ```bash
   "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" installer\SportsMax.iss
   ```
   o, si Inno se instaló por usuario:
   ```bash
   "%LOCALAPPDATA%\Programs\Inno Setup 6\ISCC.exe" installer\SportsMax.iss
   ```
5. El instalador queda en `installer/Output/SportsMax-Setup-1.0.0.exe`.

<img width="1919" height="1015" alt="image" src="https://github.com/user-attachments/assets/7cd376d3-6107-4ea3-9cd3-321df29feedd" />
---

## 🧩 Tecnologías

- **C# / .NET 8 · WPF** — interfaz de escritorio.
- **[LibVLCSharp](https://github.com/videolan/libvlcsharp) + libVLC** — motor de video nativo.
- **[Microsoft Edge WebView2](https://developer.microsoft.com/microsoft-edge/webview2/)** —
  navegador embebido (Chromium) para reproducir las páginas de los canales.
- **Inno Setup** — instalador.

---

## 📁 Estructura del proyecto

```
SportsMax/
├─ App.xaml(.cs)            Aplicación, recursos y tema oscuro
├─ MainWindow.xaml(.cs)     Ventana principal (lista + reproductor)
├─ PipWindow.xaml(.cs)      Ventana flotante (Picture-in-Picture)
├─ Models/
│   └─ SportEvent.cs        Modelo de evento + cálculo de estado por hora local
├─ Services/
│   ├─ IEventSource.cs      Contrato de fuente de eventos
│   ├─ EventAggregator.cs   Agrega y deduplica las fuentes
│   ├─ La14HdSource.cs      Parser de fuentes con array plano
│   ├─ PltvHdSource.cs      Parser de fuentes tipo Strapi
│   ├─ StreamTpSource.cs    Parser de fuente StreamTP
│   ├─ AdBlocker.cs         Bloqueo de ads/popups + bypass CORS (WebView2)
│   ├─ Obfuscator.cs        Ofuscación ligera de las URLs de fuentes
│   └─ UrlUtils.cs          Utilidades de URL
├─ Assets/                  Logo e icono
├─ installer/
│   ├─ SportsMax.iss        Script del instalador (Inno Setup)
│   └─ license.txt          Aviso legal mostrado en el asistente
├─ PRIVACY.md               Política de privacidad
├─ LICENSE                  Licencia MIT (+ nota sobre contenido de terceros)
└─ README.md
```

---

## 🔒 Privacidad

SportsMax **no recopila datos personales, no incluye telemetría ni publicidad propia**.
Toda la información (registros y caché del navegador) permanece en tu equipo en
`%LOCALAPPDATA%\SportsMax`. Lee la política completa en [PRIVACY.md](PRIVACY.md).

---

## 📄 Licencia

Código distribuido bajo licencia **[MIT](LICENSE)**. La licencia cubre únicamente el código
de la aplicación, **no** el contenido de terceros.
