using System;
using System.Buffers.Text;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace MatriX.GST.Services;

public static class AesTo
{
    #region Encrypt
    public static string Encrypt(ReadOnlySpan<char> plainText)
    {
        if (plainText.IsEmpty)
            return null;

        try
        {
            var aesinst = AesPool.Instance;

            int byteCount = Encoding.UTF8.GetByteCount(plainText);

            byte[] cipherBuf = null;
            if (byteCount > AesInstance.ByteSize)
                cipherBuf = new byte[byteCount];

            Span<byte> cipher = cipherBuf != null
                ? cipherBuf.AsSpan()
                : aesinst.ByteBuffer;

            if (!Encoding.UTF8.TryGetBytes(plainText, cipher, out int writtenPlain) || writtenPlain == 0)
                return null;

            int paddedLen = aesinst.Aes.GetCiphertextLengthCbc(writtenPlain, PaddingMode.PKCS7);

            byte[] destBuf = null;
            if (paddedLen > AesInstance.ByteSize)
                destBuf = new byte[paddedLen];

            Span<byte> dest = destBuf != null
                ? destBuf.AsSpan()
                : aesinst.DestBuffer;

            int cipherLen = aesinst.Aes.EncryptCbc(
                cipher.Slice(0, writtenPlain),
                aesinst.Aes.IV,
                dest,
                PaddingMode.PKCS7);

            if (cipherLen <= 0)
                return null;

            int maxChars = Base64Url.GetEncodedLength(cipherLen);

            char[] base64Chars = null;
            if (maxChars > AesInstance.CharSize)
                base64Chars = new char[maxChars];

            Span<char> buffer = base64Chars != null
                ? base64Chars.AsSpan()
                : aesinst.CharBuffer;

            if (!Base64Url.TryEncodeToChars(dest.Slice(0, cipherLen), buffer, out int charsWritten))
                return null;

            return new string(buffer.Slice(0, charsWritten));
        }
        catch
        {
            return null;
        }
    }
    #endregion

    #region Decrypt
    public static string Decrypt(ReadOnlySpan<char> cipherText)
    {
        if (cipherText.IsEmpty)
            return null;

        try
        {
            var aesinst = AesPool.Instance;

            int maxBytes = Base64Url.GetMaxDecodedLength(cipherText.Length);

            byte[] cipherBuf = null;
            if (maxBytes > AesInstance.ByteSize)
                cipherBuf = new byte[maxBytes];

            Span<byte> cipher = cipherBuf != null
                ? cipherBuf.AsSpan()
                : aesinst.ByteBuffer;

            if (!Base64Url.TryDecodeFromChars(cipherText, cipher, out int cipherLen))
                return null;

            byte[] destBuf = null;
            if (cipherLen > AesInstance.ByteSize)
                destBuf = new byte[cipherLen];

            Span<byte> dest = destBuf != null
                ? destBuf.AsSpan()
                : aesinst.DestBuffer;

            int plainLen = aesinst.Aes.DecryptCbc(
                cipher.Slice(0, cipherLen),
                aesinst.Aes.IV,
                dest,
                PaddingMode.PKCS7);

            if (plainLen <= 0)
                return null;

            return Encoding.UTF8.GetString(dest.Slice(0, plainLen));
        }
        catch
        {
            return null;
        }
    }
    #endregion
}


public static class AesPool
{
    static byte[] aesKey, aesIV;

    [ThreadStatic]
    private static AesInstance _instance;

    static AesPool()
    {
        if (File.Exists("aeskey"))
        {
            var i = File.ReadAllText("aeskey").Split("/");
            aesKey = Encoding.UTF8.GetBytes(i[0]);
            aesIV = Encoding.UTF8.GetBytes(i[1]);
        }
        else
        {
            string k = Code(16);
            string v = Code(16);
            File.WriteAllText("aeskey", $"{k}/{v}");

            aesKey = Encoding.UTF8.GetBytes(k);
            aesIV = Encoding.UTF8.GetBytes(v);
        }
    }

    static readonly string ArrayList = "qwertyuioplkjhgfdsazxcvbnmQWERTYUIOPLKJHGFDSAZXCVBNM1234567890";

    static string Code(int size = 8)
    {
        return string.Create(size, ArrayList, (span, chars) =>
        {
            for (int i = 0; i < span.Length; i++)
            {
                span[i] = chars[Random.Shared.Next(chars.Length)];
            }
        });
    }

    public static AesInstance Instance
        => (_instance ??= new AesInstance(aesKey, aesIV));
}

public class AesInstance
{
    public AesInstance(byte[] aesKey, byte[] aesIV)
    {
        Aes = Aes.Create();
        Aes.Mode = CipherMode.CBC;
        Aes.Padding = PaddingMode.PKCS7;

        Aes.Key = aesKey;
        Aes.IV = aesIV;
    }

    public readonly Aes Aes;

    public const int CharSize = 4096;
    public const int ByteSize = 16 * 1024;

    private char[] _charBuffer;
    private byte[] _byteBuffer;
    private byte[] _destBuffer;

    public char[] CharBuffer
        => _charBuffer ??= new char[CharSize];

    public byte[] ByteBuffer
        => _byteBuffer ??= new byte[ByteSize];

    public byte[] DestBuffer
        => _destBuffer ??= new byte[ByteSize];
}