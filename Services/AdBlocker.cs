using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace SportsMax.Services;

/// <summary>
/// Configuracion compartida del WebView2: opciones de browser, bloqueador de ads/popups
/// y auto-play robusto para los wrappers PHP.
/// </summary>
public static class AdBlocker
{
    // Argumentos que se pasan al proceso Chromium del WebView2.
    // - autoplay-policy: permite video.play() sin gesto previo del usuario
    // - disable-web-security: desactiva CORS/same-origin -> los .m3u8 alojados en
    //   CDNs distintos al wrapper (p.ej. fubohd.com) cargan sin manifestLoadError
    // - disable-features: desactiva aislamiento de sitios (necesario para que
    //   disable-web-security surta efecto) y heuristicas que matan el autoplay
    private const string BrowserArgs =
        "--autoplay-policy=no-user-gesture-required " +
        "--disable-web-security " +
        "--disable-features=IsolateOrigins,site-per-process,PreloadMediaEngagementData,MediaEngagementBypassAutoplayPolicies,AutoplayIgnoreWebAudio";

    // Subcadenas que disparan un bloqueo cuando aparecen en la URL del request.
    private static readonly string[] BlockedHosts =
    {
        // Google
        "doubleclick.net", "googlesyndication.com", "googleadservices.com",
        "googletagmanager.com", "googletagservices.com", "google-analytics.com",
        "googleads.g.doubleclick", "pagead2.googlesyndication",

        // Pop ads en sitios de streaming
        "popads.net", "popcash.net", "popunder.net", "popunderclick.com",
        "propellerads.com", "propeller-tracking.com", "propu.sh",
        "exoclick.com", "exosrv.com", "exdynsrv.com", "realsrv.com",
        "adcash.com", "mgid.com", "adsterra.com", "adsterra.net",
        "juicyads.com", "yllix.com", "yllix-cdn.com",
        "hilltopads.com", "hilltopads.net",
        "clickadu.com", "clickaine.com", "claudxyz.com",
        "admaven.com", "ad-maven.com", "adsmaven.com",
        "adskeeper.com", "adskeeper.co.uk",
        "onclickads.net", "onclkds.com", "onclickbiz.com",
        "trafficforce.com", "trafficstars.com",
        "trafficjunky.net", "trafficjunky.com",
        "luckyads.pro", "ero-advertising.com",
        "tsyndicate.com", "adservme.com", "videovard.com",
        "popsoul.com", "popmonster.cc",

        // RTB / exchanges
        "adnxs.com", "amazon-adsystem.com", "rubiconproject.com",
        "pubmatic.com", "openx.net", "casalemedia.com",
        "criteo.com", "criteo.net", "yieldmo.com",
        "smartadserver.com", "adform.net", "mathtag.com",
        "demdex.net", "everesttech.net", "krxd.net",

        // Recomendaciones / native ads
        "taboola.com", "outbrain.com", "revcontent.com",

        // Trackers
        "scorecardresearch.com", "moatads.com", "adsafeprotected.com",
        "connect.facebook.net", "facebook.com/tr",
        "histats.com", "statcounter.com", "addthis.com", "sharethis.com",

        // Patrones en path
        "/popunder", "/popunderjs", "/popads.", "/popunder.",
        "/ads.js", "/ads.html", "/banner.js", "ad-script",
    };

