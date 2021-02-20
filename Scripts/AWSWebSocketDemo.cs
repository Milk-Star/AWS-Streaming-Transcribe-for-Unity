using System;
using System.Collections.Generic;
using UnityEngine;
using System.Text;
using NativeWebSocket;
using UnityEngine.UI;

public class AWSWebSocketDemo : MonoBehaviour
{
  WebSocket websocket;
  float timer = 0f;
  int interator = 0;
  int lastSample;
  AudioClip clip;

  public KeyCode ConnectKey;
  public KeyCode DisconnectKey;
  public Text text;

  public string AudioFileType = "pcm";
  public string AudioBitRate = "16000";

  void Update()
  {
    if (Input.GetKeyDown(DisconnectKey))
      SendCloseStreamEvent();

    if (Input.GetKeyDown(ConnectKey))
      Connect();

    if (websocket == null || websocket.State != WebSocketState.Open)
      return;

#if !UNITY_WEBGL || UNITY_EDITOR
    websocket.DispatchMessageQueue();
#endif

    timer += Time.deltaTime;
    if (timer > 0.2f)
    {
      timer = 0;
      interator++;
      int pos = Microphone.GetPosition(null);
      int diff = pos - lastSample;
      if (diff > 0)
      {
        float[] samples = new float[diff * clip.channels];
        clip.GetData(samples, lastSample);


        AudioClip newclip = AudioClip.Create(interator.ToString(), samples.Length, clip.channels, clip.frequency, false);
        newclip.SetData(samples, 0);

        byte[] ba = ConvertAudioTo16BitPCM(newclip);

        SendAudioEvent(ba);
      }
      lastSample = pos;
    }
  }

  async void Connect()
  {
    Reset();
    foreach (string mic in Microphone.devices)
      Debug.LogError(mic);

    string url = FindObjectOfType<AWSTranscribePresignedURL>().GetRequestURL(AudioFileType, AudioBitRate);
    websocket = new WebSocket(url);

    int bitRate = Convert.ToInt32(AudioBitRate);
    clip = Microphone.Start("", false, 300, bitRate);

    websocket.OnOpen += () =>
    {
      Debug.LogError("Connection open!");
    };

    websocket.OnError += (e) =>
    {
      Debug.LogError("Error! " + e);
    };

    websocket.OnClose += (e) =>
    {
      Debug.LogError("Connection closed!");
    };

    websocket.OnMessage += ReadMessage;

    await websocket.Connect();
  }

  public byte[] ConvertAudioTo16BitPCM(AudioClip clipChunk) //from SavWav
  {
    //Get array of float values representing audio.
    var samples = new float[clipChunk.samples];
    clipChunk.GetData(samples, 0);

    //bytesData array is twice the size of
    //dataSource array because a float converted in Int16 is 2 bytes.
    Int16[] intData = new Int16[samples.Length];
    Byte[] bytesData = new Byte[samples.Length * 2];

    int rescaleFactor = 32767; //magic number to convert float to Int16. 
    //This works because float values for audio are between -1 and 1. 
    //The max an int16 can represent is 32767 so we can use this to remap.

    for (int i = 0; i < samples.Length; i++)
    {
      //get our int16 representation of a float value.
      intData[i] = (short)(samples[i] * rescaleFactor);
      Byte[] byteArr = new Byte[2];
      byteArr = BitConverter.GetBytes(intData[i]);
      byteArr.CopyTo(bytesData, i * 2);
    }

    return bytesData;
  }

  void SendAudioEvent(byte[] payload)
  {
    byte[] audioEvent = CreateAudioEvent(payload);
    websocket.Send(audioEvent);
  }

  void SendCloseStreamEvent()
  {
    if (websocket.State == WebSocketState.Open)
    {
      byte[] audioEvent = CreateAudioEvent(new byte[0]);
      websocket.Send(audioEvent);
    }
  }

  void Reset()
  {
    Microphone.End("");
    lastSample = 0;
    interator = 0;
    if (websocket != null)
    {
      websocket.OnMessage -= ReadMessage;
    }
  }

