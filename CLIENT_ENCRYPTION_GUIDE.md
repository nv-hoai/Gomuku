# Hướng Dẫn Tích Hợp Mã Hóa Cho Client (Unity C#)

## Tổng Quan

Server đã triển khai cơ chế mã hóa hybrid RSA + AES-256 để bảo vệ communication. Flow như sau:

1. **Server**: Tạo RSA key pair (2048-bit) khi khởi động
2. **Client → Server**: Yêu cầu public key
3. **Server → Client**: Gửi RSA public key
4. **Client**: Sinh session key (32 bytes ngẫu nhiên cho AES-256)
5. **Client → Server**: Mã hóa session key bằng RSA public key và gửi
6. **Server**: Giải mã session key bằng private key
7. **Cả 2 bên**: Dùng AES-256 để mã hóa/giải mã tất cả messages

---

## Message Protocol

### 1. Lấy Public Key Từ Server

**Client gửi:**
```
GET_PUBLIC_KEY
```

**Server trả về:**
```
PUBLIC_KEY:{"PublicKey":"<RSA_XML_STRING>","Message":"Server public key"}
```

### 2. Gửi Session Key

**Client gửi:**
```
SET_SESSION_KEY:<base64_encrypted_session_key>
```

**Server trả về:**
```
SESSION_KEY_ACK:Encryption enabled
```

### 3. Giao Tiếp Đã Mã Hóa

**Format cho messages đã mã hóa:**
```
ENC:<base64_encrypted_data>
```

---

## Code Mẫu Cho Client (Unity C#)

### Bước 1: Thêm CryptoUtil Helpers

```csharp
using System;
using System.Security.Cryptography;
using System.Text;
using System.IO;

public static class ClientCryptoUtil
{
    // Generate random bytes for session key
    public static byte[] GenerateRandomBytes(int length)
    {
        byte[] bytes = new byte[length];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(bytes);
        }
        return bytes;
    }

    // Convert bytes to Base64
    public static string ToBase64(byte[] data)
    {
        return Convert.ToBase64String(data);
    }

    // Convert Base64 to bytes
    public static byte[] FromBase64(string base64)
    {
        return Convert.FromBase64String(base64);
    }

    // RSA encrypt using XML public key
    public static byte[] RsaEncrypt(byte[] data, string publicKeyXml)
    {
        using (var rsa = new RSACryptoServiceProvider(2048))
        {
            rsa.FromXmlString(publicKeyXml);
            return rsa.Encrypt(data, true); // OAEP padding
        }
    }

    // AES encrypt (returns IV + ciphertext)
    public static byte[] AesEncrypt(byte[] plainText, byte[] key)
    {
        using (var aes = Aes.Create())
        {
            aes.Key = key;
            aes.GenerateIV();
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using (var encryptor = aes.CreateEncryptor())
            using (var ms = new MemoryStream())
            {
                // Prepend IV to ciphertext
                ms.Write(aes.IV, 0, aes.IV.Length);
                
                using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                {
                    cs.Write(plainText, 0, plainText.Length);
                    cs.FlushFinalBlock();
                }
                
                return ms.ToArray();
            }
        }
    }

    // AES decrypt (expects IV + ciphertext)
    public static byte[] AesDecrypt(byte[] ivAndCipherText, byte[] key)
    {
        using (var aes = Aes.Create())
        {
            aes.Key = key;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            // Extract IV (first 16 bytes)
            byte[] iv = new byte[aes.BlockSize / 8];
            Array.Copy(ivAndCipherText, 0, iv, 0, iv.Length);

            // Extract ciphertext
            byte[] cipherText = new byte[ivAndCipherText.Length - iv.Length];
            Array.Copy(ivAndCipherText, iv.Length, cipherText, 0, cipherText.Length);

            aes.IV = iv;

            using (var decryptor = aes.CreateDecryptor())
            using (var ms = new MemoryStream(cipherText))
            using (var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
            using (var result = new MemoryStream())
            {
                cs.CopyTo(result);
                return result.ToArray();
            }
        }
    }
}
```

### Bước 2: NetworkClient Class