    // Se inyecta antes de cualquier script de la pagina (en cada frame).
    // Solo anti-popup + ocultar ads. NO fuerza autoplay (el bucle de play/click
    // hacia toggle en Clappr y rompia la carga del stream).
    private const string PageScript = @"
(function(){
  'use strict';
  try {
    // ============== ANTI-POPUP ==============
    try {
      window.open = function() { return null; };
      window.alert = function() {};
      window.confirm = function() { return false; };
      window.prompt = function() { return null; };
      var origAdd = EventTarget.prototype.addEventListener;
      EventTarget.prototype.addEventListener = function(type, fn, opts){
        try {
          if (type === 'click' && typeof fn === 'function') {
            var s = fn.toString();
            if (s.indexOf('window.open') >= 0 ||
                s.indexOf('popunder') >= 0 ||
                s.indexOf('popUnder') >= 0) {
              return;
            }
          }
        } catch(e){}
        return origAdd.apply(this, arguments);
      };
    } catch(e){}

    // ============== CONTROL DE VOLUMEN ==============
    try {
      if (typeof window.__smVol !== 'number') window.__smVol = 0.8;
      window.__smApplyVol = function(){
        try {
          document.querySelectorAll('video, audio').forEach(function(m){
            try { m.volume = window.__smVol; m.muted = (window.__smVol === 0); } catch(e){}
          });
        } catch(e){}
      };
      window.__smSetVolume = function(v){
        try {
          v = Number(v);
          if (isNaN(v)) return;
          window.__smVol = Math.max(0, Math.min(1, v));
          window.__smApplyVol();
        } catch(e){}
      };
      // Reaplica el volumen a videos que aparezcan despues (no reinicia la reproduccion)
      setInterval(window.__smApplyVol, 1200);
    } catch(e){}

    // ============== OCULTAR ADS ==============
    function hideAds(){
      try {
        var sels = [
          '[id^=""ads-""]','[id*=""banner""]','[class*=""banner-ad""]',
          '[class*=""ad-container""]','[class*=""advert""]',
          'iframe[src*=""doubleclick""]','iframe[src*=""googlesyndication""]',
          'iframe[src*=""adskeeper""]','iframe[src*=""mgid""]',
          'iframe[src*=""adsterra""]','iframe[src*=""propeller""]',
          'iframe[src*=""exoclick""]','iframe[src*=""popads""]'
        ];
        sels.forEach(function(s){
          try {
            document.querySelectorAll(s).forEach(function(el){
              el.style.display = 'none';
            });
          } catch(e){}
        });
      } catch(e){}
    }
    if (document.readyState !== 'loading') hideAds();
    else document.addEventListener('DOMContentLoaded', hideAds);
    setInterval(hideAds, 2000);

  } catch(e){}
})();
";

    /// <summary>
    /// Opciones de browser que se deben pasar al crear el CoreWebView2Environment.
    /// </summary>
    public static CoreWebView2EnvironmentOptions CreateEnvironmentOptions()
    {
        return new CoreWebView2EnvironmentOptions
        {
            AdditionalBrowserArguments = BrowserArgs
        };
    }

    /// <summary>
    /// Aplica handlers de bloqueo y el script combinado al WebView2 indicado.
    /// Llamar despues de EnsureCoreWebView2Async.
    /// </summary>
    public static async Task ApplyAsync(WebView2 webView)
    {
        if (webView?.CoreWebView2 == null) return;
        var cw = webView.CoreWebView2;

        // 1) Bloquea popups via window.open
        cw.NewWindowRequested += (s, e) =>
        {
            e.Handled = true;
            App.Log($"[Block popup] {e.Uri}");
        };

        // 2) No dejes que la pagina cierre el WebView
        cw.WindowCloseRequested += (s, e) =>
        {
            App.Log("[Block window.close]");
        };

        // 3) Deniega permisos sensibles
        cw.PermissionRequested += (s, e) =>
        {
            e.State = CoreWebView2PermissionState.Deny;
        };

        // 4) Filtro de recursos para dominios de ads
        try
        {
            cw.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);
            cw.WebResourceRequested += (s, e) =>
            {
                var url = (e.Request.Uri ?? string.Empty).ToLowerInvariant();
                foreach (var host in BlockedHosts)
                {
                    if (url.Contains(host))
                    {
                        try
                        {
                            e.Response = cw.Environment.CreateWebResourceResponse(
                                new MemoryStream(), 403, "Blocked by SportsMax", "Content-Type: text/plain");
                        }
                        catch { /* ignore */ }
                        return;
                    }
                }
            };
        }
        catch (Exception ex)
        {
            App.Log($"[AdBlocker filter] {ex.Message}");
        }

        // 5) Settings utiles
        try
        {
            cw.Settings.IsScriptEnabled = true;
            cw.Settings.IsStatusBarEnabled = false;
        }
        catch { /* propiedades opcionales */ }

        // 6) Inyectar script combinado (anti-popup + auto-play + ocultar ads)
        try
        {
            await cw.AddScriptToExecuteOnDocumentCreatedAsync(PageScript);
        }
        catch (Exception ex)
        {
            App.Log($"[AdBlocker script] {ex.Message}");
        }
    }
}
