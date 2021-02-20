using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Text;
using System.Security.Cryptography;
using System;
using System.Globalization;

public class AWSTranscribePresignedURL : MonoBehaviour
{
  string method = "GET";
  string service = "transcribe";
  [SerializeField] string region = "us-east-2";
  string endpoint; //Start $"wss://transcribestreaming.{region}.amazonaws.com:8443";
  string host; //Start $"transcribestreaming.{region}.amazonaws.com:8443";
  string amz_date = "YYYYMMDD'T'HHMMSS'Z'"; //Start now.ToString("yyyyMMdd'T'hhmmss'Z'");
  string datestamp = "YYYYMMDD"; //Start now.ToString("yyyyMMdd");
  string canonical_uri = "/stream-transcription-websocket";
  string canonical_headers; //Start $"host:{host}\n";
  string signed_headers = "host";
  string algorithm = "AWS4-HMAC-SHA256";
  string credential_scope; //Start
  string canonical_querystring;
  [SerializeField] string accessKey;
  string payload_hash; //CreatePayloadHash
  string canonical_request; //CreateCanonicalRequest
  string string_to_sign;
  [SerializeField] string secret_key;
  string signature;
  string request_url;

  public string GetRequestURL(string audioFileType, string audioBitRate)
  {
    endpoint = $"wss://transcribestreaming.{region}.amazonaws.com:8443";
    host = $"transcribestreaming.{region}.amazonaws.com:8443";

    DateTime now = DateTime.Now.ToUniversalTime();
    amz_date = now.ToString("yyyyMMdd'T'HHmmss'Z'");
    datestamp = now.ToString("yyyyMMdd");

    canonical_headers = $"host:{host}\n";

    credential_scope = $"{datestamp}%2F{region}%2F{service}%2Faws4_request";

    CreateCanonicalQuerystring(audioFileType, audioBitRate); 
    CreatePayloadHash();
    CreateCanonicalRequest(); 
    CreateStringToSign();
    CreateSignature();
    CreateURL();

    return request_url;
  }

  void CreateCanonicalQuerystring(string audioFileType, string audioBitRate)
  {
    canonical_querystring = "X-Amz-Algorithm=" + algorithm;
    canonical_querystring += "&X-Amz-Credential=" + accessKey + "%2F" + credential_scope;
    canonical_querystring += "&X-Amz-Date=" + amz_date;
    canonical_querystring += "&X-Amz-Expires=300";
    canonical_querystring += "&X-Amz-SignedHeaders=" + signed_headers;
    canonical_querystring += $"&language-code=en-US&media-encoding={audioFileType}&sample-rate={audioBitRate}";
  }

  void CreatePayloadHash()
  {
    payload_hash = ToHex(Hash(""));
  }

  void CreateCanonicalRequest()
  {
    canonical_request = method + '\n'
       + canonical_uri + '\n'
       + canonical_querystring + '\n'
       + canonical_headers + '\n'
       + signed_headers + '\n'
       + payload_hash;

    print("Canonical Request: " + canonical_request);
  }

  void CreateStringToSign()
  {
    string hashedCanonicalRequest = ToHex(Hash(canonical_request));
    //For some reason the response will contain forward slashes and not %2F. 
    string newcredentialscope = $"{datestamp}/{region}/{service}/aws4_request";

    string_to_sign = algorithm + "\n"
      + amz_date + "\n"
      + newcredentialscope + "\n"
      + hashedCanonicalRequest;

    print("StringToSign: " + string_to_sign);
  }

  void CreateSignature()
  {
    // Create the signing key
    byte[] signing_key = GetSignatureKey(secret_key, datestamp, region, service);
    signature = ToHex(GetKeyedHash(signing_key, string_to_sign));
  }

  void CreateURL()
  {
    canonical_querystring += "&X-Amz-Signature=" + signature;
    request_url = endpoint + canonical_uri + "?" + canonical_querystring;
  }


  static byte[] HmacSHA256(String data, byte[] key)
  {
    String algorithm = "HmacSHA256";
    KeyedHashAlgorithm kha = KeyedHashAlgorithm.Create(algorithm);
    kha.Key = key;

    return kha.ComputeHash(Encoding.UTF8.GetBytes(data));
  }

  static byte[] GetSignatureKey(String key, String dateStamp, String regionName, String serviceName)
  {
    byte[] kSecret = Encoding.UTF8.GetBytes(("AWS4" + key).ToCharArray());
    byte[] kDate = HmacSHA256(dateStamp, kSecret);
    byte[] kRegion = HmacSHA256(regionName, kDate);
    byte[] kService = HmacSHA256(serviceName, kRegion);
    byte[] kSigning = HmacSHA256("aws4_request", kService);

    return kSigning;
  }

  public static byte[] Hash(string value)
  {
    return new SHA256CryptoServiceProvider().ComputeHash(Encoding.UTF8.GetBytes(value));
  }


  public static string ToHex(byte[] data)
  {
    StringBuilder sb = new StringBuilder();
    for (int i = 0; i < data.Length; i++)
    {
      sb.Append(data[i].ToString("x2", CultureInfo.InvariantCulture));
    }
    return sb.ToString();
  }

  public static byte[] GetKeyedHash(byte[] key, string value)
  {
    KeyedHashAlgorithm mac = new HMACSHA256(key);
    mac.Initialize();
    return mac.ComputeHash(Encoding.UTF8.GetBytes(value));
  }


}