```csharp
using System;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;

public class NetworkClient
{
    private TcpClient client;
    private NetworkStream stream;
    
    // Encryption
    private byte[] sessionKey;
    private bool isEncryptionEnabled = false;
    
    public async Task<bool> ConnectAsync(string host, int port)
    {
        try
        {
            client = new TcpClient();
            await client.ConnectAsync(host, port);
            stream = client.GetStream();
            Debug.Log("Connected to server");
            
            // Initialize encryption
            await InitializeEncryption();
            
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"Connection failed: {ex.Message}");
            return false;
        }
    }

    private async Task InitializeEncryption()
    {
        try
        {
            // Step 1: Request public key
            await SendRawMessage("GET_PUBLIC_KEY");
            
            // Step 2: Receive public key (wait for response)
            string response = await ReceiveRawMessage();
            
            if (response.StartsWith("PUBLIC_KEY:"))
            {
                string json = response.Substring("PUBLIC_KEY:".Length);
                var data = JsonConvert.DeserializeObject<PublicKeyResponse>(json);
                
                // Step 3: Generate session key (32 bytes for AES-256)
                sessionKey = ClientCryptoUtil.GenerateRandomBytes(32);
                
                // Step 4: Encrypt session key with RSA public key
                byte[] encryptedSessionKey = ClientCryptoUtil.RsaEncrypt(sessionKey, data.PublicKey);
                string encryptedBase64 = ClientCryptoUtil.ToBase64(encryptedSessionKey);
                
                // Step 5: Send encrypted session key to server
                await SendRawMessage($"SET_SESSION_KEY:{encryptedBase64}");
                
                // Step 6: Wait for acknowledgment
                string ack = await ReceiveRawMessage();
                
                if (ack.StartsWith("SESSION_KEY_ACK:"))
                {
                    isEncryptionEnabled = true;
                    Debug.Log("Encryption enabled successfully!");
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Encryption initialization failed: {ex.Message}");
        }
    }

    // Send unencrypted message (for handshake only)
    private async Task SendRawMessage(string message)
    {
        byte[] data = Encoding.UTF8.GetBytes(message + "\n");
        await stream.WriteAsync(data, 0, data.Length);
        await stream.FlushAsync();
        Debug.Log($"Sent: {message}");
    }

    // Receive unencrypted message (for handshake only)
    private async Task<string> ReceiveRawMessage()
    {
        byte[] buffer = new byte[4096];
        int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
        string message = Encoding.UTF8.GetString(buffer, 0, bytesRead).Trim();
        Debug.Log($"Received: {message}");
        return message;
    }

    // Send encrypted message (after handshake)
    public async Task SendMessage(string message)
    {
        try
        {
            string messageToSend = message;

            if (isEncryptionEnabled && sessionKey != null)
            {
                // Encrypt message
                byte[] plainBytes = Encoding.UTF8.GetBytes(message);
                byte[] encryptedBytes = ClientCryptoUtil.AesEncrypt(plainBytes, sessionKey);
                string encryptedBase64 = ClientCryptoUtil.ToBase64(encryptedBytes);
                messageToSend = $"ENC:{encryptedBase64}";
                Debug.Log($"Sending encrypted: {message}");
            }

            byte[] data = Encoding.UTF8.GetBytes(messageToSend + "\n");
            await stream.WriteAsync(data, 0, data.Length);
            await stream.FlushAsync();
        }
        catch (Exception ex)
        {
            Debug.LogError($"Send failed: {ex.Message}");
        }
    }

    // Receive encrypted message (continuous listening)
    public async Task<string> ReceiveMessage()
    {
        try
        {
            byte[] buffer = new byte[4096];
            int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
            
            if (bytesRead == 0)
                return null;

            string message = Encoding.UTF8.GetString(buffer, 0, bytesRead).Trim();

            // Decrypt if encrypted
            if (isEncryptionEnabled && sessionKey != null && message.StartsWith("ENC:"))
            {
                string encryptedData = message.Substring("ENC:".Length);
                byte[] encryptedBytes = ClientCryptoUtil.FromBase64(encryptedData);
                byte[] decryptedBytes = ClientCryptoUtil.AesDecrypt(encryptedBytes, sessionKey);
                message = Encoding.UTF8.GetString(decryptedBytes);
                Debug.Log($"Decrypted: {message}");
            }

            return message;
        }
        catch (Exception ex)
        {
            Debug.LogError($"Receive failed: {ex.Message}");
            return null;
        }
    }

    // Helper class for JSON deserialization
    [Serializable]
    private class PublicKeyResponse
    {
        public string PublicKey;
        public string Message;
    }
}
```

### Bước 3: Sử Dụng Trong Game