  //https://docs.aws.amazon.com/transcribe/latest/dg/event-stream.html
  byte[] CreateAudioEvent(byte[] payload) //CRCs and prelude must be bigendian.
  {
    Encoding utf8 = Encoding.UTF8;
    //Build our headers
    //ContentType
    List<byte> contentTypeHeader = GetHeaders(":content-type", "application/octet-stream");
    List<byte> eventTypeHeader = GetHeaders(":event-type", "AudioEvent");
    List<byte> messageTypeHeader = GetHeaders(":message-type", "event");
    List<byte> headers = new List<byte>();
    headers.AddRange(contentTypeHeader);
    headers.AddRange(eventTypeHeader);
    headers.AddRange(messageTypeHeader);

    //Calculate total byte length and headers byte length
    byte[] totalByteLength = BitConverter.GetBytes(headers.Count + payload.Length + 16); //16 accounts for 8 byte prelude, 2x 4 byte crcs.
    if (BitConverter.IsLittleEndian)
      Array.Reverse(totalByteLength);

    byte[] headersByteLength = BitConverter.GetBytes(headers.Count);
    if (BitConverter.IsLittleEndian)
      Array.Reverse(headersByteLength);
    
    //Build the prelude
    byte[] prelude = new byte[8];
    totalByteLength.CopyTo(prelude, 0);
    headersByteLength.CopyTo(prelude, 4);

    //calculate checksum for prelude (total + headers)
    var crc32 = new Crc32();
    byte[] preludeCRC = BitConverter.GetBytes(crc32.Get(prelude));
    if (BitConverter.IsLittleEndian)
      Array.Reverse(preludeCRC);

    //Construct the message
    List<byte> messageAsList = new List<byte>();
    messageAsList.AddRange(prelude);
    messageAsList.AddRange(preludeCRC);
    messageAsList.AddRange(headers);
    messageAsList.AddRange(payload);

    //Calculate checksum for message
    byte[] message = messageAsList.ToArray();
    byte[] messageCRC = BitConverter.GetBytes(crc32.Get(message));
    if (BitConverter.IsLittleEndian)
      Array.Reverse(messageCRC);

    //Add message checksum
    messageAsList.AddRange(messageCRC);
    message = messageAsList.ToArray();

    return message;
  }

  List<byte> GetHeaders(string headerName, string headerValue)
  {
    Encoding utf8 = Encoding.UTF8;

    byte[] name = utf8.GetBytes(headerName);
    byte[] nameByteLength = new byte[] { Convert.ToByte(name.Length) };
    byte[] valueType = new byte[] { Convert.ToByte(7) }; //7 represents a string
    byte[] value = utf8.GetBytes(headerValue);
    byte[] valueByteLength = new byte[2];
    //byte length array is always two bytes regardless of the int it represents.
    valueByteLength[0] = (byte)((value.Length & 0xFF00) >> 8);
    valueByteLength[1] = (byte)((value.Length & 0x00FF));

    //Construct the header
    List<byte> headerList = new List<byte>();
    headerList.AddRange(nameByteLength);
    headerList.AddRange(name);
    headerList.AddRange(valueType);
    headerList.AddRange(valueByteLength);
    headerList.AddRange(value);

    return headerList;
  }

  void ReadMessage(byte[] bytes)
  {
    //First 8 bytes are the prelude with info about header lengths and total length.
    byte[] totalByteLengthBytes = new byte[4];
    Array.Copy(bytes, totalByteLengthBytes, 4);
    if (BitConverter.IsLittleEndian)
      Array.Reverse(totalByteLengthBytes);
    //an int32 is 4 bytes
    int totalByteLength = BitConverter.ToInt32(totalByteLengthBytes, 0);

    byte[] headersByteLengthBytes = new byte[4];
    Array.Copy(bytes, 4, headersByteLengthBytes, 0, 4);
    if (BitConverter.IsLittleEndian)
      Array.Reverse(headersByteLengthBytes);
    int headersByteLength = BitConverter.ToInt32(headersByteLengthBytes, 0);

    //Use the prelude to get the offset of the message.
    int offset = headersByteLength + 12;
    //Message length is everything but the headers, CRCs, and prelude.
    int payloadLength = totalByteLength - (headersByteLength + 16);
    byte[] payload = new byte[payloadLength];
    Array.Copy(bytes, offset, payload, 0, payloadLength);
    string message = System.Text.Encoding.UTF8.GetString(payload);

    //Convert the message to and object so we can easily get the results.
    Debug.LogError("Received OnMessage! (" + bytes.Length + " bytes) " + message);
    AWSTranscribeWebsocketMessage jsonMessage = JsonUtility.FromJson<AWSTranscribeWebsocketMessage>(message);

    //REMOVE
    //Display
    if (jsonMessage != null && jsonMessage.Transcript.Results.Count > 0)
    {
      text.text = jsonMessage.Transcript.Results[0].Alternatives[0].Transcript;
      Debug.LogError(text.text);
    }
  }

  private async void OnApplicationQuit()
  {
    SendCloseStreamEvent();
    Reset();
    await websocket.Close();
  }
}