using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sts2SkinManager.Config;

public class CharacterSkinChoice
{
    [JsonPropertyName("active")]
    public string Active { get; set; } = "default";

    [JsonPropertyName("available_variants")]
    public List<string> AvailableVariants { get; set; } = new();
}

public class SkinChoicesConfig
{
    public Dictionary<string, CharacterSkinChoice> Characters { get; set; } = new();

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static SkinChoicesConfig LoadOrEmpty(string path)
    {
        if (!File.Exists(path)) return new SkinChoicesConfig();
        try
        {
            var json = File.ReadAllText(path);
            var dict = JsonSerializer.Deserialize<Dictionary<string, CharacterSkinChoice>>(json, JsonOpts);
            return new SkinChoicesConfig { Characters = dict ?? new() };
        }
        catch
        {
            return new SkinChoicesConfig();
        }
    }

    public void Save(string path)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        var json = JsonSerializer.Serialize(Characters, JsonOpts);
        File.WriteAllText(path, json);
    }

    public void SyncAvailableVariants(string character, IEnumerable<string> variants)
    {
        if (!Characters.TryGetValue(character, out var choice))
        {
            choice = new CharacterSkinChoice { Active = "default" };
            Characters[character] = choice;
        }
        var list = new List<string> { "default" };
        list.AddRange(variants.Where(v => v != "default").OrderBy(v => v));
        choice.AvailableVariants = list.Distinct().ToList();
        if (!choice.AvailableVariants.Contains(choice.Active))
        {
            choice.Active = "default";
        }
    }
}
