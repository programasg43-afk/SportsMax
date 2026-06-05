using System;

namespace SportsMax.Services;

/// <summary>
/// Algunos wrappers de reproductor (p.ej. streamhdx.com/live1.php) solo sirven el
/// player cuando se cargan dentro de un &lt;iframe&gt;: detectan la navegacion
/// directa via el header Sec-Fetch-Dest y, si no es "iframe", devuelven una pagina
/// con el mensaje "Usa iframe para cargar este reproductor".
///
/// Para esos hosts cargamos la URL incrustada en un documento iframe a pantalla
/// completa (NavigateToString), replicando exactamente lo que hace la web oficial:
/// asi el navegador envia Sec-Fetch-Dest: iframe y el wrapper sirve el reproductor.
/// </summary>
public static class PlayerEmbed
{
    // Hosts cuyos wrappers exigen carga via iframe (no aceptan navegacion directa).
    private static readonly string[] IframeOnlyHosts =
    {
        "streamhdx.com",
    };

    /// <summary>True si la URL debe cargarse dentro de un iframe en el WebView.</summary>
    public static bool RequiresIframe(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return false;
        try
        {
            var host = new Uri(url).Host;
            foreach (var h in IframeOnlyHosts)
            {
                if (host.Equals(h, StringComparison.OrdinalIgnoreCase) ||
                    host.EndsWith("." + h, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }
        catch { /* url no parseable: se trata como normal */ }
        return false;
    }

    /// <summary>
    /// Documento HTML minimo (fondo negro, sin margenes) con un iframe a pantalla
    /// completa apuntando a <paramref name="url"/>. Al cargar el iframe, el navegador
    /// envia Sec-Fetch-Dest: iframe y el wrapper devuelve el reproductor real.
    /// </summary>
    public static string BuildIframeDocument(string url)
    {
        var safe = (url ?? string.Empty)
            .Replace("&", "&amp;")
            .Replace("\"", "&quot;");

        return @"<!DOCTYPE html><html><head><meta charset=""utf-8"">
<meta name=""viewport"" content=""width=device-width, initial-scale=1"">
<style>
  html,body{margin:0;padding:0;width:100%;height:100%;background:#000;overflow:hidden}
  iframe{position:fixed;inset:0;width:100%;height:100%;border:0;background:#000}
</style></head>
<body>
  <iframe src=""" + safe + @"""
    allow=""autoplay; fullscreen; encrypted-media; picture-in-picture""
    allowfullscreen scrolling=""no""></iframe>
</body></html>";
    }

    /// <summary>
    /// Autoplay "suave": arranca la reproduccion llamando a las APIs del reproductor
    /// (Clappr/JW/video.js/&lt;video&gt;) SIN clicks sinteticos — esos provocaban un
    /// toggle play/pause que rompia la carga del stream. Reintenta unas veces y
    /// recorre tambien los iframes (p.ej. el wrapper incrustado de streamhdx).
    /// Se inyecta tras NavigationCompleted, tanto en el reproductor principal como
    /// en la ventana flotante.
    /// </summary>
    public const string AutoplayScript = @"
(function(){
  var tries = 0;
  function wins(){
    var ws = [window];
    try { document.querySelectorAll('iframe').forEach(function(f){
      try { if (f.contentWindow) ws.push(f.contentWindow); } catch(e){}
    }); } catch(e){}
    return ws;
  }
  function go(){
    wins().forEach(function(w){
      var d = null; try { d = w.document; } catch(e){}
      try { if(w.player && w.player.play){ if(!w.player.isPlaying || !w.player.isPlaying()) w.player.play(); } } catch(e){}
      try { if(w.jwplayer){ var jw=w.jwplayer(); if(jw && jw.play) jw.play(true); } } catch(e){}
      try { if(w.videojs && w.videojs.getAllPlayers){ w.videojs.getAllPlayers().forEach(function(p){ try{p.play();}catch(e){} }); } } catch(e){}
      try { if(d) d.querySelectorAll('video').forEach(function(v){ try { v.autoplay = true; v.play && v.play().catch(function(){}); } catch(e){} }); } catch(e){}
    });
    tries++;
    if (tries < 8) setTimeout(go, 700);
  }
  setTimeout(go, 200);
})();";
}
