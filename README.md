# Установка на linux
```
curl -s https://raw.githubusercontent.com/ngcores/MatriX.GST/master/install.sh | bash
```

# settings.json
```
{
  "port": 8590,
  "IPAddressAny": true,
  "worknodetominutes": 5,
  "tsargs": "--pubipv4 внешний_ip"
}
```

# /etc/hosts
```
127.0.1.1 google.com
127.0.1.1 www.google.com
```

# aeskey
```
Yn933YWpC6WnL23h/15J8uPGgMOSl677S
```

# Gst payload
```
json
{"userId":"gstmandalorian","target":"gst","magnet":"magnet:?xt=urn:btih:2a18dd802983c38426854835a192c134c38ab84a","queryString":"index=1&audio=0","default_settings":"gst_settings.json"}

payload
VaapQwgzvAw7_LsK5ZzKVt0FFZGybxCWTb-t5AWnPvUeSoNXc3foyc1oLvKSxWXtg_lZx6Upcm1LcRJOAXQ28tZ1yyveJErWqbvpJGFISuh9PpKK2pi5Wo2x9hCP50KZ05zoH119lfxcMJYTCuZF7DmASwP6Y5YlrUZYM7W5BG7gjDO_lsq-znHJnaQJhsXr3Bo6u_ftme5uVORApU25d6pTxxhJcdMfZsq-MuVmxH0ioFd-72WFqBnpx9jcB5bC

playlist
http://IP:8590/VaapQwgzvAw7_LsK5ZzKVt0FFZGybxCWTb-t5AWnPvUeSoNXc3foyc1oLvKSxWXtg_lZx6Upcm1LcRJOAXQ28tZ1yyveJErWqbvpJGFISuh9PpKK2pi5Wo2x9hCP50KZ05zoH119lfxcMJYTCuZF7DmASwP6Y5YlrUZYM7W5BG7gjDO_lsq-znHJnaQJhsXr3Bo6u_ftme5uVORApU25d6pTxxhJcdMfZsq-MuVmxH0ioFd-72WFqBnpx9jcB5bC.m3u8
```

# Stream payload
```
json
{"userId":"mandalorian","magnet":"magnet:?xt=urn:btih:2a18dd802983c38426854835a192c134c38ab84a","queryString":"index=1&play","default_settings":"default_settings.json"}

payload
x6KfMr_MY9vJV6aRz9zTVvMWzH3nRZJl_6v5HcRTfBj6sBWUd2lbjZ6b6xcUKuDoL06NLJFk5modyD_0LcvWUgWq--4o5Ea1ievij9Jvxrsq_gWDunmCuXFzZmWRKv3nSU9OYJW7ywvYqNh6JLijPUsr-5g0u23u99kWNmtmgi2Fj5vJrnlGg-c-NRKlGhFSlzEqzU-bC7oK3197SyLLqK68t-h8uiGJUKnW8g_ujJU

playlist
http://IP:8590/x6KfMr_MY9vJV6aRz9zTVvMWzH3nRZJl_6v5HcRTfBj6sBWUd2lbjZ6b6xcUKuDoL06NLJFk5modyD_0LcvWUgWq--4o5Ea1ievij9Jvxrsq_gWDunmCuXFzZmWRKv3nSU9OYJW7ywvYqNh6JLijPUsr-5g0u23u99kWNmtmgi2Fj5vJrnlGg-c-NRKlGhFSlzEqzU-bC7oK3197SyLLqK68t-h8uiGJUKnW8g_ujJU
```


## AES encryption format

```text
AES-CBC + PKCS#7 padding
```

Параметры:

```text
Cipher:      AES
Mode:        CBC
Padding:     PKCS#7
Text input:  UTF-8
Result:      Base64Url без "=" padding
Key size:    16 bytes
IV size:     16 bytes
```

Фактически используется:

```text
AES-128-CBC
```

### Файл ключа

Ключ и IV хранятся в файле `aeskey`.

Формат файла:

```text
<key>/<iv>
```

Пример:

```text
aB3kLm9QwErTy123/ZxCvBnM456qWeRtY
```

Где:

```text
key = "aB3kLm9QwErTy123"
iv  = "ZxCvBnM456qWeRtY"
```

Важно:

* `key` должен быть длиной **16 ASCII-символов**
* `iv` должен быть длиной **16 ASCII-символов**
* `key` и `iv` не должны содержать символ `/`
* `key` и `iv` не являются HEX
* `key` и `iv` не являются Base64
* их нужно брать как обычную строку и кодировать в UTF-8 bytes

То есть:

```text
keyBytes = UTF8(key)
ivBytes  = UTF8(iv)
```

### Encrypt

Алгоритм шифрования:

```text
plainBytes = UTF8(inputText)

cipherBytes = AES-CBC-Encrypt(
    data = plainBytes,
    key = UTF8(key),
    iv = UTF8(iv),
    padding = PKCS7
)

result = Base64UrlEncode(cipherBytes, withoutPadding = true)
```

Base64Url означает:

```text
+ заменяется на -
/ заменяется на _
= в конце убирается
```

### Псевдокод

```text
function encrypt(text):
    keyText, ivText = readFile("aeskey").split("/")

    key = utf8Bytes(keyText)
    iv = utf8Bytes(ivText)

    plainBytes = utf8Bytes(text)

    encryptedBytes = aesCbcEncrypt(
        plainBytes,
        key,
        iv,
        padding = "PKCS7"
    )

    return base64UrlEncode(encryptedBytes, removePadding = true)
```
