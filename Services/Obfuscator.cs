using System;
using System.Text;

namespace SportsMax.Services;

/// <summary>
/// Ofuscacion ligera (XOR + Base64) para no exponer las URLs de las fuentes
/// en texto plano dentro del codigo fuente publicado.
/// NOTA: no es cifrado seguro — la clave esta en el binario; solo evita que
/// las URLs sean legibles/indexables directamente en el repositorio.
/// </summary>
internal static class Obfuscator
{
    private static readonly byte[] Key =
        Encoding.UTF8.GetBytes("Sp0rtsM4x::feed::v1::2026");

    /// <summary>Decodifica un valor producido por <see cref="Encode"/>.</summary>
    public static string Decode(string encoded)
    {
        if (string.IsNullOrEmpty(encoded)) return string.Empty;
        var data = Convert.FromBase64String(encoded);
        var outb = new byte[data.Length];
        for (int i = 0; i < data.Length; i++)
            outb[i] = (byte)(data[i] ^ Key[i % Key.Length]);
        return Encoding.UTF8.GetString(outb);
    }

    /// <summary>
    /// Codifica un texto (uso de desarrollo: para generar los literales que se
    /// pegan en el codigo). XOR es simetrico, asi que reutiliza la misma logica.
    /// </summary>
    public static string Encode(string plain)
    {
        if (string.IsNullOrEmpty(plain)) return string.Empty;
        var data = Encoding.UTF8.GetBytes(plain);
        var outb = new byte[data.Length];
        for (int i = 0; i < data.Length; i++)
            outb[i] = (byte)(data[i] ^ Key[i % Key.Length]);
        return Convert.ToBase64String(outb);
    }
}
