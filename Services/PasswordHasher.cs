using System.Security.Cryptography;
using Konscious.Security.Cryptography;

namespace Api.Services;

// Convención: contrasenia_hash = [salt(16 bytes) | hash(32 bytes)]
public static class PasswordHasher
{
    private const int SaltLen = 16;
    private const int HashLen = 32;

    private const int Iterations = 3;     // t
    private const int MemoryKb   = 65536; // m
    private const int Degree     = 1;     // p

    public static bool VerifyArgon2id(string password, byte[] stored)
    {
        if (stored is null || stored.Length < SaltLen + HashLen) return false;

        var salt = stored.AsSpan(0, SaltLen).ToArray();
        var hash = stored.AsSpan(SaltLen, HashLen).ToArray();

        var calc = ComputeArgon2id(password, salt);
        return CryptographicOperations.FixedTimeEquals(hash, calc);
    }

    public static byte[] CreateArgon2id(string password, out string algoritmo)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltLen);
        var hash = ComputeArgon2id(password, salt);

        var result = new byte[SaltLen + HashLen];
        Buffer.BlockCopy(salt, 0, result, 0, SaltLen);
        Buffer.BlockCopy(hash, 0, result, SaltLen, HashLen);

        algoritmo = $"argon2id(v=19,m={MemoryKb},t={Iterations},p={Degree},saltlen={SaltLen},hashlen={HashLen})";
        return result;
    }

    private static byte[] ComputeArgon2id(string password, byte[] salt)
    {
        var argon = new Argon2id(System.Text.Encoding.UTF8.GetBytes(password))
        {
            Salt = salt,
            Iterations = Iterations,
            MemorySize = MemoryKb,
            DegreeOfParallelism = Degree
        };
        return argon.GetBytes(HashLen);
    }
}
