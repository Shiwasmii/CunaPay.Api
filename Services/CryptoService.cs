using System.Security.Cryptography;
using System.Text;

namespace CunaPay.Api.Services;

public class CryptoService
{
    private readonly byte[] _masterKey;

    public CryptoService(IConfiguration configuration)
    {
        var masterKeyHex = configuration["Crypto:MasterKeyHex"] ?? new string('0', 64);
        _masterKey = Convert.FromHexString(masterKeyHex);
        
        if (_masterKey.Length != 32)
        {
            throw new ArgumentException("MASTER_KEY must be 32 bytes hex (64 hex chars)");
        }
    }

    public string Encrypt(string plainText)
    {
        var iv = new byte[12];
        RandomNumberGenerator.Fill(iv);
        
        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var cipherBytes = new byte[plainBytes.Length];
        var tag = new byte[16];

        using (var aesGcm = new AesGcm(_masterKey))
        {
            aesGcm.Encrypt(iv, plainBytes, cipherBytes, tag);
        }

        // Combine: [12 byte IV][16 byte tag][N byte ciphertext]
        var result = new byte[12 + 16 + cipherBytes.Length];
        Array.Copy(iv, 0, result, 0, 12);
        Array.Copy(tag, 0, result, 12, 16);
        Array.Copy(cipherBytes, 0, result, 28, cipherBytes.Length);
        
        return Convert.ToBase64String(result);
    }

    public string Decrypt(string cipherTextBase64)
    {
        var combined = Convert.FromBase64String(cipherTextBase64);
        if (combined.Length < 28)
        {
            throw new ArgumentException("Invalid ciphertext format");
        }

        var iv = new byte[12];
        var tag = new byte[16];
        var cipherBytes = new byte[combined.Length - 28];

        Array.Copy(combined, 0, iv, 0, 12);
        Array.Copy(combined, 12, tag, 0, 16);
        Array.Copy(combined, 28, cipherBytes, 0, cipherBytes.Length);

        var plainBytes = new byte[cipherBytes.Length];
        
        using (var aesGcm = new AesGcm(_masterKey))
        {
            aesGcm.Decrypt(iv, cipherBytes, tag, plainBytes);
        }
        
        return Encoding.UTF8.GetString(plainBytes);
    }
}
