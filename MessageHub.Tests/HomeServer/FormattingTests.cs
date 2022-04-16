using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using MessageHub.HomeServer.Formatting;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MessageHub.Tests.HomeServer;

[TestClass]
public class FormattingTests
{
    private static string GetFilePath([CallerFilePath] string? filePath = null)
    {
        return filePath!;
    }

    [TestMethod]
    public void TestMethod1()
    {
        string filePath = GetFilePath();
        string examplesFilePath = Path.Combine(Path.GetDirectoryName(filePath)!, "json-examples.txt");
        var lines = File.ReadAllLines(examplesFilePath);
        var bytes = Encoding.UTF8.GetBytes(string.Join('\n', lines)).AsSpan();

        var elements = new List<JsonElement?>();
        var jsonList = new List<string>();
        while (true)
        {
            var reader = new Utf8JsonReader(bytes);
            if (JsonElement.TryParseValue(ref reader, out var element))
            {
                elements.Add(element);
                string json = Encoding.UTF8.GetString(bytes[..(int)reader.BytesConsumed]);
                jsonList.Add(json);
                bytes = bytes[(int)reader.BytesConsumed..];
                if (bytes.Length == 0)
                {
                    break;
                }
                bytes = bytes[1..]; // Skip new line.
            }
            else
            {
                break;
            }
        }
        Assert.IsTrue(elements.Count > 0, elements.Count.ToString());
        Assert.IsTrue(elements.Count % 2 == 0, elements.Count.ToString());

        for (int i = 0; i < elements.Count; i += 2)
        {
            var (original, expected) = (elements[i], jsonList[i + 1]);
            var actual = CanonicalJson.Serialize(original);
            Assert.AreEqual(expected, actual, original.ToString());
        }
    }
}
