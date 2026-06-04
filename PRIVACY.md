# Política de Privacidad de SportsMax

**Última actualización:** 3 de junio de 2026
**Versión de la aplicación:** 1.0

Esta Política de Privacidad describe cómo la aplicación de escritorio **SportsMax**
("la Aplicación", "el Software") trata la información cuando usted la utiliza. Le
recomendamos leerla en su totalidad. Al instalar y usar SportsMax, usted declara
haber leído y aceptado los términos aquí descritos.

---

## 1. Resumen rápido

- **SportsMax NO recopila, almacena ni transmite datos personales a los desarrolladores.**
- **SportsMax NO incluye telemetría, analítica, publicidad propia ni rastreadores.**
- Toda la información que la Aplicación genera (registros, caché) **permanece en su
  equipo** y nunca se envía a servidores controlados por los desarrolladores.
- SportsMax **no aloja contenido de video**: únicamente muestra una agenda pública de
  eventos deportivos y abre enlaces de **terceros** que usted decide reproducir.

---

## 2. Responsable del tratamiento

SportsMax es un proyecto de software distribuido "tal cual". No existe un servidor
central propio ni una entidad que recopile información de los usuarios. El tratamiento
de datos se limita al funcionamiento local de la Aplicación en su dispositivo.

---

## 3. Información que la Aplicación maneja

### 3.1 Información que NO recopilamos
SportsMax **no solicita ni recopila**:
- Nombre, correo electrónico, teléfono u otros identificadores personales.
- Cuentas de usuario o credenciales (la Aplicación no tiene inicio de sesión).
- Ubicación geográfica precisa.
- Contactos, archivos personales o información del sistema más allá de lo necesario
  para ejecutarse.

### 3.2 Datos almacenados localmente en su equipo
Para funcionar, la Aplicación crea y mantiene los siguientes datos **solo en su
dispositivo**, en la carpeta `%LOCALAPPDATA%\SportsMax`:

| Dato | Propósito | Ubicación |
|------|-----------|-----------|
| Archivo de registro (`sportsmax.log`) | Diagnóstico de errores y eventos de la app | `%LOCALAPPDATA%\SportsMax\sportsmax.log` |
| Caché y datos del navegador embebido (WebView2) | Reproducir las páginas de los canales | `%LOCALAPPDATA%\SportsMax\WebView2` y `WebView2Pip` |

Estos datos **nunca se envían a los desarrolladores**. Usted puede eliminarlos en
cualquier momento borrando la carpeta indicada, o desinstalando la Aplicación.

---

## 4. Navegador embebido (Microsoft WebView2)

SportsMax utiliza **Microsoft Edge WebView2** (motor Chromium) para reproducir las
páginas de los proveedores de transmisión. Como cualquier navegador, WebView2 puede
almacenar localmente **cookies, almacenamiento de sitio y caché** de las páginas de
terceros que usted abre. Estos datos:

- Se guardan **localmente** en la carpeta de datos de WebView2 mencionada arriba.
- Son gestionados por el componente WebView2 de Microsoft, sujeto a la
  [Declaración de privacidad de Microsoft](https://privacy.microsoft.com/).
- Pueden eliminarse borrando las carpetas `WebView2` y `WebView2Pip`.

SportsMax incluye un **bloqueador de ventanas emergentes y de redes publicitarias**
que reduce las cookies y rastreadores de terceros cargados por dichas páginas.

---

## 5. Conexiones de red que realiza la Aplicación

SportsMax establece conexiones de red únicamente para:

1. **Descargar la agenda de eventos** desde las fuentes públicas de terceros
   configuradas en la Aplicación (archivos JSON de programación deportiva).
2. **Reproducir el canal que usted seleccione**, cargando la página o el flujo de
   video del proveedor de terceros correspondiente y sus redes de distribución (CDN).
3. **Instalar/verificar el componente WebView2** (solo durante la instalación, si no
   está presente en el sistema).

La Aplicación **no envía** información sobre su actividad, hábitos de visualización ni
identificadores a los desarrolladores ni a terceros con fines de perfilado. Las
conexiones a fuentes y CDNs de terceros se rigen por las políticas de privacidad de
dichos terceros, sobre las que SportsMax no tiene control.

---

## 6. Cookies y tecnologías de rastreo

- La Aplicación **en sí** no usa cookies propias ni identificadores de seguimiento.
- Las **páginas de terceros** reproducidas dentro del navegador embebido pueden
  intentar establecer cookies; el bloqueador integrado mitiga las de redes
  publicitarias conocidas, pero no puede garantizar el bloqueo del 100 %.

---

## 7. Publicidad

SportsMax **no muestra publicidad propia** y **no comparte datos con anunciantes**.
Por el contrario, incorpora un bloqueador que intenta suprimir los anuncios y ventanas
emergentes que las páginas de terceros pudieran mostrar.

---

## 8. Contenido de terceros (aviso importante)

SportsMax es un **agregador y reproductor**. La Aplicación:

- **No aloja, no produce, no almacena y no distribuye** ningún contenido audiovisual.
- Obtiene una **agenda pública** de eventos desde fuentes de terceros y **abre enlaces
  de terceros** que el usuario elige.
- **No tiene control** sobre la disponibilidad, legalidad, exactitud o licencias del
  contenido al que dichos enlaces apuntan.

La responsabilidad sobre el acceso y uso de ese contenido recae **exclusivamente en el
usuario final**, quien debe asegurarse de cumplir las leyes de su jurisdicción y los
términos de los titulares de derechos. Consulte el descargo de responsabilidad del
archivo `README.md`.

---

## 9. Seguridad

La Aplicación se ejecuta localmente con los permisos del usuario del sistema. No abre
puertos de servidor ni acepta conexiones entrantes. Los datos locales (registros y
caché) quedan protegidos por los permisos de su cuenta de Windows. Aun así, ningún
software es completamente infalible; úsela bajo su propia responsabilidad.

---

## 10. Privacidad de menores

SportsMax no está dirigida a menores de edad y no recopila conscientemente información
de ellos. El contenido de terceros puede no ser apropiado para todas las edades; se
recomienda la supervisión de un adulto.

---

## 11. Sus controles y derechos

Usted puede en cualquier momento:

- **Eliminar los datos locales**: borrando la carpeta `%LOCALAPPDATA%\SportsMax`.
- **Desinstalar la Aplicación**: desde "Agregar o quitar programas" de Windows, lo que
  elimina el software (puede eliminar manualmente la carpeta de datos si desea borrar
  también registros y caché).
- **Revisar el código fuente**: SportsMax es de código abierto; puede auditar qué hace
  la Aplicación.

---

## 12. Cambios en esta Política

Esta Política puede actualizarse en futuras versiones de la Aplicación. La fecha de
"Última actualización" en la parte superior reflejará la versión vigente. El uso
continuado de la Aplicación tras una actualización implica la aceptación de los
cambios.

---

## 13. Descargo de responsabilidad

EL SOFTWARE SE PROPORCIONA "TAL CUAL", SIN GARANTÍA DE NINGÚN TIPO. LOS DESARROLLADORES
NO SE RESPONSABILIZAN DEL CONTENIDO DE TERCEROS, DE LA DISPONIBILIDAD DE LAS FUENTES, NI
DEL USO QUE EL USUARIO HAGA DE LOS ENLACES. CONSULTE EL ARCHIVO `LICENSE` Y EL DESCARGO
DE RESPONSABILIDAD DEL `README.md`.

---

## 14. Contacto

Al ser un proyecto de código abierto, las consultas pueden realizarse a través del
repositorio en GitHub (sección *Issues*). No existe un canal que recopile datos
personales de los usuarios.
