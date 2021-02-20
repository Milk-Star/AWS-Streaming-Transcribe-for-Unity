using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class AWSTranscribeWebsocketMessage
{
  public Transcript Transcript;
}

[System.Serializable]
public class Item
{
  public string Content;
  public double EndTime;
  public double StartTime;
  public string Type;
  public bool VocabularyFilterMatch;
}

[System.Serializable]
public class Alternative
{
  public List<Item> Items;
  public string Transcript;
}

[System.Serializable]
public class Result
{
  public List<Alternative> Alternatives;
  public double EndTime;
  public bool IsPartial;
  public string ResultId;
  public double StartTime;
}

[System.Serializable]
public class Transcript
{
  public List<Result> Results;
}