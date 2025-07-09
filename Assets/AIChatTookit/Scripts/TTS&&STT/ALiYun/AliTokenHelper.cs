using System;
using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class AliTokenHelper : MonoBehaviour
{
    #region Params
    [SerializeField] private string accessKeyId = "���AccessKeyId";
    [SerializeField] private string accessKeySecret = "���AccessKeySecret";
    [SerializeField] private string token = "";
    [SerializeField] private string appkey="���������app key";//app key
    // API ��ַ
    private string endpoint = "http://nls-meta.cn-shanghai.aliyuncs.com"; 

    #endregion

    void Start()
    {
        StartCoroutine(SendAliyunApiRequest());
    }

    #region Public Mehod
    //��ȡToken
    public string OnGetToken()
    {
        return token;
    }
    //��ȡApp key
    public string OnGetAppKey()
    {
        return appkey;
    }

    #endregion


    #region Private Method
    // Э�̣����Ͱ����� API ����
    private IEnumerator SendAliyunApiRequest()
    {
        // �����������
        var parameters = new Dictionary<string, string>
        {
            { "AccessKeyId", accessKeyId },
            { "Format", "JSON" },
            { "RegionId", "cn-shanghai" },
            { "SignatureMethod", "HMAC-SHA1" },
            { "SignatureNonce", Guid.NewGuid().ToString() },
            { "SignatureVersion", "1.0" },
            { "Timestamp", GetUtcTimestamp() },
            { "Version", "2019-02-28" } // ���ݾ������汾����
        };

        // ����� Action �����������ȡ Token �� Action �� CreateToken
        parameters.Add("Action", "CreateToken"); // ʾ�� Action������ʵ�� API �ĵ���д

        // 1. �Բ�����������
        var sortedParams = new SortedDictionary<string, string>(parameters);

        // 2. �����ǩ���ַ���
        var canonicalizedQueryString = GetCanonicalizedQueryString(sortedParams);
        var stringToSign = "GET&" + PercentEncode("/") + "&" + PercentEncode(canonicalizedQueryString);

        // 3. ����ǩ��
        var signature = SignString(stringToSign, accessKeySecret + "&");

        // 4. ���ǩ����������
        parameters.Add("Signature", signature);

        // 5. ������������ URL
        var url = endpoint + "?" + GetQueryString(parameters);

        // 6. ���� HTTP GET ����
        using (UnityWebRequest webRequest = UnityWebRequest.Get(url))
        {
            yield return webRequest.SendWebRequest();

            if (webRequest.result == UnityWebRequest.Result.Success)
            {
                Debug.Log("Token����ɹ�:");
                string _callback = webRequest.downloadHandler.text;
                var _data = JsonUtility.FromJson<TokenBody>(_callback);
                token = _data.Token.Id;
            }
            else
            {
                Debug.LogError("Token����ʧ��:");
                Debug.LogError(webRequest.error);
                Debug.LogError("�������»�ȡToken....");
                yield return new WaitForSeconds(1);//���»�ȡ
                StartCoroutine(SendAliyunApiRequest());
            }
        }
    }

    // ��ȡ UTC ʱ�������ʽΪ yyyy-MM-ddTHH:mm:ssZ
    private string GetUtcTimestamp()
    {
        DateTime utcNow = DateTime.UtcNow;
        return utcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
    }

    // �Բ������� URL ����
    private string PercentEncode(string value)
    {
        if (string.IsNullOrEmpty(value))
            return null;

        // ʹ�� UTF8 ���뽫�ַ���ת��Ϊ�ֽ�����
        byte[] bytes = Encoding.UTF8.GetBytes(value);

        // ����������ַ���
        StringBuilder encoded = new StringBuilder();
        foreach (byte b in bytes)
        {
            // ������Ҫ�������ַ���A-Z, a-z, 0-9, '-', '_', '.', '~'
            if (
                (b >= 'A' && b <= 'Z') || // A-Z
                (b >= 'a' && b <= 'z') || // a-z
                (b >= '0' && b <= '9') || // 0-9
                b == '-' || b == '_' || b == '.' || b == '~' // �����ַ�������
            )
            {
                encoded.Append((char)b); // ֱ�ӱ�����Щ�ַ�
            }
            else
            {
                // �����ַ�תΪ %XX ��ʽ
                encoded.Append('%');
                encoded.Append(BitConverter.ToString(new byte[] { b }).Replace("-", "").ToUpper());
            }
        }

        // �����ƶ���Ҫ���滻���ַ�
        string encodedString = encoded.ToString();
        encodedString = encodedString.Replace("+", "%20"); // �滻�Ӻ�
        encodedString = encodedString.Replace("*", "%2A"); // �滻�Ǻ�
        encodedString = encodedString.Replace("%7E", "~"); // �滻���˺ţ�ʵ���������Ѿ������� ~���˲����ܶ��࣬��Ϊ����ȫ���ݱ�����

        return encodedString;
    }

    // ����淶���Ĳ�ѯ�ַ���������ǩ����
    private string GetCanonicalizedQueryString(SortedDictionary<string, string> parameters)
    {
        var sb = new StringBuilder();
        foreach (var param in parameters)
        {
            if (sb.Length > 0)
                sb.Append("&");
            sb.Append(PercentEncode(param.Key));
            sb.Append("=");
            sb.Append(PercentEncode(param.Value));
        }
        return sb.ToString();
    }

    // �����ѯ�ַ��������ڹ��� URL��
    private string GetQueryString(Dictionary<string, string> parameters)
    {
        var sb = new StringBuilder();
        foreach (var param in parameters)
        {
            if (sb.Length > 0)
                sb.Append("&");
            sb.Append(param.Key);
            sb.Append("=");
            sb.Append(param.Value);
        }
        return sb.ToString();
    }

    // ����ǩ��
    private string SignString(string data, string key)
    {
        using (var algorithm = new HMACSHA1(Encoding.UTF8.GetBytes(key)))
        {
            var hashBytes = algorithm.ComputeHash(Encoding.UTF8.GetBytes(data));
            return Convert.ToBase64String(hashBytes);
        }
    }

    #endregion

    #region ���ݶ���

    [System.Serializable]
    public class TokenBody
    {
        public string ErrMsg=string.Empty;
        public TokenData Token=new TokenData();
    }
    [System.Serializable]
    public class TokenData
    {
        public string UserId = string.Empty;
        public string Id = string.Empty;//token
        public int ExpireTime;
    }
    #endregion
}
