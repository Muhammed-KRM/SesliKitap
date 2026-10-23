using System.Buffers;
using System.Security.Cryptography;
using System.Text;

namespace SesliKitap.Business.Helpers;

/// <summary>
/// Akış (Stream), bayt dizisi ve metinler üzerinden kriptografik özet (SHA-256 ve SHA-1) hesaplama yardımcısı.
/// </summary>
public static class HashHelper
{
    private const int BufferSize = 81920; // 80 KB

    /// <summary>
    /// Verilen akışın tamamını 80 KB tamponlarla okuyarak küçük harf onaltılık (hex) SHA-256 özetini hesaplar.
    /// Akış seekable ise başlangıç konumuna geri sarar.
    /// </summary>
    /// <param name="stream">Okunacak veri akışı.</param>
    /// <param name="ct">İptal belirteci.</param>
    /// <returns>64 karakterli küçük harf hex SHA-256 özeti.</returns>
    public static async Task<string> ComputeSha256Async(Stream stream, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(stream);

        long? originalPosition = stream.CanSeek ? stream.Position : null;
        if (stream.CanSeek)
        {
            stream.Position = 0;
        }

        using var incrementalHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = ArrayPool<byte>.Shared.Rent(BufferSize);

        try
        {
            int bytesRead;
            while ((bytesRead = await stream.ReadAsync(buffer.AsMemory(0, BufferSize), ct)) > 0)
            {
                incrementalHash.AppendData(buffer, 0, bytesRead);
            }

            var hashBytes = incrementalHash.GetHashAndReset();
            return Convert.ToHexStringLower(hashBytes);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
            if (originalPosition.HasValue && stream.CanSeek)
            {
                stream.Position = originalPosition.Value;
            }
        }
    }

    /// <summary>
    /// Verilen metnin UTF-8 baytları üzerinden küçük harf onaltılık (hex) SHA-256 özetini hesaplar.
    /// </summary>
    /// <param name="input">Özetlenecek metin.</param>
    /// <returns>64 karakterli küçük harf hex SHA-256 özeti.</returns>
    public static string ComputeSha256(string input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var bytes = Encoding.UTF8.GetBytes(input);
        var hashBytes = SHA256.HashData(bytes);
        return Convert.ToHexStringLower(hashBytes);
    }

    /// <summary>
    /// Verilen bayt dizisinin küçük harf onaltılık (hex) SHA-256 özetini hesaplar.
    /// </summary>
    /// <param name="bytes">Özetlenecek bayt dizisi.</param>
    /// <returns>64 karakterli küçük harf hex SHA-256 özeti.</returns>
    public static string ComputeSha256(ReadOnlySpan<byte> bytes)
    {
        var hashBytes = SHA256.HashData(bytes);
        return Convert.ToHexStringLower(hashBytes);
    }

    /// <summary>
    /// Önbellek anahtarları için verilen metnin küçük harf onaltılık (hex) SHA-1 özetini üretir.
    /// </summary>
    /// <param name="input">Özetlenecek girdi metni.</param>
    /// <returns>40 karakterli küçük harf hex SHA-1 özeti.</returns>
#pragma warning disable CA5350 // Önbellek anahtarı üretimi için SHA-1 güvenlidir (kriptografik amaçlı değildir)
    public static string Sha1Hex(string input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var bytes = Encoding.UTF8.GetBytes(input);
        var hashBytes = SHA1.HashData(bytes);
        return Convert.ToHexStringLower(hashBytes);
    }
#pragma warning restore CA5350
}
