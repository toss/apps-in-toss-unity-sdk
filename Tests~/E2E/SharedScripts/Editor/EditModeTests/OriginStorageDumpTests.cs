// -----------------------------------------------------------------------
// OriginStorageDumpTests.cs - EditMode MigrationGetOriginStorage 응답 역직렬화 검증
// Level 0: union 타입 keyPath(string | string[] | null)가 응답 전체를 실패시키지 않는지 확인
// -----------------------------------------------------------------------

using AppsInToss;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

[TestFixture]
public class OriginStorageDumpTests
{
    private const string Dump = @"{
      ""origin"": ""https://example.test"",
      ""indexedDB"": [{
        ""name"": ""db"",
        ""version"": 1,
        ""objectStores"": [
          { ""name"": ""scalar"", ""keyPath"": ""id"", ""autoIncrement"": false, ""indexes"": [], ""records"": [] },
          { ""name"": ""none"", ""keyPath"": null, ""autoIncrement"": false, ""indexes"": [], ""records"": [] },
          { ""name"": ""compound"", ""keyPath"": [""a"", ""b""], ""autoIncrement"": false,
            ""indexes"": [{ ""name"": ""i"", ""keyPath"": [""a"", ""b""], ""multiEntry"": false, ""unique"": false }],
            ""records"": [] }
        ]
      }]
    }";

    [Test]
    public void KeyPath_AcceptsStringNullAndArray()
    {
        var dump = JsonConvert.DeserializeObject<OriginStorageDump>(Dump);
        var stores = dump.IndexedDB[0].ObjectStores;

        Assert.AreEqual("id", stores[0].KeyPath);
        Assert.IsNull(stores[1].KeyPath);
        Assert.IsInstanceOf<JArray>(stores[2].KeyPath);
        Assert.AreEqual(2, ((JArray)stores[2].KeyPath).Count);
        Assert.IsInstanceOf<JArray>(stores[2].Indexes[0].KeyPath);
    }
}