```csharp
using UnityEngine;
using System.Threading.Tasks;

public class GameManager : MonoBehaviour
{
    private NetworkClient networkClient;

    async void Start()
    {
        networkClient = new NetworkClient();
        
        // Connect và tự động setup encryption
        bool connected = await networkClient.ConnectAsync("127.0.0.1", 5000);
        
        if (connected)
        {
            // Bây giờ có thể gửi messages - sẽ tự động được mã hóa
            await networkClient.SendMessage("LOGIN:{\"Username\":\"player1\",\"Password\":\"pass123\"}");
            
            // Nhận messages - sẽ tự động được giải mã
            string response = await networkClient.ReceiveMessage();
            Debug.Log($"Server response: {response}");
        }
    }
}
```

---

## Lưu Ý Quan Trọng

### 1. **Messages Không Được Mã Hóa**
Chỉ 3 messages trong handshake không được mã hóa:
- `GET_PUBLIC_KEY` (client → server)
- `PUBLIC_KEY:...` (server → client)
- `SET_SESSION_KEY:...` (client → server)
- `SESSION_KEY_ACK:...` (server → client)

### 2. **Tất Cả Messages Khác Đều Được Mã Hóa**
Sau khi handshake hoàn tất, mọi message đều có format:
```
ENC:<base64_encrypted_data>
```

### 3. **Session Key**
- 32 bytes (256 bits) cho AES-256
- Được sinh ngẫu nhiên mỗi khi client connect
- Mã hóa bằng RSA-2048 với OAEP padding khi gửi

### 4. **AES Encryption**
- Mode: CBC
- Padding: PKCS7
- IV: 16 bytes, được prepend vào ciphertext
- Format: `[IV (16 bytes)][Ciphertext]`

### 5. **Compatibility**
- Server dùng: `System.Security.Cryptography`
- Client cần: .NET Standard 2.0+ (Unity 2018.3+)
- RSA XML format được dùng để truyền public key

---

## Testing

### Test Không Mã Hóa (Legacy)
Nếu muốn test không mã hóa, client có thể bỏ qua `InitializeEncryption()` và gửi trực tiếp:
```csharp
await SendRawMessage("LOGIN:{...}");
```

### Test Có Mã Hóa
1. Connect to server
2. Server sẽ tự động log: "RSA key pair generated"
3. Client gọi `InitializeEncryption()`
4. Kiểm tra logs cho "Encryption enabled"
5. Gửi message bình thường, sẽ thấy `[ENCRYPTED]` trong server logs

---

## Troubleshooting

### Lỗi: "Invalid session key length"
- Session key phải chính xác 32 bytes
- Check: `sessionKey.Length == 32`

### Lỗi: "Failed to decrypt message"
- Kiểm tra IV có được extract đúng không (first 16 bytes)
- Verify padding mode: PKCS7
- Check cipher mode: CBC

### Lỗi: "Invalid RSA key"
- Verify XML format từ server
- Ensure RSA là 2048-bit
- Check OAEP padding được dùng

### Server không nhận được message
- Check format: `ENC:<base64>`
- Verify newline `\n` ở cuối message
- Ensure session key đã được set trước khi gửi encrypted messages

---

## Security Best Practices

1. ✅ **Luôn validate** server responses trong production
2. ✅ **Không log** session key hoặc decrypted data trong production
3. ✅ **Implement timeout** cho handshake process
4. ✅ **Handle reconnection**: Generate new session key mỗi lần reconnect
5. ✅ **Certificate pinning** (optional): Verify server identity
6. ✅ **Rate limiting**: Implement trên client side để tránh spam

---

## Flow Diagram

```
Client                          Server
  |                               |
  |--- GET_PUBLIC_KEY ----------->|
  |                               | (Generate/retrieve RSA keys)
  |<-- PUBLIC_KEY:{xml} ----------|
  |                               |
  | (Generate sessionKey)         |
  | (Encrypt with RSA pubkey)     |
  |                               |
  |--- SET_SESSION_KEY:xxx ------>|
  |                               | (Decrypt with RSA privkey)
  |<-- SESSION_KEY_ACK -----------|
  |                               |
  |=== ENCRYPTED COMMUNICATION ===|
  |                               |
  |--- ENC:encrypted_login ------>|
  |                               | (Decrypt with AES)
  |<-- ENC:encrypted_response ----|
  | (Decrypt with AES)            |
  |                               |
```

---

## Kết Luận

Server đã sẵn sàng! Client chỉ cần:
1. Copy `ClientCryptoUtil` class
2. Integrate `NetworkClient` class
3. Gọi `ConnectAsync()` - encryption tự động được setup
4. Dùng `SendMessage()` / `ReceiveMessage()` như bình thường

Tất cả encryption/decryption sẽ transparent với game logic! 🔐
